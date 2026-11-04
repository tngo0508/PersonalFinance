/* global PFI18n */
/**
 * News Article Summary Module: the inline briefing under an article.
 * Asks ApiService for the AI summary of an article and renders it as a TL;DR, key points and a why-it-matters
 * line, with the coverage the summary was written from (full article, feed excerpt or headline only). Summaries are
 * cached for the page's lifetime; loading, errors and rate limits show in the panel and on the button.
 */
(function () {
    'use strict';

    const SVG_NS = 'http://www.w3.org/2000/svg';

    /**
     * Session cache for summary responses keyed by article URL.
     * @type {Map<string, Object>}
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
     * Create an element with a class and optional text.
     * @param {string} tag - The element name.
     * @param {string} className - The class attribute.
     * @param {string} [text] - Text content; never markup.
     * @returns {HTMLElement} The element.
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
     * Remove every child of a node.
     * @param {Element} node - The node to empty.
     * @returns {void}
     */
    function clear (node) {
        while (node.firstChild) {
            node.removeChild(node.firstChild);
        }
    }

    /**
     * The coin icon of the why-it-matters line.
     * @returns {SVGElement} The icon, hidden from assistive technology.
     */
    function coinIcon () {
        const svg = document.createElementNS(SVG_NS, 'svg');
        svg.setAttribute('width', '16');
        svg.setAttribute('height', '16');
        svg.setAttribute('viewBox', '0 0 24 24');
        svg.setAttribute('fill', 'none');
        svg.setAttribute('stroke', 'currentColor');
        svg.setAttribute('stroke-width', '2');
        svg.setAttribute('aria-hidden', 'true');
        const circle = document.createElementNS(SVG_NS, 'circle');
        circle.setAttribute('cx', '12');
        circle.setAttribute('cy', '12');
        circle.setAttribute('r', '9');
        const path = document.createElementNS(SVG_NS, 'path');
        path.setAttribute('d', 'M14.5 9.5a2.5 2.5 0 0 0-5 0c0 2.5 5 2.5 5 5a2.5 2.5 0 0 1-5 0M12 6v1.5M12 16.5V18');
        svg.appendChild(circle);
        svg.appendChild(path);
        return svg;
    }

    /**
     * Show the loading skeleton in a panel.
     * @param {Element} content - The panel's content element.
     * @returns {void}
     */
    function renderSkeleton (content) {
        clear(content);
        const skeleton = el('div', 'news-summary-skeleton');
        skeleton.setAttribute('aria-busy', 'true');
        skeleton.setAttribute('aria-label', PFI18n.t('News.Js.Summary.Loading'));
        for (let i = 0; i < 4; i++) {
            skeleton.appendChild(el('span', ''));
        }
        content.appendChild(skeleton);
    }

    /**
     * Show a plain message in a panel (an error, a rate limit, a fixed server message).
     * @param {Element} content - The panel's content element.
     * @param {string} message - The message.
     * @returns {void}
     */
    function renderMessage (content, message) {
        clear(content);
        content.appendChild(el('p', 'news-summary-text', message));
    }

    /**
     * Render a summary response: the TL;DR, the key points, why it matters, then the coverage and model line. A
     * response without a TL;DR (a fixed server message) shows as plain text.
     * @param {Element} content - The panel's content element.
     * @param {Object} data - The summary response.
     * @param {string} articleUrl - The article's link, for the footer.
     * @returns {void}
     */
    function renderSummary (content, data, articleUrl) {
        clear(content);
        if (!data.tldr) {
            content.appendChild(el('p', 'news-summary-text', data.summary || PFI18n.t('News.Js.Summary.None')));
            appendNotes(content, data);
            return;
        }

        const tldr = el('div', 'news-summary-section');
        tldr.appendChild(el('span', 'news-summary-label', PFI18n.t('News.Js.Summary.Tldr')));
        tldr.appendChild(el('p', 'news-summary-tldr', data.tldr));
        content.appendChild(tldr);

        const points = Array.isArray(data.keyPoints) ? data.keyPoints : [];
        if (points.length > 0) {
            const section = el('div', 'news-summary-section');
            section.appendChild(el('span', 'news-summary-label', PFI18n.t('News.Js.Summary.KeyPoints')));
            const list = el('ul', 'news-summary-points');
            points.forEach((point) => list.appendChild(el('li', '', point)));
            section.appendChild(list);
            content.appendChild(section);
        }

        if (data.whyItMatters) {
            const why = el('p', 'news-summary-why');
            why.appendChild(coinIcon());
            const body = el('span', '');
            body.appendChild(el('b', '', PFI18n.t('News.Js.Summary.WhyItMatters')));
            body.appendChild(document.createTextNode(data.whyItMatters));
            why.appendChild(body);
            content.appendChild(why);
        }

        const foot = el('p', 'news-summary-foot');
        if (data.coverage) {
            const chip = el('span', 'news-summary-coverage', PFI18n.t(`News.Js.Summary.Coverage.${data.coverage}`));
            chip.dataset.coverage = data.coverage;
            foot.appendChild(chip);
            foot.appendChild(el('span', '', PFI18n.t(`News.Js.Summary.CoverageText.${data.coverage}`)));
        }
        foot.appendChild(el('span', '', PFI18n.t('News.Js.Summary.Model', { model: data.model || 'AI' })));
        if (articleUrl) {
            const link = el('a', '', PFI18n.t('News.Js.Summary.ReadArticle'));
            link.href = articleUrl;
            link.target = '_blank';
            link.rel = 'noopener noreferrer';
            foot.appendChild(link);
        }
        content.appendChild(foot);
        appendNotes(content, data);
    }

    /**
     * Append the budget warning and the AI disclaimer the server sent, when it did.
     * @param {Element} content - The panel's content element.
     * @param {Object} data - The summary response.
     * @returns {void}
     */
    function appendNotes (content, data) {
        if (data.budgetWarning) {
            content.appendChild(el('p', 'news-summary-note', data.budgetWarning));
        }
        if (data.disclaimer) {
            content.appendChild(el('p', 'news-summary-disclosure', data.disclaimer));
        }
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
        if (!summaryContainer || !summaryContent) {
            return;
        }
        const label = btn.querySelector('.news-summarize-label') || btn;

        // A second click closes an open summary
        if (!summaryContainer.hidden && !inFlight.has(articleUrl)) {
            summaryContainer.hidden = true;
            btn.setAttribute('aria-expanded', 'false');
            return;
        }
        btn.setAttribute('aria-expanded', 'true');

        // Check cache first
        if (summaryCache.has(articleUrl)) {
            renderSummary(summaryContent, summaryCache.get(articleUrl), articleUrl);
            summaryContainer.hidden = false;
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
        renderSkeleton(summaryContent);
        summaryContainer.hidden = false;

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
                    description: articleDescription,
                    url: articleUrl
                })
            });

            if (response.ok) {
                const data = await response.json();
                // Cache the result
                summaryCache.set(articleUrl, data);
                renderSummary(summaryContent, data, articleUrl);
                summaryContainer.hidden = false;
            } else if (response.status === 429) {
                const error = await response.json();
                renderMessage(summaryContent, error.error || PFI18n.t('News.Js.Summary.LimitReached'));
                showError(btn, PFI18n.t('News.Js.Summary.RateLimited'));
            } else {
                renderMessage(summaryContent, PFI18n.t('News.Js.Summary.Failed'));
                showError(btn, PFI18n.t('News.Js.Summary.FailedTitle'));
            }
        } catch (error) {
            renderMessage(summaryContent, PFI18n.t('News.Js.Summary.NetworkError'));
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
