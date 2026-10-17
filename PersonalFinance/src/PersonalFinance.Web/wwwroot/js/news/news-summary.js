/**
 * News Article Summary Module
 * Handles AI-powered article summarization with session caching,
 * loading states, error handling, and rate limit feedback.
 */
(function () {
    'use strict';

    /**
     * Session cache for summaries keyed by article URL.
     * @type {Map<string, string>}
     */
    const summaryCache = new Map();

    /**
     * Set of article URLs currently being summarized to prevent duplicate requests.
     * @type {Set<string>}
     */
    const inFlight = new Set();

    /**
     * Initialize the summary module by attaching click handlers to all summarize buttons.
     * @returns {void}
     */
    function init() {
        const buttons = document.querySelectorAll('.finance-hub-summarize-btn');
        buttons.forEach(function (btn) {
            btn.addEventListener('click', handleSummarize);
        });
    }

    /**
     * Handle the summarize button click event.
     * @param {Event} event - The click event.
     * @returns {void}
     */
    async function handleSummarize(event) {
        const btn = /** @type {HTMLButtonElement} */ (event.currentTarget);
        const articleUrl = btn.dataset.articleUrl;
        const articleTitle = btn.dataset.articleTitle;
        const articleDescription = btn.dataset.articleDescription;

        if (!articleUrl) {
            return;
        }

        // Find the associated summary container
        const card = btn.closest('.finance-hub-card');
        if (!card) {
            return;
        }

        const summaryContainer = card.querySelector('.finance-hub-card-summary');
        const summaryContent = summaryContainer ? summaryContainer.querySelector('.finance-hub-summary-content') : null;

        // Check cache first
        if (summaryCache.has(articleUrl)) {
            if (summaryContent) {
                summaryContent.textContent = summaryCache.get(articleUrl);
            }
            if (summaryContainer) {
                summaryContainer.hidden = false;
            }
            return;
        }

        // Prevent duplicate requests
        if (inFlight.has(articleUrl)) {
            return;
        }

        // Set loading state
        btn.disabled = true;
        const originalText = btn.textContent;
        btn.textContent = 'Loading...';
        btn.setAttribute('aria-busy', 'true');

        if (summaryContent) {
            summaryContent.textContent = '';
        }
        if (summaryContainer) {
            summaryContainer.hidden = false;
        }

        inFlight.add(articleUrl);

        try {
            const response = await fetch('/api/AiAssistant/summarize', {
                method: 'POST',
                headers: {
                    'Accept': 'application/json',
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    title: articleTitle,
                    description: articleDescription
                })
            });

            if (response.ok) {
                const data = await response.json();
                const summary = data.summary || 'No summary available.';

                // Cache the result
                summaryCache.set(articleUrl, summary);

                if (summaryContent) {
                    summaryContent.textContent = summary;
                }
                if (summaryContainer) {
                    summaryContainer.hidden = false;
                }
            } else if (response.status === 429) {
                if (summaryContent) {
                    summaryContent.textContent = 'Too many requests. Please wait a moment and try again.';
                }
                showError(btn, 'Rate limited. Try again later.');
            } else {
                if (summaryContent) {
                    summaryContent.textContent = 'Unable to generate summary. Please try again.';
                }
                showError(btn, 'Failed to summarize. Please retry.');
            }
        } catch (error) {
            if (summaryContent) {
                summaryContent.textContent = 'Network error. Please try again.';
            }
            showError(btn, 'Network error. Please retry.');
        } finally {
            inFlight.delete(articleUrl);
            btn.disabled = false;
            btn.textContent = originalText;
            btn.removeAttribute('aria-busy');
        }
    }

    /**
     * Show an error state on the button with retry capability.
     * @param {HTMLButtonElement} btn - The button element.
     * @param {string} message - The error message.
     * @returns {void}
     */
    function showError(btn, message) {
        btn.disabled = false;
        btn.textContent = 'Retry';
        btn.classList.add('finance-hub-summarize-error');
        btn.title = message;

        // Remove error state after 3 seconds
        setTimeout(function () {
            btn.classList.remove('finance-hub-summarize-error');
            btn.textContent = 'Summarize';
            btn.title = 'Summarize with AI';
        }, 3000);
    }

    // Expose public API
    window.NewsSummaryModule = {
        init: init
    };

    // Auto-initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
