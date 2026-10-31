/* global PFI18n, InvestmentMath */
/**
 * Saved Investment Scenarios on the Investment Calculator (#592): Save stores the plan's inputs (never results) under a
 * name; the Saved panel lists each scenario with its headline result, worked out here from its inputs, and loads,
 * compares, renames, duplicates or deletes it. The panel is a section at the end of the page on a phone and a
 * slide-out drawer (a modal dialog) from the wide layout up.
 *
 * Writes go to the Web app's /api/InvestmentScenarios with the antiforgery token; a failure shows the message the
 * server sends, in the reader's language. When the scenarios could not be loaded, the panel shows the unavailable
 * notice, Save stays disabled and the calculator keeps working.
 *
 * calculator.js creates this with create(root, deps) and calls update(plan) after every change of the plan.
 */
(function () {
    'use strict';

    const API = '/api/InvestmentScenarios';
    /** Where the panel becomes a drawer: the layout that pins the bar and rail (investment-calculator.css). */
    const WIDE_QUERY = '(min-width: 62em)';
    const NAME_MAX = 100;

    /**
     * The scenario enums as the server spells them (InvestmentScenarioMode, InvestmentGoalSolveFor), with the goal
     * tile's question ('monthly' or 'years', InvestmentMath.solveGoal's mode) mapped to its solve-for value.
     */
    const MODES = Object.freeze({
        grow: 'Grow',
        goal: 'Goal',
        solveFor: Object.freeze({ monthly: 'Monthly', years: 'Years' })
    });

    /**
     * The goal tile's question for a scenario's solve-for value.
     * @param {string|null} solveFor - 'Monthly', 'Years' or null.
     * @returns {string} 'years' or 'monthly'.
     */
    function goalModeOf (solveFor) {
        return solveFor === MODES.solveFor.years ? 'years' : 'monthly';
    }

    /**
     * Round to cents, as the server stores amounts and rates.
     * @param {number} value - The value.
     * @returns {number} The value with at most two decimals.
     */
    function cents (value) {
        return Math.round(Number(value) * 100) / 100;
    }

    /**
     * A scenario's inputs as InvestmentMath reads them.
     * @param {object} s - The scenario.
     * @returns {object} The inputs.
     */
    function toInputs (s) {
        return {
            initial: Number(s.initialAmount),
            monthly: Number(s.monthlyContribution),
            rate: Number(s.assumedReturn),
            years: Number(s.years),
            increase: Number(s.contributionIncrease),
            inflation: Number(s.inflation),
            fee: Number(s.fee),
            delay: Number(s.delayYears) || 0,
            stopAt: s.stopAfterYear === null || s.stopAfterYear === undefined ? null : Number(s.stopAfterYear)
        };
    }

    /**
     * The inputs of a scenario or a save request as one comparable string (no name, note or dates).
     * @param {object} s - The scenario or request.
     * @returns {string} The key.
     */
    function inputsKey (s) {
        const goal = s.mode === MODES.goal;
        return JSON.stringify([
            s.mode, cents(s.initialAmount), cents(s.monthlyContribution), cents(s.assumedReturn), Number(s.years),
            cents(s.contributionIncrease), cents(s.inflation), cents(s.fee), Number(s.delayYears) || 0,
            s.stopAfterYear === null || s.stopAfterYear === undefined ? null : Number(s.stopAfterYear),
            goal ? cents(s.goalTarget) : null, goal ? s.goalSolveFor : null
        ]);
    }

    /**
     * Last edited first, newest id breaking a tie: the server's order.
     * @param {object} a - A scenario.
     * @param {object} b - Another.
     * @returns {number} The sort order.
     */
    function byLastEdited (a, b) {
        const diff = Date.parse(b.lastEditedAtUtc) - Date.parse(a.lastEditedAtUtc);
        return diff !== 0 ? diff : b.id - a.id;
    }

    /**
     * Create the Saved panel's controller.
     * @param {HTMLElement} root - The calculator root.
     * @param {object} deps - currency, plan(), goal(), load(scenario), compare (or null), money(amount, code).
     * @returns {{update: Function}} update(plan) refreshes the "loaded" line and the Save state.
     */
    function create (root, deps) {
        const byId = (id) => document.getElementById(id);
        const t = (key, args) => PFI18n.t(`InvestmentCalculator.Js.Saved.${key}`, args);
        const panel = byId('icSaved');
        const list = byId('icSavedList');
        const opener = byId('icSavedOpen');
        const saveButton = byId('icSave');
        const closeButton = byId('icSavedClose');
        const backdrop = byId('icSavedBackdrop');
        const dialog = byId('icSaveDialog');
        const form = byId('icSaveForm');
        const nameField = byId('icSaveName');
        const noteField = byId('icSaveNote');
        const updateButton = byId('icSaveUpdate');
        const newButton = byId('icSaveNew');
        const cancelButton = byId('icSaveCancel');
        if (!panel) {
            return { update () {} };
        }

        const unavailable = panel.dataset.unavailable === 'true';
        const max = parseInt(panel.dataset.max, 10) || 20;
        let scenarios = [];
        try {
            scenarios = JSON.parse(panel.dataset.scenarios || '[]');
        } catch (e) {
            scenarios = [];
        }
        scenarios.sort(byLastEdited);
        /** The scenario the plan came from, and its inputs when loaded or last saved. */
        let loaded = null;
        let loadedKey = null;
        let edited = false;
        let busy = false;
        /** What the dialog is doing: {kind: 'save'|'rename'|'delete', scenario}. */
        let task = null;
        let drawerOpen = false;
        let lastFocus = null;

        /**
         * Write a message to the panel's live region.
         * @param {string} text - The message.
         */
        function announce (text) {
            const status = byId('icSavedStatus');
            if (status) {
                status.textContent = '';
                // A fresh node change, so the same message twice is read twice.
                window.setTimeout(() => { status.textContent = text; }, 50);
            }
        }

        /**
         * Show or clear an error.
         * @param {HTMLElement|null} el - The error element.
         * @param {string} text - The message, or empty to hide it.
         */
        function showError (el, text) {
            if (el) {
                el.textContent = text;
                el.hidden = !text;
            }
        }

        /**
         * The current plan as a save request.
         * @param {string} name - The name.
         * @param {string} note - The note.
         * @returns {object} The request.
         */
        function request (name, note) {
            const plan = deps.plan();
            const goal = deps.goal();
            const hasGoal = goal.touched && goal.target > 0;
            return {
                name,
                note: note || null,
                mode: hasGoal ? MODES.goal : MODES.grow,
                currencyCode: deps.currency,
                initialAmount: cents(plan.initial),
                monthlyContribution: cents(plan.monthly),
                assumedReturn: cents(plan.rate),
                years: plan.years,
                contributionIncrease: cents(plan.increase),
                inflation: cents(plan.inflation),
                fee: cents(plan.fee),
                delayYears: plan.delay || 0,
                stopAfterYear: plan.stopAt === null || plan.stopAt === undefined ? null : plan.stopAt,
                goalTarget: hasGoal ? cents(goal.target) : null,
                goalSolveFor: hasGoal ? (MODES.solveFor[goal.mode] || MODES.solveFor.monthly) : null
            };
        }

        /**
         * A scenario's stored inputs sent back with a new name and note (a rename).
         * @param {object} s - The scenario.
         * @param {string} name - The new name.
         * @param {string} note - The new note.
         * @returns {object} The request.
         */
        function renameRequest (s, name, note) {
            return {
                name,
                note: note || null,
                mode: s.mode,
                currencyCode: s.currencyCode,
                initialAmount: s.initialAmount,
                monthlyContribution: s.monthlyContribution,
                assumedReturn: s.assumedReturn,
                years: s.years,
                contributionIncrease: s.contributionIncrease,
                inflation: s.inflation,
                fee: s.fee,
                delayYears: s.delayYears,
                stopAfterYear: s.stopAfterYear,
                goalTarget: s.goalTarget,
                goalSolveFor: s.goalSolveFor
            };
        }

        /**
         * Call the Web app's scenario endpoints.
         * @param {string} method - The HTTP method.
         * @param {string} url - The endpoint.
         * @param {object} [body] - The JSON body.
         * @returns {Promise<*>} The JSON answer.
         */
        async function api (method, url, body) {
            let response;
            try {
                response = await fetch(url, {
                    method,
                    headers: {
                        'Accept': 'application/json',
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': window.PFUtils ? window.PFUtils.getAntiForgeryToken(root) : ''
                    },
                    body: body === undefined ? undefined : JSON.stringify(body)
                });
            } catch (e) {
                throw new Error(t('Failed'));
            }
            if (!response.ok) {
                const answer = await response.json().catch(() => ({}));
                throw new Error(answer.error || t('Failed'));
            }
            return response.json().catch(() => ({}));
        }

        /**
         * A scenario's headline: its balance at the end of its years, in its own currency.
         * @param {object} s - The scenario.
         * @returns {string} E.g. "$300,851 in 20 years".
         */
        function headline (s) {
            const projection = InvestmentMath.project(InvestmentMath.normalize(toInputs(s)));
            return t('Headline', {
                amount: deps.money(projection.final, s.currencyCode),
                years: PFI18n.plural('InvestmentCalculator.Js.Years', Number(s.years))
            });
        }

        /**
         * One button of a scenario's row.
         * @param {string} act - The action.
         * @param {string} label - The visible text.
         * @param {string} nameId - The id of the scenario's name, which describes the button.
         * @param {boolean} [disabled] - Whether it is disabled.
         * @returns {HTMLButtonElement} The button.
         */
        function actionButton (act, label, nameId, disabled) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = `ic-action ic-saved__act ic-saved__act--${act}`;
            button.dataset.act = act;
            button.textContent = label;
            button.setAttribute('aria-describedby', nameId);
            button.disabled = Boolean(disabled);
            return button;
        }

        /**
         * One scenario's row.
         * @param {object} s - The scenario.
         * @returns {HTMLLIElement} The row.
         */
        function item (s) {
            const li = document.createElement('li');
            const current = loaded && loaded.id === s.id;
            li.className = `ic-saved__item${current ? ' is-current' : ''}`;
            li.dataset.id = String(s.id);
            const nameId = `icSavedName${s.id}`;
            const other = s.currencyCode !== deps.currency;

            const name = document.createElement('p');
            name.className = 'ic-saved__name';
            name.id = nameId;
            name.textContent = s.name;
            li.appendChild(name);

            const result = document.createElement('p');
            result.className = 'ic-saved__headline';
            result.textContent = headline(s);
            li.appendChild(result);

            const meta = document.createElement('p');
            meta.className = 'ic-saved__meta';
            const parts = [t('Edited', {
                date: PFI18n.formatDate(new Date(s.lastEditedAtUtc), { year: 'numeric', month: 'short', day: 'numeric' })
            })];
            if (s.mode === MODES.goal) {
                parts.push(t('Goal', { amount: deps.money(Number(s.goalTarget), s.currencyCode) }));
            }
            meta.textContent = parts.join(' · ');
            li.appendChild(meta);

            if (s.note) {
                const note = document.createElement('p');
                note.className = 'ic-saved__note';
                note.textContent = s.note;
                li.appendChild(note);
            }
            if (other) {
                const notice = document.createElement('p');
                notice.className = 'ic-saved__other';
                notice.textContent = t('OtherCurrency', { currency: s.currencyCode });
                li.appendChild(notice);
            }

            const actions = document.createElement('div');
            actions.className = 'ic-saved__actions';
            const load = actionButton('load', t('Load'), nameId, other);
            load.classList.add('ic-action--primary');
            actions.append(
                load,
                actionButton('compare', t('Compare'), nameId, other || !deps.compare),
                actionButton('rename', t('Rename'), nameId),
                actionButton('duplicate', t('Duplicate'), nameId),
                actionButton('delete', t('Delete'), nameId)
            );
            li.appendChild(actions);
            return li;
        }

        /**
         * Redraw the list, the counts and the "loaded" line, then put focus back where it was.
         * @param {{id: number, act: string}|null} [focus] - The button to focus afterwards.
         */
        function render (focus) {
            const count = byId('icSavedCount');
            if (count) {
                count.textContent = unavailable ? '' : PFI18n.formatNumber(scenarios.length);
            }
            if (unavailable || !list) {
                return;
            }
            const room = byId('icSavedRoom');
            if (room) {
                room.textContent = t('Room', { count: PFI18n.formatNumber(scenarios.length), max: PFI18n.formatNumber(max) });
            }
            const empty = byId('icSavedEmpty');
            if (empty) {
                empty.hidden = scenarios.length > 0;
            }
            list.replaceChildren(...scenarios.map(item));
            showCurrent();
            if (focus) {
                const target = list.querySelector(`[data-id="${focus.id}"] [data-act="${focus.act}"]`) ||
                    list.querySelector(`[data-id="${focus.id}"] [data-act="load"]`);
                (target || panel).focus();
            }
        }

        /**
         * Say which scenario the plan came from, and whether it changed since.
         */
        function showCurrent () {
            const line = byId('icSavedCurrent');
            if (!line) {
                return;
            }
            line.hidden = !loaded;
            line.textContent = loaded ? t(edited ? 'CurrentEdited' : 'Current', { name: loaded.name }) : '';
        }

        /**
         * Open the panel: the drawer on wide screens, a scroll to the section otherwise.
         */
        function openPanel () {
            if (!window.matchMedia || !window.matchMedia(WIDE_QUERY).matches) {
                const reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
                panel.scrollIntoView({ block: 'start', behavior: reduced ? 'auto' : 'smooth' });
                panel.focus({ preventScroll: true });
                return;
            }
            drawerOpen = true;
            lastFocus = document.activeElement;
            panel.classList.add('is-open');
            panel.setAttribute('role', 'dialog');
            panel.setAttribute('aria-modal', 'true');
            if (backdrop) {
                backdrop.hidden = false;
            }
            if (closeButton) {
                closeButton.hidden = false;
            }
            if (opener) {
                opener.setAttribute('aria-expanded', 'true');
            }
            document.addEventListener('keydown', onDrawerKey);
            panel.focus();
        }

        /**
         * Close the drawer.
         * @param {boolean} [restore] - Whether focus goes back to where it was (default true).
         */
        function closePanel (restore) {
            if (!drawerOpen) {
                return;
            }
            drawerOpen = false;
            panel.classList.remove('is-open');
            panel.removeAttribute('role');
            panel.removeAttribute('aria-modal');
            if (backdrop) {
                backdrop.hidden = true;
            }
            if (closeButton) {
                closeButton.hidden = true;
            }
            if (opener) {
                opener.setAttribute('aria-expanded', 'false');
            }
            document.removeEventListener('keydown', onDrawerKey);
            if (restore !== false) {
                (lastFocus && document.contains(lastFocus) ? lastFocus : opener || root).focus();
            }
        }

        /**
         * Escape closes the drawer and Tab stays inside it, unless the save dialog is open over it.
         * @param {KeyboardEvent} event - The key event.
         */
        function onDrawerKey (event) {
            if (!drawerOpen || (dialog && dialog.open)) {
                return;
            }
            if (event.key === 'Escape') {
                event.preventDefault();
                closePanel();
                return;
            }
            if (event.key !== 'Tab') {
                return;
            }
            const focusable = Array.from(panel.querySelectorAll('button:not([disabled]):not([hidden]), a[href], [tabindex="0"]'))
                .filter((el) => el.offsetParent !== null);
            if (focusable.length === 0) {
                event.preventDefault();
                panel.focus();
                return;
            }
            const first = focusable[0];
            const last = focusable[focusable.length - 1];
            if (event.shiftKey && (document.activeElement === first || document.activeElement === panel)) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        }

        /**
         * Open the dialog for saving the plan, renaming a scenario or confirming its deletion.
         * @param {string} kind - 'save', 'rename' or 'delete'.
         * @param {object|null} scenario - The scenario renamed or deleted.
         */
        function openDialog (kind, scenario) {
            if (!dialog || typeof dialog.showModal !== 'function') {
                return;
            }
            task = { kind, scenario };
            const title = byId('icSaveDialogTitle');
            const text = byId('icSaveDialogText');
            const fields = byId('icSaveFields');
            const full = scenarios.length >= max;
            showError(byId('icSaveError'), '');
            fields.hidden = kind === 'delete';
            text.hidden = true;
            text.textContent = '';
            updateButton.hidden = true;
            newButton.disabled = false;
            newButton.classList.toggle('ic-action--danger', kind === 'delete');

            if (kind === 'save') {
                title.textContent = t('SaveTitle');
                nameField.value = loaded ? loaded.name : '';
                noteField.value = loaded && loaded.note ? loaded.note : '';
                if (loaded) {
                    text.hidden = false;
                    text.textContent = t(edited ? 'EditedHint' : 'LoadedHint', { name: loaded.name });
                    updateButton.hidden = !edited;
                    updateButton.textContent = t('Update', { name: loaded.name });
                    newButton.textContent = t('SaveAsNew');
                } else {
                    newButton.textContent = t('SaveNew');
                }
                if (full) {
                    newButton.disabled = true;
                    showError(byId('icSaveError'), t('Full', { max: PFI18n.formatNumber(max) }));
                }
            } else if (kind === 'rename') {
                title.textContent = t('RenameTitle');
                nameField.value = scenario.name;
                noteField.value = scenario.note || '';
                newButton.textContent = t('RenameAction');
            } else {
                title.textContent = t('DeleteTitle');
                text.hidden = false;
                text.textContent = t('DeleteText', { name: scenario.name });
                newButton.textContent = t('DeleteAction');
            }
            dialog.showModal();
            if (kind === 'delete') {
                cancelButton.focus();
            } else {
                nameField.focus();
                nameField.select();
            }
        }

        /**
         * Put a saved scenario in the list, last edited first.
         * @param {object} saved - The scenario the server answered with.
         */
        function remember (saved) {
            scenarios = scenarios.filter((s) => s.id !== saved.id).concat(saved).sort(byLastEdited);
        }

        /**
         * Make a saved scenario the one the plan came from.
         * @param {object} saved - The scenario the server answered with.
         */
        function adopt (saved) {
            remember(saved);
            loaded = saved;
            loadedKey = inputsKey(saved);
        }

        /**
         * Note a renamed scenario, which is also the loaded one's new name when it is the loaded one.
         * @param {object} saved - The scenario the server answered with.
         */
        function renamed (saved) {
            remember(saved);
            if (loaded && loaded.id === saved.id) {
                loaded = saved;
            }
        }

        /**
         * Forget a deleted scenario.
         * @param {object} gone - The scenario.
         */
        function forget (gone) {
            scenarios = scenarios.filter((s) => s.id !== gone.id);
            if (loaded && loaded.id === gone.id) {
                loaded = null;
                loadedKey = null;
            }
        }

        /**
         * Send what the dialog asked for; the answer updates the list (and the loaded scenario).
         * @param {object} job - The dialog's task.
         * @param {string} choice - 'update' or 'new' (a save).
         * @param {string} name - The trimmed name.
         * @param {string} note - The trimmed note.
         * @returns {Promise<{message: string, focus: object|null}>} What to announce and focus.
         */
        async function send (job, choice, name, note) {
            if (job.kind === 'save' && choice === 'update' && loaded) {
                const saved = await api('PUT', `${API}/${loaded.id}`, request(name, note));
                adopt(saved);
                return { message: t('Updated', { name: saved.name }), focus: null };
            }
            if (job.kind === 'save') {
                const saved = await api('POST', API, request(name, note));
                adopt(saved);
                return { message: t('Saved', { name: saved.name }), focus: null };
            }
            if (job.kind === 'rename') {
                const saved = await api('PUT', `${API}/${job.scenario.id}`, renameRequest(job.scenario, name, note));
                renamed(saved);
                return { message: t('Renamed', { name: saved.name }), focus: { id: saved.id, act: 'rename' } };
            }
            await api('DELETE', `${API}/${job.scenario.id}`);
            forget(job.scenario);
            return { message: t('Deleted', { name: job.scenario.name }), focus: null };
        }

        /**
         * Close the dialog after a success and say what happened.
         * @param {object} job - The dialog's task.
         * @param {{message: string, focus: object|null}} done - What to announce and focus.
         */
        function finish (job, done) {
            edited = false;
            dialog.close();
            showError(byId('icSavedError'), '');
            render(done.focus);
            if (job.kind === 'delete') {
                const next = list && list.querySelector('[data-act="load"]:not([disabled]), [data-act="rename"]');
                (next || panel).focus();
            }
            announce(done.message);
        }

        /**
         * Hold or release the dialog while a request runs.
         * @param {object} job - The dialog's task.
         * @param {boolean} hold - True while the request runs.
         */
        function holdDialog (job, hold) {
            busy = hold;
            form.toggleAttribute('aria-busy', hold);
            updateButton.disabled = hold;
            // Saving as new stays off while the list is full; openDialog sets every button afresh anyway.
            newButton.disabled = hold || (job.kind === 'save' && scenarios.length >= max);
        }

        /**
         * Handle the dialog's buttons: one request at a time, the dialog kept open with the message on failure.
         * @param {SubmitEvent} event - The submit event.
         */
        async function onSubmit (event) {
            event.preventDefault();
            if (busy || !task) {
                return;
            }
            const job = task;
            const choice = event.submitter && event.submitter.value === 'update' ? 'update' : 'new';
            const name = nameField.value.trim();
            const note = noteField.value.trim();
            const error = byId('icSaveError');
            if (job.kind !== 'delete' && !name) {
                showError(error, t('NameRequired'));
                nameField.focus();
                return;
            }
            holdDialog(job, true);
            try {
                finish(job, await send(job, choice, name, note));
            } catch (e) {
                showError(error, e.message);
            } finally {
                holdDialog(job, false);
            }
        }

        /**
         * Load a scenario into the calculator.
         * @param {object} s - The scenario.
         */
        function load (s) {
            loaded = s;
            loadedKey = inputsKey(s);
            edited = false;
            closePanel(false);
            deps.load(s);
            render();
            if (opener) {
                opener.focus();
            }
            announce(t('Loaded', { name: s.name }));
        }

        /**
         * Add a scenario to the Compare view.
         * @param {object} s - The scenario.
         */
        function compareScenario (s) {
            const added = deps.compare.add({ inputs: toInputs(s), name: s.name, source: 'saved' });
            if (!added) {
                showError(byId('icSavedError'), t('CompareFull'));
                return;
            }
            showError(byId('icSavedError'), '');
            closePanel(false);
            const tab = byId('icViewCompareTab');
            if (tab) {
                if (tab.getAttribute('aria-selected') !== 'true') {
                    tab.click();
                }
                tab.focus();
            }
            announce(PFI18n.t('InvestmentCalculator.Js.Compare.Added', { name: s.name }));
        }

        /**
         * Note that a list request started or ended.
         * @param {boolean} hold - True while it runs.
         */
        function holdList (hold) {
            busy = hold;
        }

        /**
         * Duplicate a scenario as "Copy of …".
         * @param {object} s - The scenario.
         */
        async function duplicate (s) {
            if (busy) {
                return;
            }
            const errorEl = byId('icSavedError');
            if (scenarios.length >= max) {
                showError(errorEl, t('Full', { max: PFI18n.formatNumber(max) }));
                return;
            }
            holdList(true);
            try {
                const copy = await api('POST', `${API}/${s.id}/duplicate`, {
                    name: t('CopyName', { name: s.name }).slice(0, NAME_MAX)
                });
                remember(copy);
                showError(errorEl, '');
                render({ id: s.id, act: 'duplicate' });
                announce(t('Duplicated', { name: copy.name }));
            } catch (e) {
                showError(errorEl, e.message);
            } finally {
                holdList(false);
            }
        }

        if (list) {
            list.addEventListener('click', (event) => {
                const button = event.target.closest('[data-act]');
                const row = button && button.closest('[data-id]');
                const s = row && scenarios.find((candidate) => String(candidate.id) === row.dataset.id);
                if (!s || button.disabled) {
                    return;
                }
                const act = button.dataset.act;
                if (act === 'load') {
                    load(s);
                } else if (act === 'compare') {
                    compareScenario(s);
                } else if (act === 'rename') {
                    openDialog('rename', s);
                } else if (act === 'duplicate') {
                    duplicate(s);
                } else if (act === 'delete') {
                    openDialog('delete', s);
                }
            });
        }
        if (opener) {
            opener.addEventListener('click', () => (drawerOpen ? closePanel() : openPanel()));
        }
        if (closeButton) {
            closeButton.addEventListener('click', () => closePanel());
        }
        if (backdrop) {
            backdrop.addEventListener('click', () => closePanel());
        }
        if (saveButton && !unavailable) {
            saveButton.addEventListener('click', () => openDialog('save', null));
        }
        if (form) {
            form.addEventListener('submit', onSubmit);
        }
        if (cancelButton) {
            cancelButton.addEventListener('click', () => dialog.close());
        }
        if (window.matchMedia) {
            const wide = window.matchMedia(WIDE_QUERY);
            const narrowed = () => {
                if (!wide.matches) {
                    closePanel(false);
                }
            };
            if (typeof wide.addEventListener === 'function') {
                wide.addEventListener('change', narrowed);
            }
        }
        render();

        return {
            /**
             * Note whether the plan still matches the loaded scenario.
             */
            update () {
                if (!loaded) {
                    return;
                }
                const now = inputsKey(request(loaded.name, loaded.note));
                if ((now !== loadedKey) !== edited) {
                    edited = now !== loadedKey;
                    showCurrent();
                }
            }
        };
    }

    window.InvestmentScenarios = Object.freeze({ create, MODES, goalModeOf });
})();
