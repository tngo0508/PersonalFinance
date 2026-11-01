/* global PFI18n */
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
    function init () {
        const buttons = document.querySelectorAll('.news-summarize-btn');
        buttons.forEach((btn) => {
            btn.addEventListener('click', handleSummarize);
        });
    }

    /**
     * Handle the summarize button click event.
     * @param {Event} event - The click event.
     * @returns {void}
     */
    async function handleSummarize (event) {
        const btn = /** @type {HTMLButtonElement} */ (event.currentTarget);
        const articleUrl = btn.dataset.articleUrl;
        const articleTitle = btn.dataset.articleTitle;
        const articleDescription = btn.dataset.articleDescription;

        if (!articleUrl) {
            return;
        }

        // The summary panel the button controls
        const summaryContainer = document.getElementById(btn.getAttribute('aria-controls'));
        const summaryContent = summaryContainer ? summaryContainer.querySelector('.news-summary-content') : null;
        const label = btn.querySelector('.news-summarize-label') || btn;

        // A second click closes an open summary
        if (summaryContainer && !summaryContainer.hidden && !inFlight.has(articleUrl)) {
            summaryContainer.hidden = true;
            btn.setAttribute('aria-expanded', 'false');
            return;
        }
        btn.setAttribute('aria-expanded', 'true');

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
        const originalText = label.textContent;
        label.textContent = PFI18n.t('News.Js.Summary.Loading');
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
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': window.PFUtils.getAntiForgeryToken()
                },
                body: JSON.stringify({
                    title: articleTitle,
                    description: articleDescription
                })
            });

            if (response.ok) {
                const data = await response.json();
                const summary = [data.summary || PFI18n.t('News.Js.Summary.None'), data.budgetWarning, data.disclaimer].filter(Boolean).join('\n\n');

                // Cache the result
                summaryCache.set(articleUrl, summary);

                if (summaryContent) {
                    summaryContent.textContent = summary;
                }
                if (summaryContainer) {
                    summaryContainer.hidden = false;
                }
            } else if (response.status === 429) {
                const error = await response.json();
                if (summaryContent) {
                    summaryContent.textContent = error.error || PFI18n.t('News.Js.Summary.LimitReached');
                }
                showError(btn, PFI18n.t('News.Js.Summary.RateLimited'));
            } else {
                if (summaryContent) {
                    summaryContent.textContent = PFI18n.t('News.Js.Summary.Failed');
                }
                showError(btn, PFI18n.t('News.Js.Summary.FailedTitle'));
            }
        } catch (error) {
            if (summaryContent) {
                summaryContent.textContent = PFI18n.t('News.Js.Summary.NetworkError');
            }
            showError(btn, PFI18n.t('News.Js.Summary.NetworkErrorTitle'));
        } finally {
            inFlight.delete(articleUrl);
            btn.disabled = false;
            label.textContent = originalText;
            btn.removeAttribute('aria-busy');
        }
    }

    /**
     * Show an error state on the button with retry capability.
     * @param {HTMLButtonElement} btn - The button element.
     * @param {string} message - The error message.
     * @returns {void}
     */
    function showError (btn, message) {
        const label = btn.querySelector('.news-summarize-label') || btn;
        btn.disabled = false;
        label.textContent = PFI18n.t('News.Js.Summary.Retry');
        btn.classList.add('news-summarize-error');
        btn.title = message;

        // Remove error state after 3 seconds
        setTimeout(() => {
            btn.classList.remove('news-summarize-error');
            label.textContent = PFI18n.t('News.Js.Summary.Button');
            btn.title = PFI18n.t('News.Js.Summary.Title');
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
