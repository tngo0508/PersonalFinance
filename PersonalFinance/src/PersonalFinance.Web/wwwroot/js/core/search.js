(function () {
    'use strict';

    /**
     * Search module for the top bar.
     * Handles debounced search input and displays results from the API.
     */
    window.SearchModule = (function () {
        let debounceTimer;
        let searchInput;
        let resultsDropdown;

        /**
         * Initialize the search module.
         * @returns {void}
         */
        function init() {
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', setup);
            } else {
                setup();
            }
        }

        /**
         * Set up search event listeners.
         * @returns {void}
         */
        function setup() {
            searchInput = document.getElementById('topbarSearchInput');
            resultsDropdown = document.getElementById('searchResultsDropdown');

            if (!searchInput || !resultsDropdown) {
                return;
            }

            searchInput.addEventListener('input', function () {
                clearTimeout(debounceTimer);
                const query = searchInput.value.trim();

                if (query.length < 2) {
                    hideResults();
                    return;
                }

                debounceTimer = setTimeout(function () {
                    performSearch(query);
                }, 300);
            });

            // Close dropdown on click outside
            document.addEventListener('click', function (e) {
                if (searchInput && !searchInput.contains(e.target) &&
                    resultsDropdown && !resultsDropdown.contains(e.target)) {
                    hideResults();
                }
            });
        }

        /**
         * Perform search via fetch API.
         * @param {string} query - The search query.
         * @returns {void}
         */
        function performSearch(query) {
            fetch('/api/Search?q=' + encodeURIComponent(query))
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error('Search failed');
                    }
                    return response.json();
                })
                .then(function (data) {
                    displayResults(data);
                })
                .catch(function () {
                    // Silently fail - search is a nice-to-have feature
                });
        }

        /**
         * Display search results in the dropdown.
         * @param {Object} data - Search results with Transactions, BudgetItems, SavingsGoals.
         * @returns {void}
         */
        function displayResults(data) {
            if (!resultsDropdown) {
                return;
            }

            let hasResults = false;
            let html = '';

            if (data.transactions && data.transactions.length > 0) {
                hasResults = true;
                html += '<div class="search-results-source">Transactions</div>';
                data.transactions.forEach(function (item) {
                    html += '<a href="' + item.url + '" class="search-result-item">' +
                        '<div class="search-result-title">' + escapeHtml(item.title) + '</div>' +
                        '<div class="search-result-desc">' + escapeHtml(item.description) + '</div>' +
                        '</a>';
                });
            }

            if (data.budgetItems && data.budgetItems.length > 0) {
                hasResults = true;
                html += '<div class="search-results-source">Budgets</div>';
                data.budgetItems.forEach(function (item) {
                    html += '<a href="' + item.url + '" class="search-result-item">' +
                        '<div class="search-result-title">' + escapeHtml(item.title) + '</div>' +
                        '<div class="search-result-desc">' + escapeHtml(item.description) + '</div>' +
                        '</a>';
                });
            }

            if (data.savingsGoals && data.savingsGoals.length > 0) {
                hasResults = true;
                html += '<div class="search-results-source">Savings Goals</div>';
                data.savingsGoals.forEach(function (item) {
                    html += '<a href="' + item.url + '" class="search-result-item">' +
                        '<div class="search-result-title">' + escapeHtml(item.title) + '</div>' +
                        '<div class="search-result-desc">' + escapeHtml(item.description) + '</div>' +
                        '</a>';
                });
            }

            if (!hasResults) {
                html = '<div class="search-empty">No results found</div>';
            }

            resultsDropdown.innerHTML = html;
            resultsDropdown.classList.add('show');
        }

        /**
         * Hide the search results dropdown.
         * @returns {void}
         */
        function hideResults() {
            if (resultsDropdown) {
                resultsDropdown.classList.remove('show');
                resultsDropdown.innerHTML = '';
            }
        }

        /**
         * Escape HTML to prevent XSS.
         * @param {string} text - The text to escape.
         * @returns {string} Escaped text.
         */
        function escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }

        return {
            init: init
        };
    })();

    // Auto-initialize
    window.SearchModule.init();

})();
