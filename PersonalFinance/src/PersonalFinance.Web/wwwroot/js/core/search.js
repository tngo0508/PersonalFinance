(function () {
    'use strict';

    /**
     * Global Search in the top bar.
     * A combobox: typing searches the signed-in user's records, arrow keys move through the results, Enter opens
     * the highlighted one and Esc closes the list. A failed search shows the Unavailable State with a retry,
     * never "no results".
     */
    window.SearchModule = (function () {
        // Matches SearchService.MinQueryLength on the server.
        const minQueryLength = 2;
        const debounceMs = 300;
        // Group headings are Shell.Js.Search.Group.* strings from the layout's island.
        const groups = [
            { key: 'transactions', labelKey: 'Shell.Js.Search.Group.Transactions' },
            { key: 'categories', labelKey: 'Shell.Js.Search.Group.Categories' },
            { key: 'savingsGoals', labelKey: 'Shell.Js.Search.Group.SavingsGoals' }
        ];

        let input;
        let dropdown;
        let list;
        let message;
        let messageText;
        let retryButton;
        let status;
        let debounceTimer;
        let currentRequest;
        let lastQuery = '';
        let options = [];
        let activeIndex = -1;

        /**
         * Initialize the search module once the DOM is ready.
         * @returns {void}
         */
        function init () {
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', setup);
            } else {
                setup();
            }
        }

        /**
         * Find the search elements and wire up the event listeners.
         * @returns {void}
         */
        function setup () {
            input = document.getElementById('topbarSearchInput');
            dropdown = document.getElementById('searchResultsDropdown');
            list = document.getElementById('searchResultsList');
            message = document.getElementById('searchMessage');
            messageText = document.getElementById('searchMessageText');
            retryButton = document.getElementById('searchRetryButton');
            status = document.getElementById('searchStatus');

            if (!input || !dropdown || !list || !message || !messageText || !retryButton || !status) {
                return;
            }

            input.addEventListener('input', onInput);
            input.addEventListener('keydown', onKeyDown);
            retryButton.addEventListener('click', () => {
                search(lastQuery);
                input.focus();
            });

            document.addEventListener('click', (e) => {
                if (!input.contains(e.target) && !dropdown.contains(e.target)) {
                    close();
                }
            });
        }

        /**
         * Debounce typing and search once the query is long enough.
         * @returns {void}
         */
        function onInput () {
            clearTimeout(debounceTimer);
            const query = input.value.trim();

            if (query.length < minQueryLength) {
                cancelRequest();
                close();
                return;
            }

            debounceTimer = setTimeout(() => {
                search(query);
            }, debounceMs);
        }

        /**
         * Keyboard support: Up/Down move the highlight, Enter opens it, Esc closes the list.
         * @param {KeyboardEvent} e - The keydown event.
         * @returns {void}
         */
        function onKeyDown (e) {
            if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
                if (options.length === 0) {
                    return;
                }

                e.preventDefault();
                open();
                const step = e.key === 'ArrowDown' ? 1 : -1;
                setActive((activeIndex + step + options.length) % options.length);
            } else if (e.key === 'Enter') {
                if (!dropdown.hidden && activeIndex >= 0) {
                    e.preventDefault();
                    window.location.assign(options[activeIndex].getAttribute('href'));
                }
            } else if (e.key === 'Escape') {
                if (!dropdown.hidden) {
                    e.preventDefault();
                    close();
                }
            }
        }

        /**
         * Search the API and show the results, the empty message or the Unavailable State.
         * Only the latest query's response is shown; earlier requests are cancelled.
         * @param {string} query - The trimmed search text.
         * @returns {Promise<void>}
         */
        async function search (query) {
            cancelRequest();
            lastQuery = query;
            const request = new AbortController();
            currentRequest = request;

            try {
                const response = await fetch(`/api/Search?q=${encodeURIComponent(query)}`, {
                    headers: { Accept: 'application/json' },
                    signal: request.signal
                });

                if (!response.ok) {
                    throw new Error(`Search failed with status ${response.status}`);
                }

                const data = await response.json();
                if (request === currentRequest) {
                    showResults(data, query);
                }
            } catch (error) {
                if (error.name === 'AbortError' || request !== currentRequest) {
                    return;
                }

                showUnavailable();
            }
        }

        /**
         * Abort the in-flight search request, if any.
         * @returns {void}
         */
        function cancelRequest () {
            if (currentRequest) {
                currentRequest.abort();
                currentRequest = null;
            }
        }

        /**
         * Render the grouped results as listbox options.
         * @param {Object} data - Search results keyed by group (transactions, categories, savingsGoals).
         * @param {string} query - The query the results are for.
         * @returns {void}
         */
        function showResults (data, query) {
            clearList();

            groups.forEach((group) => {
                const items = (data && Array.isArray(data[group.key])) ? data[group.key] : [];
                const safeItems = items.filter((item) => isSameSiteUrl(item.url));

                if (safeItems.length === 0) {
                    return;
                }

                const groupEl = document.createElement('div');
                groupEl.setAttribute('role', 'group');
                const headingId = `searchGroup-${group.key}`;
                groupEl.setAttribute('aria-labelledby', headingId);

                const heading = document.createElement('div');
                heading.id = headingId;
                heading.className = 'search-results-source';
                heading.textContent = window.PFI18n.t(group.labelKey);
                groupEl.appendChild(heading);

                safeItems.forEach((item) => {
                    groupEl.appendChild(createOption(item, options.length));
                });

                list.appendChild(groupEl);
            });

            if (options.length === 0) {
                showMessage(window.PFI18n.t('Shell.Js.Search.NoResults', { query }), false);
                return;
            }

            message.hidden = true;
            announce(window.PFI18n.plural('Shell.Js.Search.Results', options.length));
            open();
        }

        /**
         * Build one result option. All text goes in through textContent.
         * @param {Object} item - A search result with title, description and url.
         * @param {number} index - The option's position across all groups.
         * @returns {HTMLAnchorElement} The option element.
         */
        function createOption (item, index) {
            const option = document.createElement('a');
            option.id = `searchOption-${index}`;
            option.className = 'search-result-item';
            option.setAttribute('role', 'option');
            option.setAttribute('aria-selected', 'false');
            option.setAttribute('href', item.url);
            option.tabIndex = -1;

            const title = document.createElement('div');
            title.className = 'search-result-title';
            title.textContent = item.title || '';
            option.appendChild(title);

            if (item.description) {
                const description = document.createElement('div');
                description.className = 'search-result-desc';
                description.textContent = item.description;
                option.appendChild(description);
            }

            option.addEventListener('mousemove', () => {
                setActive(index);
            });

            options.push(option);
            return option;
        }

        /**
         * Show the Unavailable State: the search failed, which is not the same as having no results.
         * @returns {void}
         */
        function showUnavailable () {
            clearList();
            showMessage(window.PFI18n.t('Shell.Js.Search.Unavailable'), true);
        }

        /**
         * Show a message in place of results.
         * @param {string} text - The message.
         * @param {boolean} canRetry - Whether to offer the retry button.
         * @returns {void}
         */
        function showMessage (text, canRetry) {
            messageText.textContent = text;
            retryButton.hidden = !canRetry;
            message.hidden = false;
            announce(text);
            open();
        }

        /**
         * Highlight one option and tell assistive technology about it.
         * @param {number} index - The option to highlight.
         * @returns {void}
         */
        function setActive (index) {
            clearActive();
            activeIndex = index;
            const option = options[index];
            option.classList.add('is-active');
            option.setAttribute('aria-selected', 'true');
            option.scrollIntoView({ block: 'nearest' });
            input.setAttribute('aria-activedescendant', option.id);
        }

        /**
         * Remove the highlight from the active option, if any.
         * @returns {void}
         */
        function clearActive () {
            const option = options[activeIndex];
            if (option) {
                option.classList.remove('is-active');
                option.setAttribute('aria-selected', 'false');
            }

            activeIndex = -1;
            input.removeAttribute('aria-activedescendant');
        }

        /**
         * Remove all options and the highlight.
         * @returns {void}
         */
        function clearList () {
            clearActive();
            list.replaceChildren();
            options = [];
        }

        /**
         * Show the dropdown.
         * @returns {void}
         */
        function open () {
            dropdown.hidden = false;
            input.setAttribute('aria-expanded', 'true');
        }

        /**
         * Hide the dropdown, keeping the typed text.
         * @returns {void}
         */
        function close () {
            dropdown.hidden = true;
            input.setAttribute('aria-expanded', 'false');
            clearActive();
        }

        /**
         * Announce a status to screen readers through the polite live region.
         * @param {string} text - What to announce.
         * @returns {void}
         */
        function announce (text) {
            status.textContent = text;
        }

        /**
         * Only same-site paths are rendered as links ("/x", never "//host" or "javascript:").
         * @param {string} url - The result URL from the API.
         * @returns {boolean} Whether the URL is a same-site path.
         */
        function isSameSiteUrl (url) {
            return typeof url === 'string' && url.startsWith('/') && !url.startsWith('//') &&
                !url.startsWith('/\\');
        }

        return {
            init: init
        };
    })();

    window.SearchModule.init();
})();
