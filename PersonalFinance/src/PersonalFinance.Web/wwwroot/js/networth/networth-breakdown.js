/* global PFI18n */
(function () {
    'use strict';

    /**
     * Create an element with a class and optional text.
     * @param {string} tag - The tag name.
     * @param {string} className - The class attribute.
     * @param {string} [text] - Text content, set with textContent.
     * @returns {HTMLElement} The new element.
     */
    function el (tag, className, text) {
        const node = document.createElement(tag);
        node.className = className;
        if (text !== undefined) {
            node.textContent = text;
        }
        return node;
    }

    /**
     * Build one FI Breakdown card, mirroring Views/Networth/_FiBreakdown.cshtml.
     * @param {Object} card - A card from the Calculate response (key, label, value, inputs, formula, scenarios, note).
     * @returns {HTMLElement} The card element.
     */
    function buildCard (card) {
        const labelId = `nwBreakdown-${card.key}-label`;
        const wrapper = el('div', 'card nw-breakdown__card');
        wrapper.setAttribute('role', 'group');
        wrapper.setAttribute('aria-labelledby', labelId);

        const body = el('div', 'card-body');
        const label = el('div', 'nw-breakdown__label', card.label);
        label.id = labelId;
        body.append(label, el('div', 'nw-breakdown__value', card.value));

        const inputs = el('div', 'nw-breakdown__inputs');
        inputs.append(el('span', 'nw-breakdown__from', PFI18n.t('NetWorth.Js.Breakdown.From')));
        (card.inputs || []).forEach((input) => {
            const chip = el('span', 'nw-breakdown__chip', input.label);
            chip.title = input.value;
            inputs.append(chip);
        });
        body.append(inputs);

        if (card.formula) {
            body.append(el('p', 'nw-breakdown__formula', card.formula));
        }
        if (card.scenarios && card.scenarios.length > 0) {
            const list = el('ul', 'nw-breakdown__scenarios');
            card.scenarios.forEach((scenario) => list.append(el('li', '', scenario)));
            body.append(list);
        }
        if (card.note) {
            body.append(el('p', 'nw-breakdown__note', card.note));
        }

        wrapper.append(body);
        return wrapper;
    }

    /**
     * Re-render the FI Breakdown from a recalculated projection.
     * @param {Object} data - The Calculate response; its breakdown property holds the cards.
     * @returns {void}
     */
    function updateBreakdown (data) {
        const grid = document.getElementById('nwBreakdownGrid');
        if (!grid || !data || !data.breakdown || !Array.isArray(data.breakdown.cards)) {
            return;
        }

        grid.replaceChildren(...data.breakdown.cards.map(buildCard));
    }

    window.NW = Object.assign(window.NW || {}, {
        updateBreakdown: updateBreakdown
    });
})();
