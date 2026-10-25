/* global PFI18n */
/**
 * PROTOTYPE (#340, throwaway). Savings Goals client strings: a summary line built from plurals and named
 * placeholders, the delete confirmation, and a client-side required-name message. Reads strings through PFI18n.
 */
(function () {
    'use strict';

    /**
     * Format a number as whole dollars (currency still USD-only in this prototype).
     * @param {number} value - Amount.
     * @returns {string} e.g. "$1,200".
     */
    function dollars (value) {
        return `$${ Math.round(value).toLocaleString('en-US')}`;
    }

    /** Render the summary line from the cards on the page. */
    function renderSummary () {
        const summary = document.getElementById('savingsSummary');
        if (!summary) {
            return;
        }
        const cards = Array.prototype.slice.call(document.querySelectorAll('.savings-goal-card'));
        const reached = cards.filter((card) => { return card.dataset.complete === 'true'; }).length;
        const remaining = cards.reduce((sum, card) => { return sum + (parseFloat(card.dataset.remaining) || 0); }, 0);
        const goals = PFI18n.plural('SavingsGoals.Js.Summary.Goals', cards.length);
        summary.textContent = `${goals } · ${
            PFI18n.plural('SavingsGoals.Js.Summary.Reached', reached) } · ${
            PFI18n.t('SavingsGoals.Js.Summary.ToSave', { amount: dollars(remaining), goals: goals })}`;
    }

    /** Confirm deletes with the goal's name in the translated sentence. */
    function wireDeleteConfirm () {
        document.querySelectorAll('.savings-goal-delete-form').forEach((form) => {
            form.addEventListener('submit', (event) => {
                if (!window.confirm(PFI18n.t('SavingsGoals.Js.ConfirmDelete', { name: form.dataset.goalName || '' }))) {
                    event.preventDefault();
                }
            });
        });
    }

    /** Replace the browser's built-in required message on the goal name with ours. */
    function wireNameValidation () {
        const input = document.getElementById('goalName');
        if (!input) {
            return;
        }
        input.addEventListener('invalid', () => {
            input.setCustomValidity(input.value.trim() ? '' : PFI18n.t('SavingsGoals.Js.NameRequired'));
        });
        input.addEventListener('input', () => { input.setCustomValidity(''); });
    }

    /** Show where the JS strings came from on the prototype panel. */
    function reportSource () {
        const status = document.getElementById('i18nJsStatus');
        if (status) {
            status.textContent = `(${ PFI18n.size() } keys from ${ PFI18n.source() })`;
        }
    }

    document.addEventListener('DOMContentLoaded', async () => {
        wireDeleteConfirm();
        await PFI18n.ready;
        renderSummary();
        wireNameValidation();
        reportSource();
    });
})();
