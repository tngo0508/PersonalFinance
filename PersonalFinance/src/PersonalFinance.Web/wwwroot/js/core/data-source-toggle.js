(function () {
    'use strict';

    /**
     * Handles data source button click and posts preference to server.
     * @param {Event} event - The click event.
     * @returns {Promise<void>}
     */
    async function handleSourceClick (event) {
        const button = event.currentTarget;
        if (!button || button.classList.contains('active')) {
            return;
        }

        const container = button.closest('.data-source-toggle');
        if (!container) {
            return;
        }

        const sourceValue = parseInt(button.getAttribute('data-source'), 10);
        if (isNaN(sourceValue)) {
            return;
        }

        const token = window.PFUtils.getAntiForgeryToken(container);
        const buttons = container.querySelectorAll('.data-source-btn');
        buttons.forEach((btn) => {
            btn.disabled = true;
        });

        try {
            const response = await fetch('/BudgetTracker/SetDataSource', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify({ source: sourceValue })
            });

            if (!response.ok) {
                throw new Error('Failed to update data source preference');
            }

            // Reload keeping current URL parameters (same month / period)
            window.location.reload();
        } catch (error) {
            // eslint-disable-next-line no-console
            console.error('Error switching data source:', error);
            buttons.forEach((btn) => {
                btn.disabled = false;
            });
            const existingError = container.querySelector('.data-source-error');
            if (existingError) {
                existingError.remove();
            }
            const errorDiv = document.createElement('div');
            errorDiv.className = 'data-source-error';
            errorDiv.setAttribute('role', 'alert');
            errorDiv.textContent = window.PFI18n.t('Shell.Js.DataSource.SwitchFailed');
            container.appendChild(errorDiv);
            setTimeout(() => {
                if (errorDiv.parentNode) {
                    errorDiv.remove();
                }
            }, 5000);
        }
    }

    /**
     * Initialises all data source toggle components on the page.
     * @returns {void}
     */
    function init () {
        const toggles = document.querySelectorAll('.data-source-toggle');
        toggles.forEach((toggle) => {
            const buttons = toggle.querySelectorAll('.data-source-btn');
            buttons.forEach((btn) => {
                btn.removeEventListener('click', handleSourceClick);
                btn.addEventListener('click', handleSourceClick);
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
