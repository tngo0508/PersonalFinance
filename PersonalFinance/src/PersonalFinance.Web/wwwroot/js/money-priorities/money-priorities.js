/* global PFI18n */
/**
 * Money Priorities page: the form with its follow-up fields, the background submit, the result card, the Priority
 * Tree (drawn by window.MoneyPrioritiesDiagram from diagram.js), the Purpose Note and Earlier results.
 *
 * The ApiService computes the path, every figure and the "Your path" sentences. This module only shows and hides
 * follow-up fields, sends the answers, and formats what comes back: computed figures in whole US dollars, amounts the
 * user typed with cents, rates with up to two decimals. Rounding happens here, once, when formatting. Every word the
 * page writes comes from the MoneyPriorities.Js. strings (PFI18n.t); the leaf labels, disclaimers and messages the
 * server sends are already in the request's Language.
 *
 * The Purpose Note is AI-written plain text: it is asked for in the background after the result shows, never holds
 * up the result or the diagram, and is always rendered with textContent, never as HTML.
 */
(function () {
    'use strict';

    /** The request fields, in form order, with the input that holds each one. */
    const NUMBER_FIELDS = ['MonthlyAmount', 'DebtApr', 'DebtBalance', 'EmergencyFundMonths', 'MonthlyExpenses',
        'EmployerMatchPercent', 'HorizonYears', 'AgeBand'];
    const BOOLEAN_FIELDS = ['HasFullEmployerMatch', 'AtContributionLimit'];

    /** The scenarios the page has a label for (MoneyPriorities.Js.Scenario.<name>). */
    const SCENARIOS = ['Cash', 'Conservative', 'Base', 'Aggressive'];
    const SCENARIO_PREFIX = 'MoneyPriorities.Js.Scenario.';

    // Separators and dates from the page culture, symbol from the currency (PFI18n, spec §2.2).
    const wholeDollars = { format: (value) => PFI18n.formatMoney(value, 'USD', { whole: true }) };
    const dollarsAndCents = { format: (value) => PFI18n.formatMoney(value, 'USD') };
    const rateNumber = new Intl.NumberFormat(PFI18n.culture, { maximumFractionDigits: 2 });
    const monthsNumber = new Intl.NumberFormat(PFI18n.culture, { maximumFractionDigits: 1 });
    const countNumber = new Intl.NumberFormat(PFI18n.culture, { maximumFractionDigits: 0 });

    let form = null;
    let submitButton = null;
    let diagramFrame = null;
    let wholeTreeDiagram = '';
    /** The Priority Tree's thresholds, sent by the server: { highInterestAprPercent, emergencyFundTargetMonths, longHorizonYears }. */
    let thresholds = {};
    /** The AI disclaimer shown under a Purpose Note (the server's reply carries its own; a saved note uses this one). */
    let noteDisclaimer = '';
    /** The server's message for a Purpose Note request that fails before the server answers (network or non-OK). */
    let noteUnavailable = '';
    /** Counts the results shown, so a late Purpose Note reply for a Walk no longer on screen is ignored. */
    let shownResult = 0;

    /** A computed figure, in whole dollars ("$86,542"). */
    function formatComputed (value) {
        return wholeDollars.format(value);
    }

    /** An amount the user typed, with its cents ("$500.00"). */
    function formatTyped (value) {
        return dollarsAndCents.format(value);
    }

    /** A rate with up to two decimals ("22.99%", "7%"). */
    function formatRate (value) {
        return `${rateNumber.format(value)}%`;
    }

    /** A number of months (up to one decimal), singular or plural in the page's Language. */
    function formatMonths (value) {
        return PFI18n.plural('MoneyPriorities.Js.Months', value);
    }

    /** A tax year as written, never grouped ("2026", not "2.026"). */
    function formatYear (value) {
        return String(value);
    }

    /** A UTC timestamp from the server as a short date in the page culture. */
    function formatDate (value) {
        const text = String(value || '');
        const hasZone = /(?:Z|[+-]\d{2}:\d{2})$/.test(text);
        const date = new Date(hasZone ? text : `${text}Z`);
        return Number.isNaN(date.getTime())
            ? ''
            : PFI18n.formatDate(date, { year: 'numeric', month: 'short', day: 'numeric' });
    }

    /** The page's label of a growth scenario; the name as sent when the page has none. */
    function scenarioLabel (name) {
        return SCENARIOS.includes(name) ? PFI18n.t(SCENARIO_PREFIX + name) : name;
    }

    /**
     * Creates an element with an optional class and text.
     * @param {string} tag - Tag name.
     * @param {string} [className] - Class attribute.
     * @param {string} [text] - Text content.
     * @returns {HTMLElement} The element.
     */
    function el (tag, className, text) {
        const node = document.createElement(tag);
        if (className) {
            node.className = className;
        }
        if (text !== undefined && text !== null) {
            node.textContent = text;
        }
        return node;
    }

    function byId (id) {
        return document.getElementById(id);
    }

    function inputFor (field) {
        return form ? form.elements.namedItem(field) : null;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Form
    // ---------------------------------------------------------------------------------------------------------------

    /** The number in a field, or null when it is empty or not a number. */
    function numberValue (field) {
        const input = inputFor(field);
        if (!input || typeof input.value !== 'string') {
            return null;
        }
        const raw = input.value.trim();
        if (raw === '') {
            return null;
        }
        const value = Number(raw);
        return Number.isFinite(value) ? value : null;
    }

    /** The chosen radio of a yes/no question as a boolean, or null when neither is chosen. */
    function booleanValue (field) {
        const checked = form ? form.querySelector(`input[name="${field}"]:checked`) : null;
        if (!checked) {
            return null;
        }
        return checked.value === 'true';
    }

    /**
     * Which follow-up fields the answers so far call for. These are show/hide rules for fields only: the ApiService
     * alone decides the path and ignores answers off it. The thresholds come from the server's Priority Tree.
     * @returns {Object<string, boolean>} Visibility by request field.
     */
    function followUpVisibility () {
        const apr = numberValue('DebtApr');
        const fundMonths = numberValue('EmergencyFundMonths');
        const horizon = numberValue('HorizonYears');
        const longHorizon = horizon !== null && horizon > thresholds.longHorizonYears;
        return {
            DebtBalance: apr !== null && apr >= thresholds.highInterestAprPercent,
            MonthlyExpenses: fundMonths !== null && fundMonths < thresholds.emergencyFundTargetMonths,
            EmployerMatchPercent: booleanValue('HasFullEmployerMatch') === false,
            AgeBand: longHorizon,
            AtContributionLimit: longHorizon
        };
    }

    /** Shows and hides the follow-up fields from the current answers. */
    function applyFieldRules () {
        const visible = followUpVisibility();
        document.querySelectorAll('[data-mp-follow-up]').forEach((field) => {
            field.hidden = !visible[field.getAttribute('data-mp-follow-up')];
        });
    }

    /** Whether a field is on screen (follow-ups can be hidden). */
    function isShown (field) {
        const wrapper = document.querySelector(`[data-mp-follow-up="${field}"]`);
        return !wrapper || !wrapper.hidden;
    }

    /** The answers as the submit request; hidden follow-ups are sent as null. */
    function collectRequest () {
        const request = {};
        NUMBER_FIELDS.forEach((field) => {
            request[field] = isShown(field) ? numberValue(field) : null;
        });
        BOOLEAN_FIELDS.forEach((field) => {
            request[field] = isShown(field) ? booleanValue(field) : null;
        });
        const purpose = inputFor('Purpose');
        const purposeText = purpose ? purpose.value.trim() : '';
        request.Purpose = purposeText === '' ? null : purposeText;
        return request;
    }

    /**
     * Refills the form from a Walk's answers (camelCase, off-path answers null) and reapplies the field rules.
     * @param {Object|null} answers - The stored answers.
     */
    function fillForm (answers) {
        if (!form || !answers) {
            return;
        }
        NUMBER_FIELDS.forEach((field) => {
            const input = inputFor(field);
            const value = answers[camelCase(field)];
            if (input) {
                input.value = value === null || value === undefined ? '' : String(value);
            }
        });
        BOOLEAN_FIELDS.forEach((field) => {
            const value = answers[camelCase(field)];
            form.querySelectorAll(`input[name="${field}"]`).forEach((radio) => {
                radio.checked = value !== null && value !== undefined && radio.value === String(value);
            });
        });
        const purpose = inputFor('Purpose');
        if (purpose) {
            purpose.value = answers.purpose || '';
        }
        applyFieldRules();
    }

    function camelCase (name) {
        return name.charAt(0).toLowerCase() + name.slice(1);
    }

    /** Removes every inline and form-level error. */
    function clearErrors () {
        document.querySelectorAll('[data-mp-error]').forEach((slot) => {
            slot.textContent = '';
            slot.hidden = true;
        });
        if (form) {
            form.querySelectorAll('[aria-invalid="true"]').forEach((input) => {
                input.removeAttribute('aria-invalid');
                input.classList.remove('is-invalid');
            });
        }
        const formError = byId('mpFormError');
        if (formError) {
            formError.textContent = '';
            formError.hidden = true;
        }
    }

    function showFormError (message) {
        const formError = byId('mpFormError');
        if (formError) {
            formError.textContent = message;
            formError.hidden = false;
        }
    }

    /**
     * Shows the server's validation messages under their fields; a message with no visible field goes above the form.
     * @param {{errors: string[], fieldErrors: Object<string, string[]>}} body - The 400 body.
     */
    function showFieldErrors (body) {
        const fieldErrors = body.fieldErrors || {};
        const unplaced = [];
        let firstInvalid = null;
        Object.keys(fieldErrors).forEach((field) => {
            const slot = document.querySelector(`[data-mp-error="${field}"]`);
            const messages = fieldErrors[field] || [];
            if (!slot || !isShown(field)) {
                unplaced.push(...messages);
                return;
            }
            slot.textContent = messages.join(' ');
            slot.hidden = false;
            form.querySelectorAll(`[name="${field}"]`).forEach((input) => {
                input.setAttribute('aria-invalid', 'true');
                input.classList.add('is-invalid');
                firstInvalid = firstInvalid || input;
            });
        });
        if (Object.keys(fieldErrors).length === 0) {
            unplaced.push(...(body.errors || []));
        }
        if (unplaced.length > 0) {
            showFormError(unplaced.join(' '));
        }
        if (firstInvalid) {
            firstInvalid.focus();
        }
    }

    function setBusy (busy) {
        if (!submitButton) {
            return;
        }
        submitButton.disabled = busy;
        submitButton.setAttribute('aria-busy', busy ? 'true' : 'false');
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Result card
    // ---------------------------------------------------------------------------------------------------------------

    /** A list of label/value pairs. */
    function figureList (rows) {
        const list = el('dl', 'mp-figure-list');
        rows.forEach(([label, value]) => {
            const row = el('div', 'mp-figure');
            row.append(el('dt', null, label), el('dd', null, value));
            list.append(row);
        });
        return list;
    }

    function note (text) {
        return el('p', 'mp-note', text);
    }

    function debtFigures (debt, monthlyAmount) {
        const nodes = [];
        if (debt.outcome === 'PaidOff') {
            nodes.push(figureList([
                [PFI18n.t('MoneyPriorities.Js.Debt.Balance'), formatTyped(debt.balance)],
                [PFI18n.t('MoneyPriorities.Js.Debt.PaidOffIn'), formatMonths(debt.months)],
                [PFI18n.t('MoneyPriorities.Js.Debt.TotalInterest'), formatComputed(debt.totalInterest)],
                [PFI18n.t('MoneyPriorities.Js.Debt.LastPayment'), formatComputed(debt.finalPayment)]
            ]));
        } else if (debt.outcome === 'AmountDoesNotCoverInterest') {
            nodes.push(note(PFI18n.t('MoneyPriorities.Js.Debt.DoesNotCoverInterest', {
                amount: formatTyped(monthlyAmount),
                interest: formatComputed(debt.firstMonthInterest),
                balance: formatTyped(debt.balance)
            })));
        } else {
            nodes.push(note(PFI18n.t('MoneyPriorities.Js.Debt.LongerThanMaximum', {
                amount: formatTyped(monthlyAmount),
                balance: formatTyped(debt.balance),
                years: countNumber.format(debt.maximumYears)
            })));
        }
        nodes.push(note(PFI18n.t('MoneyPriorities.Js.Debt.Comparison', {
            apr: formatRate(debt.aprPercent),
            assumed: formatRate(debt.assumedReturnPercent)
        })));
        return nodes;
    }

    function emergencyFundFigures (fund) {
        return [
            figureList([
                [PFI18n.t('MoneyPriorities.Js.Fund.MonthlyExpenses'), formatTyped(fund.monthlyExpenses)],
                [PFI18n.t('MoneyPriorities.Js.Fund.CoversNow'),
                    formatMonths(fund.monthsCovered)],
                [PFI18n.t('MoneyPriorities.Js.Fund.Target', { months: monthsNumber.format(fund.targetMonths) }),
                    formatComputed(fund.targetAmount)],
                [PFI18n.t('MoneyPriorities.Js.Fund.Gap'), formatComputed(fund.gap)],
                [PFI18n.t('MoneyPriorities.Js.Fund.MonthsToClose'), formatMonths(fund.monthsToClose)]
            ]),
            note(PFI18n.t('MoneyPriorities.Js.Fund.NoGrowth'))
        ];
    }

    function employerMatchFigures (match) {
        return [
            figureList([
                [PFI18n.t('MoneyPriorities.Js.Match.Percent'), formatRate(match.matchPercent)],
                [PFI18n.t('MoneyPriorities.Js.Match.Capture'),
                    PFI18n.t('MoneyPriorities.Js.Match.UpToYearly', { amount: formatComputed(match.yearlyMatch) })]
            ]),
            note(PFI18n.t('MoneyPriorities.Js.Match.CapNote')),
            note(PFI18n.t('MoneyPriorities.Js.Match.Comparison', {
                match: formatRate(match.matchPercent),
                assumed: formatRate(match.assumedReturnPercent)
            }))
        ];
    }

    function retirementSplitFigures (split) {
        return [
            el('h3', 'mp-subtitle', PFI18n.t('MoneyPriorities.Js.Split.Heading', {
                amount: formatComputed(split.yearlyAmount),
                year: formatYear(split.taxYear)
            })),
            figureList([
                [PFI18n.t('MoneyPriorities.Js.Split.Ira', { limit: formatComputed(split.iraLimit) }),
                    formatComputed(split.toIra)],
                [PFI18n.t('MoneyPriorities.Js.Split.EmployeePlan', { limit: formatComputed(split.employeeDeferralLimit) }),
                    formatComputed(split.toEmployeePlan)],
                [PFI18n.t('MoneyPriorities.Js.Split.Taxable'), formatComputed(split.toTaxable)]
            ]),
            el('p', 'mp-footnote', PFI18n.t('MoneyPriorities.Js.Split.RothFootnote', {
                year: formatYear(split.taxYear),
                singleStart: formatComputed(split.rothPhaseOutSingleStart),
                singleEnd: formatComputed(split.rothPhaseOutSingleEnd),
                jointStart: formatComputed(split.rothPhaseOutJointStart),
                jointEnd: formatComputed(split.rothPhaseOutJointEnd)
            }))
        ];
    }

    function scenarioFigures (figures) {
        const table = el('table', 'table mp-scenarios');
        table.append(el('caption', null, PFI18n.plural('MoneyPriorities.Js.Scenarios.Caption', figures.horizonYears)));
        const head = el('thead');
        const headRow = el('tr');
        [PFI18n.t('MoneyPriorities.Js.Scenarios.Scenario'), PFI18n.t('MoneyPriorities.Js.Scenarios.Rate'),
            PFI18n.t('MoneyPriorities.Js.Scenarios.Balance')].forEach((label) => {
            const cell = el('th', null, label);
            cell.scope = 'col';
            headRow.append(cell);
        });
        head.append(headRow);
        const body = el('tbody');
        figures.scenarios.forEach((scenario) => {
            const row = el('tr');
            const name = el('th', null, scenarioLabel(scenario.scenario));
            name.scope = 'row';
            row.append(name, el('td', null, formatRate(scenario.ratePercent)),
                el('td', 'mp-money', formatComputed(scenario.balance)));
            body.append(row);
        });
        table.append(head, body);
        const nodes = [table];
        if (figures.totalContributed !== null && figures.totalContributed !== undefined) {
            nodes.push(figureList([
                [PFI18n.t('MoneyPriorities.Js.Scenarios.TotalContributed'), formatComputed(figures.totalContributed)]
            ]));
        }
        return nodes;
    }

    function figureNodes (result) {
        const figures = result.figures || {};
        let nodes = [];
        if (figures.debt) {
            nodes = nodes.concat(debtFigures(figures.debt, result.monthlyAmount));
        }
        if (figures.emergencyFund) {
            nodes = nodes.concat(emergencyFundFigures(figures.emergencyFund));
        }
        if (figures.employerMatch) {
            nodes = nodes.concat(employerMatchFigures(figures.employerMatch));
        }
        if (figures.retirementSplit) {
            nodes = nodes.concat(retirementSplitFigures(figures.retirementSplit));
        }
        if (figures.scenarios && figures.scenarios.length > 0) {
            nodes = nodes.concat(scenarioFigures(figures));
        }
        return nodes;
    }

    function assumptionNodes (result) {
        const a = result.assumptions || {};
        const items = [
            PFI18n.t('MoneyPriorities.Js.Assumptions.Returns', {
                conservative: formatRate(a.conservativeReturnPercent),
                base: formatRate(a.baseReturnPercent),
                aggressive: formatRate(a.aggressiveReturnPercent),
                cash: formatRate(a.cashYieldPercent)
            }),
            PFI18n.t('MoneyPriorities.Js.Assumptions.Compounding')
        ];
        if (result.limitsTaxYear) {
            items.push(PFI18n.t('MoneyPriorities.Js.Assumptions.LimitsYear', { year: formatYear(result.limitsTaxYear) }));
        }
        const list = el('ul', 'mp-assumption-list');
        items.forEach((item) => list.append(el('li', null, item)));
        return [el('h3', 'mp-subtitle', PFI18n.t('MoneyPriorities.Js.Assumptions.Heading')), list, el('p', 'mp-disclaimer', result.disclaimer)];
    }

    /**
     * Shows a Walk in the result card and the Priority Tree with its path highlighted.
     * @param {Object} view - The MoneyPrioritiesResultViewModel (camelCase): the Walk, its path sentences and diagrams.
     * @returns {Promise<void>} Resolves when the diagram has been drawn (or has failed quietly).
     */
    async function showResult (view) {
        const result = view.walk || {};
        byId('mpEmpty').hidden = true;
        byId('mpResult').hidden = false;
        const headline = byId('mpHeadline');
        headline.textContent = PFI18n.t('MoneyPriorities.Js.Headline', {
            amount: formatTyped(result.monthlyAmount),
            leaf: result.headlineLeafLabel
        });
        byId('mpFigures').replaceChildren(...figureNodes(result));
        byId('mpPath').replaceChildren(...(view.pathSentences || []).map((sentence) => el('li', null, sentence)));
        byId('mpAssumptions').replaceChildren(...assumptionNodes(result));
        startPurposeNote(result);
        await showDiagram({ wholeTree: view.wholeTreeDiagram, pathView: view.pathViewDiagram });
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Purpose Note
    // ---------------------------------------------------------------------------------------------------------------

    /**
     * Renders the Purpose Note area in one state. Every text goes in through textContent.
     * @param {string} state - Writing, Written, NoPurpose, Unavailable, QuotaReached or Refused.
     * @param {string} text - The note (Written) or the message to show.
     * @param {string} [disclaimer] - The AI disclaimer, shown under a written note.
     */
    function renderPurposeNote (state, text, disclaimer) {
        const area = byId('mpNote');
        const body = byId('mpNoteBody');
        if (!area || !body) {
            return;
        }
        area.hidden = false;
        area.dataset.state = state;
        body.setAttribute('aria-busy', state === 'Writing' ? 'true' : 'false');
        if (state === 'Writing') {
            const writing = el('p', 'mp-purpose-note__writing');
            const spinner = el('span', 'spinner-border spinner-border-sm');
            spinner.setAttribute('aria-hidden', 'true');
            writing.append(spinner, el('span', null, text));
            body.replaceChildren(writing);
        } else if (state === 'Written') {
            const nodes = [el('p', 'mp-purpose-note__text', text)];
            if (disclaimer) {
                nodes.push(el('p', 'mp-disclaimer', disclaimer));
            }
            body.replaceChildren(...nodes);
        } else {
            body.replaceChildren(el('p', 'mp-purpose-note__message', text));
        }
    }

    /**
     * Shows a Walk's saved Purpose Note at once, or asks the server for it in the background. Not awaited: the result
     * and the diagram never wait on the note.
     * @param {Object} result - The PriorityWalkResultDto (camelCase) just shown.
     */
    function startPurposeNote (result) {
        shownResult += 1;
        if (result.purposeNote) {
            renderPurposeNote('Written', result.purposeNote, noteDisclaimer);
            return;
        }
        renderPurposeNote('Writing', PFI18n.t('MoneyPriorities.Js.NoteWriting'));
        loadPurposeNote(result.walkId, shownResult);
    }

    /**
     * Requests a Walk's Purpose Note and renders its state, unless another result has been shown meanwhile.
     * @param {number} walkId - The Walk on screen when the request started.
     * @param {number} ticket - The value of shownResult when the request started.
     * @returns {Promise<void>} Resolves once the reply is rendered or ignored; never rejects.
     */
    async function loadPurposeNote (walkId, ticket) {
        const outcome = await send(`/MoneyPriorities/Note/${encodeURIComponent(walkId)}`, 'POST');
        if (ticket !== shownResult) {
            return;
        }
        const reply = outcome.ok && outcome.body ? outcome.body : null;
        if (!reply) {
            renderPurposeNote('Unavailable', noteUnavailable);
        } else if (reply.state === 'Written' && reply.note) {
            renderPurposeNote('Written', reply.note, reply.disclaimer || noteDisclaimer);
        } else if (reply.state !== 'Written' && reply.message) {
            renderPurposeNote(reply.state, reply.message);
        } else {
            renderPurposeNote('Unavailable', noteUnavailable);
        }
    }

    /** Draws the Priority Tree; a failure leaves the module's own message, and "Your path" stays the text form. */
    async function showDiagram (texts) {
        const diagram = window.MoneyPrioritiesDiagram;
        if (!diagramFrame || !diagram || typeof diagram.show !== 'function' || !texts.wholeTree) {
            return;
        }
        try {
            await diagram.show(diagramFrame, texts);
        } catch {
            // diagram.js reports its own failures in the frame; nothing to add here.
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Server calls
    // ---------------------------------------------------------------------------------------------------------------

    async function readJson (response) {
        try {
            return await response.json();
        } catch {
            return null;
        }
    }

    /**
     * Sends a request to this page's Web controller (never the ApiService directly).
     * @param {string} url - Web route, e.g. /MoneyPriorities/Submit.
     * @param {string} method - HTTP method.
     * @param {Object} [body] - JSON body.
     * @returns {Promise<{ok: boolean, status: number, body: *}>} The outcome; status 0 when the network failed.
     */
    async function send (url, method, body) {
        const headers = { Accept: 'application/json' };
        if (method !== 'GET') {
            headers.RequestVerificationToken = window.PFUtils ? window.PFUtils.getAntiForgeryToken(form) : '';
        }
        if (body !== undefined) {
            headers['Content-Type'] = 'application/json';
        }
        try {
            const response = await fetch(url, {
                method: method,
                headers: headers,
                credentials: 'same-origin',
                body: body === undefined ? undefined : JSON.stringify(body)
            });
            const content = response.status === 204 ? null : await readJson(response);
            return { ok: response.ok, status: response.status, body: content };
        } catch {
            return { ok: false, status: 0, body: null };
        }
    }

    function errorMessage (outcome) {
        if (outcome.status === 0) {
            return PFI18n.t('MoneyPriorities.Js.NetworkError');
        }
        return outcome.body && typeof outcome.body.error === 'string'
            ? outcome.body.error
            : PFI18n.t('MoneyPriorities.Js.GenericError');
    }

    async function onSubmit (event) {
        event.preventDefault();
        clearErrors();
        setBusy(true);
        const outcome = await send('/MoneyPriorities/Submit', 'POST', collectRequest());
        setBusy(false);
        if (outcome.ok && outcome.body) {
            await showResult(outcome.body);
            byId('mpHeadline').focus();
            await refreshEarlier();
            return;
        }
        if (outcome.status === 400 && outcome.body && (outcome.body.fieldErrors || outcome.body.errors)) {
            showFieldErrors(outcome.body);
            return;
        }
        showFormError(errorMessage(outcome));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Earlier results
    // ---------------------------------------------------------------------------------------------------------------

    function showEarlierError (message) {
        const box = byId('mpEarlierError');
        if (box) {
            box.textContent = message;
            box.hidden = !message;
        }
    }

    /**
     * Renders Earlier results: date, amount and priority, with View and Delete.
     * @param {Array<Object>} walks - PriorityWalkSummaryDto rows, newest first.
     */
    function renderEarlier (walks) {
        const list = byId('mpEarlier');
        const empty = byId('mpEarlierEmpty');
        if (!list || !empty) {
            return;
        }
        empty.hidden = walks.length > 0;
        list.replaceChildren(...walks.map((walk) => {
            const date = formatDate(walk.createdAtUtc);
            const item = el('li', 'mp-earlier__item');
            item.dataset.walkId = String(walk.walkId);
            const details = el('div', 'mp-earlier__details');
            details.append(
                el('span', 'mp-earlier__date', date),
                el('span', 'mp-earlier__amount',
                    PFI18n.t('MoneyPriorities.Js.Earlier.Amount', { amount: formatTyped(walk.monthlyAmount) })),
                el('span', 'mp-earlier__leaf', walk.leafLabel));
            const actions = el('div', 'mp-earlier__actions');
            const view = el('button', 'btn btn-sm btn-outline-primary mp-earlier__view',
                PFI18n.t('MoneyPriorities.Js.Earlier.View'));
            view.type = 'button';
            view.setAttribute('aria-label',
                PFI18n.t('MoneyPriorities.Js.Earlier.ViewAria', { date: date, leaf: walk.leafLabel }));
            view.addEventListener('click', () => viewWalk(walk.walkId));
            const remove = el('button', 'btn btn-sm btn-outline-danger mp-earlier__delete',
                PFI18n.t('MoneyPriorities.Js.Earlier.Delete'));
            remove.type = 'button';
            remove.setAttribute('aria-label',
                PFI18n.t('MoneyPriorities.Js.Earlier.DeleteAria', { date: date, leaf: walk.leafLabel }));
            remove.addEventListener('click', () => deleteWalk(walk.walkId));
            actions.append(view, remove);
            item.append(details, actions);
            return item;
        }));
    }

    async function refreshEarlier () {
        const outcome = await send('/MoneyPriorities/Recent', 'GET');
        if (outcome.ok && Array.isArray(outcome.body)) {
            showEarlierError('');
            renderEarlier(outcome.body);
        } else {
            showEarlierError(PFI18n.t('MoneyPriorities.Js.Earlier.RefreshFailed', { error: errorMessage(outcome) }));
        }
    }

    /** Refills the form from an earlier Walk and shows its result, figures recomputed by the server. */
    async function viewWalk (walkId) {
        showEarlierError('');
        const outcome = await send(`/MoneyPriorities/Walk/${encodeURIComponent(walkId)}`, 'GET');
        if (!outcome.ok || !outcome.body || !outcome.body.walk) {
            showEarlierError(errorMessage(outcome));
            return;
        }
        clearErrors();
        fillForm(outcome.body.walk.answers);
        await showResult(outcome.body);
        const headline = byId('mpHeadline');
        headline.scrollIntoView({ block: 'nearest' });
        headline.focus();
    }

    /** Asks, then removes an earlier Walk and refreshes the list. */
    async function deleteWalk (walkId) {
        if (!window.confirm(PFI18n.t('MoneyPriorities.Js.Earlier.ConfirmDelete'))) {
            return;
        }
        showEarlierError('');
        const outcome = await send(`/MoneyPriorities/Delete/${encodeURIComponent(walkId)}`, 'POST');
        if (!outcome.ok) {
            showEarlierError(errorMessage(outcome));
            return;
        }
        await refreshEarlier();
        const heading = byId('mpEarlierHeading');
        if (heading) {
            heading.focus();
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Start
    // ---------------------------------------------------------------------------------------------------------------

    function readPageData () {
        const island = byId('mpPageData');
        if (!island) {
            return null;
        }
        try {
            return JSON.parse(island.textContent || 'null');
        } catch {
            return null;
        }
    }

    function init () {
        form = byId('mpForm');
        submitButton = byId('mpSubmit');
        diagramFrame = byId('mp-diagram');
        if (!form) {
            return;
        }
        const data = readPageData() || {};
        wholeTreeDiagram = data.wholeTreeDiagram || '';
        thresholds = data.thresholds || {};
        noteDisclaimer = data.purposeNoteDisclaimer || '';
        noteUnavailable = data.purposeNoteUnavailable || '';

        form.addEventListener('input', applyFieldRules);
        form.addEventListener('change', applyFieldRules);
        form.addEventListener('submit', onSubmit);

        fillForm(data.latestAnswers || null);
        applyFieldRules();
        renderEarlier(Array.isArray(data.recentWalks) ? data.recentWalks : []);
        showDiagram({ wholeTree: wholeTreeDiagram, pathView: null });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
