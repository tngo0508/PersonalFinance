/* global PFI18n */
/**
 * Savings Goals page: confirms a delete with the goal's name, and replaces the browser's own "fill in this field"
 * message on the goal name with the page language's. Strings come from the page's <js-strings> island via PFI18n.
 */
(function () {
    'use strict';

    /** Ask before deleting, naming the goal in the translated sentence. */
    function wireDeleteConfirm () {
        document.querySelectorAll('.savings-goal-delete-form').forEach((form) => {
            form.addEventListener('submit', (event) => {
                const message = PFI18n.t('SavingsGoals.Js.ConfirmDelete', { name: form.dataset.goalName || '' });
                if (!window.confirm(message)) {
                    event.preventDefault();
                }
            });
        });
    }

    /** Show the required-name message in the page language rather than the browser's. */
    function wireNameValidation () {
        const input = document.getElementById('goalName');
        if (!input) {
            return;
        }
        input.addEventListener('invalid', () => {
            input.setCustomValidity(input.value.trim() ? '' : PFI18n.t('SavingsGoals.Js.NameRequired'));
        });
        input.addEventListener('input', () => {
            input.setCustomValidity('');
        });
    }

    /** Wire the page once the DOM is ready. */
    function init () {
        wireDeleteConfirm();
        wireNameValidation();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
