/**
 * Money Priorities page: the form with its follow-up fields, the background submit, the result card, the Priority
 * Tree (drawn by window.MoneyPrioritiesDiagram from diagram.js) and Earlier results.
 *
 * The ApiService computes the path, every figure and the "Your path" sentences. This module only shows and hides
 * follow-up fields, sends the answers, and formats what comes back: computed figures in whole US dollars, amounts the
 * user typed with cents, rates with up to two decimals. Rounding happens here, once, when formatting.
 */
(function () {
    'use strict';

    const GENERIC_ERROR = 'Something went wrong. Please try again.';
    const NETWORK_ERROR = 'The service is temporarily unavailable. Please try again.';

    /** The request fields, in form order, with the input that holds each one. */
    const NUMBER_FIELDS = ['MonthlyAmount', 'DebtApr', 'DebtBalance', 'EmergencyFundMonths', 'MonthlyExpenses',
        'EmployerMatchPercent', 'HorizonYears', 'AgeBand'];
    const BOOLEAN_FIELDS = ['HasFullEmployerMatch', 'AtContributionLimit'];

    const SCENARIO_LABELS = {
        Cash: 'High-yield savings',
        Conservative: 'Conservative',
        Base: 'Base',
        Aggressive: 'Aggressive'
    };

    const wholeDollars = new Intl.NumberFormat('en-US',
        { style: 'currency', currency: 'USD', minimumFractionDigits: 0, maximumFractionDigits: 0 });
    const dollarsAndCents = new Intl.NumberFormat('en-US',
        { style: 'currency', currency: 'USD', minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const rateNumber = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 });
    const monthsNumber = new Intl.NumberFormat('en-US', { maximumFractionDigits: 1 });
    const countNumber = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
    const shortDate = new Intl.DateTimeFormat('en-US', { year: 'numeric', month: 'short', day: 'numeric' });

    let form = null;
    let submitButton = null;
    let diagramFrame = null;
    let wholeTreeDiagram = '';
    /** The Priority Tree's thresholds, sent by the server: { highInterestAprPercent, emergencyFundTargetMonths, longHorizonYears }. */
    let thresholds = {};

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

    /** A whole number of months, singular or plural. */
    function formatMonths (value) {
        return value === 1 ? '1 month' : `${countNumber.format(value)} months`;
    }

    /** A UTC timestamp from the server as a short en-US date. */
    function formatDate (value) {
        const text = String(value || '');
        const hasZone = /(?:Z|[+-]\d{2}:\d{2})$/.test(text);
        const date = new Date(hasZone ? text : `${text}Z`);
        return Number.isNaN(date.getTime()) ? '' : shortDate.format(date);
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
                ['Debt balance', formatTyped(debt.balance)],
                ['Paid off in', formatMonths(debt.months)],
                ['Total interest', formatComputed(debt.totalInterest)],
                ['Last payment', formatComputed(debt.finalPayment)]
            ]));
        } else if (debt.outcome === 'AmountDoesNotCoverInterest') {
            nodes.push(note(`${formatTyped(monthlyAmount)} a month does not cover the ${
                formatComputed(debt.firstMonthInterest)} of interest this ${formatTyped(debt.balance)} debt adds in ` +
                'its first month, so the balance would never go down. No payoff figures are shown: paying more ' +
                'each month is the only way to clear it.'));
        } else {
            nodes.push(note(`At ${formatTyped(monthlyAmount)} a month, paying off this ${formatTyped(debt.balance)} ` +
                `debt would take longer than ${countNumber.format(debt.maximumYears)} years, so no payoff ` +
                'figures are shown. Paying more each month shortens it.'));
        }
        nodes.push(note(`Every dollar toward a ${formatRate(debt.aprPercent)} APR debt earns a guaranteed ${
            formatRate(debt.aprPercent)} by not paying that interest, against the ${
            formatRate(debt.assumedReturnPercent)} assumed investment return.`));
        return nodes;
    }

    function emergencyFundFigures (fund) {
        return [
            figureList([
                ['Monthly expenses', formatTyped(fund.monthlyExpenses)],
                ['Your fund covers now', `${monthsNumber.format(fund.monthsCovered)} months`],
                [`Target (${monthsNumber.format(fund.targetMonths)} months of expenses)`, formatComputed(fund.targetAmount)],
                ['Gap to the target', formatComputed(fund.gap)],
                ['Months to close the gap', formatMonths(fund.monthsToClose)]
            ]),
            note('Kept in cash, with no growth assumed.')
        ];
    }

    function employerMatchFigures (match) {
        return [
            figureList([
                ['Employer match', formatRate(match.matchPercent)],
                ['Match you could capture', `Up to ${formatComputed(match.yearlyMatch)} a year`]
            ]),
            note("Your employer's cap may be lower."),
            note(`A ${formatRate(match.matchPercent)} match is an instant ${formatRate(match.matchPercent)} return on ` +
                `what you put in, against the ${formatRate(match.assumedReturnPercent)} assumed investment return.`)
        ];
    }

    function retirementSplitFigures (split) {
        return [
            el('h3', 'mp-subtitle', `Where each year's ${formatComputed(split.yearlyAmount)} goes (${split.taxYear} limits)`),
            figureList([
                [`IRA (limit ${formatComputed(split.iraLimit)})`, formatComputed(split.toIra)],
                [`401(k) (limit ${formatComputed(split.employeeDeferralLimit)})`, formatComputed(split.toEmployeePlan)],
                ['Taxable brokerage account', formatComputed(split.toTaxable)]
            ]),
            el('p', 'mp-footnote', `Roth IRA eligibility depends on your income, which this page does not ask. In ${
                split.taxYear} it phases out between ${formatComputed(split.rothPhaseOutSingleStart)} and ${
                formatComputed(split.rothPhaseOutSingleEnd)} for single filers, and between ${
                formatComputed(split.rothPhaseOutJointStart)} and ${formatComputed(split.rothPhaseOutJointEnd)} ` +
                'for joint filers.')
        ];
    }

    function scenarioFigures (figures) {
        const table = el('table', 'table mp-scenarios');
        table.append(el('caption', null, `Balance after ${figures.horizonYears === 1 ? '1 year' : `${
            countNumber.format(figures.horizonYears)} years`}`));
        const head = el('thead');
        const headRow = el('tr');
        ['Scenario', 'Yearly rate', 'Balance'].forEach((label) => {
            const cell = el('th', null, label);
            cell.scope = 'col';
            headRow.append(cell);
        });
        head.append(headRow);
        const body = el('tbody');
        figures.scenarios.forEach((scenario) => {
            const row = el('tr');
            const name = el('th', null, SCENARIO_LABELS[scenario.scenario] || scenario.scenario);
            name.scope = 'row';
            row.append(name, el('td', null, formatRate(scenario.ratePercent)),
                el('td', 'mp-money', formatComputed(scenario.balance)));
            body.append(row);
        });
        table.append(head, body);
        const nodes = [table];
        if (figures.totalContributed !== null && figures.totalContributed !== undefined) {
            nodes.push(figureList([['Total you put in', formatComputed(figures.totalContributed)]]));
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
            `Assumed yearly returns: conservative ${formatRate(a.conservativeReturnPercent)}, base ${
                formatRate(a.baseReturnPercent)}, aggressive ${formatRate(a.aggressiveReturnPercent)}; cash ${
                formatRate(a.cashYieldPercent)}.`,
            'Contributions are made at the end of each month and compounded monthly.'
        ];
        if (result.limitsTaxYear) {
            items.push(`IRS contribution limits are for the ${result.limitsTaxYear} tax year.`);
        }
        const list = el('ul', 'mp-assumption-list');
        items.forEach((item) => list.append(el('li', null, item)));
        return [el('h3', 'mp-subtitle', 'Assumptions'), list, el('p', 'mp-disclaimer', result.disclaimer)];
    }

    /**
     * Shows a Walk in the result card and the Priority Tree with its path highlighted.
     * @param {Object} result - The PriorityWalkResultDto (camelCase).
     * @returns {Promise<void>} Resolves when the diagram has been drawn (or has failed quietly).
     */
    async function showResult (result) {
        byId('mpEmpty').hidden = true;
        byId('mpResult').hidden = false;
        const headline = byId('mpHeadline');
        headline.textContent = `Put your ${formatTyped(result.monthlyAmount)} a month toward: ${result.headlineLeafLabel}`;
        byId('mpFigures').replaceChildren(...figureNodes(result));
        byId('mpPath').replaceChildren(...(result.pathSentences || []).map((sentence) => el('li', null, sentence)));
        byId('mpAssumptions').replaceChildren(...assumptionNodes(result));
        await showDiagram({ wholeTree: result.wholeTreeDiagram, pathView: result.pathViewDiagram });
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
            return NETWORK_ERROR;
        }
        return outcome.body && typeof outcome.body.error === 'string' ? outcome.body.error : GENERIC_ERROR;
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
                el('span', 'mp-earlier__amount', `${formatTyped(walk.monthlyAmount)} a month`),
                el('span', 'mp-earlier__leaf', walk.leafLabel));
            const actions = el('div', 'mp-earlier__actions');
            const view = el('button', 'btn btn-sm btn-outline-primary mp-earlier__view', 'View');
            view.type = 'button';
            view.setAttribute('aria-label', `View the result from ${date}: ${walk.leafLabel}`);
            view.addEventListener('click', () => viewWalk(walk.walkId));
            const remove = el('button', 'btn btn-sm btn-outline-danger mp-earlier__delete', 'Delete');
            remove.type = 'button';
            remove.setAttribute('aria-label', `Delete the result from ${date}: ${walk.leafLabel}`);
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
            showEarlierError(`Earlier results could not be refreshed. ${errorMessage(outcome)}`);
        }
    }

    /** Refills the form from an earlier Walk and shows its result, figures recomputed by the server. */
    async function viewWalk (walkId) {
        showEarlierError('');
        const outcome = await send(`/MoneyPriorities/Walk/${encodeURIComponent(walkId)}`, 'GET');
        if (!outcome.ok || !outcome.body) {
            showEarlierError(errorMessage(outcome));
            return;
        }
        clearErrors();
        fillForm(outcome.body.answers);
        await showResult(outcome.body);
        const headline = byId('mpHeadline');
        headline.scrollIntoView({ block: 'nearest' });
        headline.focus();
    }

    /** Asks, then removes an earlier Walk and refreshes the list. */
    async function deleteWalk (walkId) {
        if (!window.confirm('Delete this earlier result?')) {
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
