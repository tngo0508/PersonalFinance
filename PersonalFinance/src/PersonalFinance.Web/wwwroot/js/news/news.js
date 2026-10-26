/* global PFI18n */
/**
 * Finance News — Interactive features.
 * Tab switching, news refresh, save to watchlist. The page's text is its News.Js. strings (PFI18n); article titles,
 * sources and topics are shown as the feeds give them.
 */
(function () {
    'use strict';

    const SVG_NS = 'http://www.w3.org/2000/svg';
    const BOOKMARK = [['path', { d: 'M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z' }]];
    const REFRESH = [
        ['path', { d: 'M21 2v6h-6' }], ['path', { d: 'M3 12a9 9 0 0 1 15-6.7L21 8' }],
        ['path', { d: 'M3 22v-6h6' }], ['path', { d: 'M21 12a9 9 0 0 1-15 6.7L3 16' }]
    ];
    const ALERT = [['circle', { cx: '12', cy: '12', r: '10' }], ['path', { d: 'M12 8v4' }], ['path', { d: 'M12 16h.01' }]];
    const CLOCK = [['circle', { cx: '12', cy: '12', r: '10' }], ['polyline', { points: '12 6 12 12 16 14' }]];
    const ARROW = [['path', { d: 'M5 12h14' }], ['path', { d: 'm12 5 7 7-7 7' }]];

    // =========================================================================
    // Saved Articles Set — tracks which article URLs are already saved
    // =========================================================================
    const savedArticleUrls = new Set();

    // =========================================================================
    // DOM helpers
    // =========================================================================

    /**
     * Create an element with a class, attributes and children.
     * @param {string} tag - The tag name.
     * @param {string|null} [className] - The class.
     * @param {Object<string, string>} [attributes] - Attributes to set.
     * @param {Array<Node|string>} [children] - Child nodes or text.
     * @returns {HTMLElement} The element.
     */
    function element (tag, className, attributes, children) {
        const node = document.createElement(tag);
        if (className) {
            node.className = className;
        }
        Object.entries(attributes || {}).forEach(([name, value]) => node.setAttribute(name, value));
        node.append(...(children || []));
        return node;
    }

    /**
     * An inline stroked icon.
     * @param {number} size - Width and height in px.
     * @param {Array<[string, Object<string, string>]>} shapes - The SVG shapes and their attributes.
     * @param {boolean} [filled] - Whether the shapes are filled with the text colour.
     * @returns {SVGSVGElement} The icon.
     */
    function icon (size, shapes, filled) {
        const svg = document.createElementNS(SVG_NS, 'svg');
        [['width', String(size)], ['height', String(size)], ['viewBox', '0 0 24 24'],
            ['fill', filled ? 'currentColor' : 'none'], ['stroke', 'currentColor'], ['stroke-width', '2']]
            .forEach(([name, value]) => svg.setAttribute(name, value));
        shapes.forEach(([tag, attributes]) => {
            const shape = document.createElementNS(SVG_NS, tag);
            Object.entries(attributes).forEach(([name, value]) => shape.setAttribute(name, value));
            svg.append(shape);
        });
        return svg;
    }

    /**
     * A link that opens an article in a new tab.
     * @param {string} url - The article's URL.
     * @param {string|null} className - The class.
     * @param {Array<Node|string>} children - The link's content.
     * @returns {HTMLAnchorElement} The link.
     */
    function externalLink (url, className, children) {
        return element('a', className, { href: url, target: '_blank', rel: 'noopener noreferrer' }, children);
    }

    /**
     * Hide an image's frame when the image cannot load.
     * @param {HTMLImageElement} img - The image.
     * @param {HTMLElement} frame - What to hide.
     * @returns {HTMLImageElement} The image.
     */
    function hideFrameOnError (img, frame) {
        img.addEventListener('error', () => {
            frame.style.display = 'none';
        });
        return img;
    }

    // =========================================================================
    // Tab Navigation
    // =========================================================================
    const tabButtons = document.querySelectorAll('.finance-hub-tab-btn');
    const panels = document.querySelectorAll('.finance-hub-panel');

    tabButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const tabId = btn.dataset.tab;
            tabButtons.forEach(b => {
                b.classList.remove('active');
                b.setAttribute('aria-selected', 'false');
            });
            panels.forEach(p => p.classList.remove('active'));
            btn.classList.add('active');
            btn.setAttribute('aria-selected', 'true');
            const panel = document.getElementById(`panel-${ tabId}`);
            if (panel) {
                panel.classList.add('active');
            }

            // Lazy-load data when switching to a tab
            if (tabId === 'watchlist') {
                refreshWatchlist();
            }
        });
    });

    // =========================================================================
    // Utility Functions
    // =========================================================================

    /**
     * How long ago an article was published, in words: "Just now", "5m ago", or its date after a week.
     * @param {string} dateString - The ISO publication time.
     * @returns {string} The label.
     */
    function timeAgo (dateString) {
        const date = new Date(dateString);
        const now = new Date();
        const diffMs = now.getTime() - date.getTime();
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);
        if (diffMins < 1) {
            return PFI18n.t('News.Js.Time.JustNow');
        }
        if (diffMins < 60) {
            return PFI18n.t('News.Js.Time.MinutesAgo', { count: PFI18n.formatNumber(diffMins) });
        }
        if (diffHours < 24) {
            return PFI18n.t('News.Js.Time.HoursAgo', { count: PFI18n.formatNumber(diffHours) });
        }
        if (diffDays < 7) {
            return PFI18n.t('News.Js.Time.DaysAgo', { count: PFI18n.formatNumber(diffDays) });
        }
        return PFI18n.formatDate(date, { month: 'short', day: 'numeric', year: 'numeric' });
    }

    /**
     * Call the Watchlist API with the antiforgery token.
     * @param {string} url - The endpoint.
     * @param {RequestInit} [options] - Fetch options.
     * @returns {Promise<*>} The JSON response.
     */
    async function apiFetch (url, options) {
        const res = await fetch(url, {
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
                'RequestVerificationToken': window.PFUtils.getAntiForgeryToken()
            },
            ...options
        });
        if (!res.ok) {
            const err = await res.json().catch(() => ({}));
            throw new Error(err.error || PFI18n.t('News.Js.ApiError', { status: res.status }));
        }
        return res.json();
    }

    // Populate time labels on article cards
    document.querySelectorAll('.finance-hub-card-time').forEach(el => {
        const label = el.querySelector('.article-time-label');
        if (label && el.dateTime) {
            label.textContent = timeAgo(el.dateTime);
        }
    });

    // =========================================================================
    // News Refresh (AJAX)
    // =========================================================================
    const refreshBtn = document.getElementById('refreshBtn');
    const filterForm = document.getElementById('newsFilterForm');

    /**
     * The current filters as a query string.
     * @returns {string} The query string, without "?".
     */
    function buildQueryString () {
        const params = new URLSearchParams();
        if (filterForm) {
            const search = filterForm.querySelector('input[name="search"]');
            const category = filterForm.querySelector('select[name="category"]');
            const source = filterForm.querySelector('select[name="source"]');
            if (search && search.value.trim()) {
                params.set('search', search.value.trim());
            }
            if (category && category.value) {
                params.set('category', category.value);
            }
            if (source && source.value) {
                params.set('source', source.value);
            }
        }
        return params.toString();
    }

    /**
     * Reload the articles for the current filters; on failure the articles stay and the error shows above them.
     * @returns {Promise<void>}
     */
    async function handleRefresh () {
        if (!refreshBtn) {
            return;
        }
        const btn = refreshBtn;
        const original = Array.from(btn.childNodes);
        btn.disabled = true;
        btn.replaceChildren(icon(14, REFRESH), ` ${PFI18n.t('News.Js.Refresh.Refreshing')}`);

        try {
            const qs = buildQueryString();
            const url = `/News/Refresh${ qs ? `?${ qs}` : ''}`;
            const response = await fetch(url, { headers: { 'Accept': 'application/json' } });
            if (!response.ok) {
                const err = await response.json().catch(() => ({}));
                throw new Error(err.error || PFI18n.t('News.Js.Refresh.Failed'));
            }
            const data = await response.json();
            showRefreshError(null);

            if (data.success && Array.isArray(data.articles)) {
                const grid = document.getElementById('newsGrid');
                if (grid) {
                    grid.style.opacity = '0.5';
                    grid.style.transition = 'opacity 0.15s ease';
                    requestAnimationFrame(() => {
                        grid.replaceChildren(...data.articles.map(renderArticleCard));
                        grid.style.opacity = '1';
                        // Re-bind save buttons
                        grid.querySelectorAll('.finance-hub-save-btn').forEach(bindSaveButton);
                    });
                }
                const countEl = document.querySelector('.finance-hub-results-info strong');
                if (countEl) {
                    countEl.textContent = PFI18n.formatNumber(data.articles.length);
                }
            }
        } catch (err) {
            // eslint-disable-next-line no-console
            console.error('Refresh failed:', err);
            showRefreshError(err.message);
        } finally {
            btn.disabled = false;
            btn.replaceChildren(...original);
        }
    }

    /**
     * Shows a refresh failure above the articles, which stay as they were, or hides it.
     * @param {string|null} message - The message to show, or null to hide the alert.
     * @returns {void}
     */
    function showRefreshError (message) {
        const alertEl = document.getElementById('newsRefreshError');
        const messageEl = document.getElementById('newsRefreshErrorMessage');
        if (!alertEl || !messageEl) {
            return;
        }
        messageEl.textContent = message || '';
        alertEl.hidden = !message;
    }

    /**
     * One article's card, as the page renders it.
     * @param {Object} article - The article from the Refresh action.
     * @returns {HTMLElement} The card.
     */
    function renderArticleCard (article) {
        const description = article.description || '';
        const desc = description.length > 250 ? `${description.slice(0, 250)}...` : description;
        const card = element('article', 'finance-hub-card', { 'data-article-url': article.url });
        if (article.imageUrl) {
            const frame = element('div', 'finance-hub-card-img');
            const link = externalLink(article.url, 'finance-hub-card-img-link', [frame]);
            frame.append(hideFrameOnError(element('img', null, { src: article.imageUrl, alt: '', loading: 'lazy' }), link));
            card.append(link);
        }
        const body = element('div', 'finance-hub-card-body', null, [
            element('div', 'finance-hub-card-meta', null, [
                element('span', 'finance-hub-badge source-badge', null, [article.source || '']),
                element('span', 'finance-hub-badge category-badge', null, [article.category || ''])
            ]),
            element('h3', 'finance-hub-card-title', null, [externalLink(article.url, null, [article.title || ''])])
        ]);
        if (desc) {
            body.append(element('p', 'finance-hub-card-desc', null, [desc]));
        }
        const save = element('button', `btn btn-sm finance-hub-save-btn${savedArticleUrls.has(article.url) ? ' saved' : ''}`, {
            'type': 'button',
            'data-title': article.title || '',
            'data-url': article.url || '',
            'data-source': article.source || '',
            'data-category': article.category || '',
            'data-image-url': article.imageUrl || '',
            'title': PFI18n.t('News.Js.Save.Title'),
            'aria-label': PFI18n.t('News.Js.Save.Label')
        }, saveButtonContent(savedArticleUrls.has(article.url)));
        body.append(element('div', 'finance-hub-card-footer', null, [
            element('time', 'finance-hub-card-time', { datetime: article.publishedAt }, [
                icon(14, CLOCK),
                element('span', 'article-time-label', null, [timeAgo(article.publishedAt)])
            ]),
            element('div', 'finance-hub-card-actions', null, [
                save,
                externalLink(article.url, 'btn btn-sm btn-outline finance-hub-read-more',
                    [`${PFI18n.t('News.Js.Read')} `, icon(12, ARROW)])
            ])
        ]));
        card.append(body);
        return card;
    }

    if (refreshBtn) {
        refreshBtn.addEventListener('click', handleRefresh);
        setInterval(handleRefresh, 5 * 60 * 1000);
    }

    // =========================================================================
    // Save to Watchlist
    // =========================================================================

    /**
     * The save button's content: its icon and its label.
     * @param {boolean} isSaved - Whether the article is saved.
     * @returns {Array<Node|string>} The button's content.
     */
    function saveButtonContent (isSaved) {
        return [icon(14, BOOKMARK, isSaved), ` ${PFI18n.t(isSaved ? 'News.Js.Save.Saved' : 'News.Js.Save.Button')}`];
    }

    /**
     * Mark a save button as saved or unsaved based on the article URL.
     * @param {HTMLElement} btn - The save button element.
     * @param {boolean} isSaved - Whether the article is saved.
     * @returns {void}
     */
    function setSaveButtonState (btn, isSaved) {
        btn.classList.toggle('saved', isSaved);
        btn.replaceChildren(...saveButtonContent(isSaved));
    }

    /**
     * Shows on the save button that saving failed, with the reason in its tooltip and accessible label, then lets
     * the user try again.
     * @param {HTMLButtonElement} btn - The article's save button.
     * @param {string} message - Why the article could not be saved.
     * @returns {void}
     */
    function showSaveFailed (btn, message) {
        btn.classList.add('save-failed');
        btn.replaceChildren(icon(14, ALERT), ` ${PFI18n.t('News.Js.Save.Failed')}`);
        btn.title = message;
        btn.setAttribute('aria-label', PFI18n.t('News.Js.Save.FailedLabel', { message }));
        setTimeout(() => {
            btn.classList.remove('save-failed');
            btn.title = PFI18n.t('News.Js.Save.Title');
            btn.setAttribute('aria-label', PFI18n.t('News.Js.Save.Label'));
            setSaveButtonState(btn, savedArticleUrls.has(btn.dataset.url));
        }, 5000);
    }

    /**
     * Wire an article's save button: it saves the article to the watchlist once.
     * @param {HTMLButtonElement} btn - The save button.
     * @returns {void}
     */
    function bindSaveButton (btn) {
        // Set initial state from savedArticleUrls
        const url = btn.dataset.url;
        if (url && savedArticleUrls.has(url)) {
            setSaveButtonState(btn, true);
        }

        btn.addEventListener('click', async () => {
            // If already saved, do nothing
            if (savedArticleUrls.has(url)) {
                return;
            }

            const title = btn.dataset.title;
            const source = btn.dataset.source;
            const category = btn.dataset.category;
            const imageUrl = btn.dataset.imageUrl;

            try {
                const data = await apiFetch('/api/Watchlist', {
                    method: 'POST',
                    body: JSON.stringify({ title, url, source, category, imageUrl })
                });

                if (data.success) {
                    savedArticleUrls.add(url);
                    setSaveButtonState(btn, true);
                    // Update watchlist in-memory so the tab shows the new item
                    if (data.id) {
                        watchlistItems.unshift({
                            id: data.id,
                            title,
                            url,
                            source,
                            category,
                            imageUrl: imageUrl || null,
                            notes: null,
                            savedAtUtc: new Date().toISOString()
                        });
                        if (window._watchlistLoaded) {
                            renderWatchlist();
                        }
                    }
                }
            } catch (err) {
                // eslint-disable-next-line no-console
                console.error('Save article failed:', err);
                showSaveFailed(btn, err.message || PFI18n.t('News.Js.TryAgain'));
            }
        });
    }

    document.querySelectorAll('.finance-hub-save-btn').forEach(bindSaveButton);

    // =========================================================================
    // Watchlist
    // =========================================================================
    let watchlistItems = [];

    /**
     * Load the watchlist from the server and populate the savedArticleUrls set.
     * Called once on page init (silently) to know which articles are already saved.
     * @returns {Promise<void>}
     */
    async function loadWatchlistUrls () {
        try {
            const items = await apiFetch('/api/Watchlist');
            watchlistItems = items;
            items.forEach(item => {
                if (item.url) {
                    savedArticleUrls.add(item.url);
                }
            });
            // Now that we know saved URLs, update the initial save buttons
            document.querySelectorAll('.finance-hub-save-btn').forEach(btn => {
                if (btn.dataset.url && savedArticleUrls.has(btn.dataset.url)) {
                    setSaveButtonState(btn, true);
                }
            });
        } catch (err) {
            // eslint-disable-next-line no-console
            console.error('Load watchlist URLs failed:', err);
        }
    }

    /**
     * A one-line message in the watchlist in place of its items.
     * @param {string} text - The message.
     * @returns {HTMLElement} The message.
     */
    function watchlistMessage (text) {
        return element('div', 'watchlist-empty', null, [element('p', null, null, [text])]);
    }

    /**
     * Refresh the watchlist data from the server when the user switches to the tab.
     * @returns {Promise<void>}
     */
    async function refreshWatchlist () {
        if (!window._watchlistLoaded) {
            window._watchlistLoaded = true;
        }
        const container = document.getElementById('watchlistGrid');
        if (!container) {
            return;
        }

        try {
            watchlistItems = await apiFetch('/api/Watchlist');
            // Sync savedArticleUrls with latest server data
            savedArticleUrls.clear();
            watchlistItems.forEach(item => {
                if (item.url) {
                    savedArticleUrls.add(item.url);
                }
            });
            renderWatchlist();
        } catch (err) {
            // eslint-disable-next-line no-console
            console.error('Load watchlist failed:', err);
            container.replaceChildren(watchlistMessage(PFI18n.t('News.Js.Watchlist.LoadFailed')));
        }
    }

    /**
     * One saved article in the watchlist, with its Read link and Remove button.
     * @param {Object} item - The watchlist item.
     * @returns {HTMLElement} The item.
     */
    function watchlistItem (item) {
        const node = element('div', 'watchlist-item', { 'data-id': String(item.id), 'data-url': item.url || '' });
        if (item.imageUrl) {
            const frame = element('div', 'watchlist-item-img');
            frame.append(hideFrameOnError(element('img', null, { src: item.imageUrl, alt: '' }), frame));
            node.append(frame);
        }
        const remove = element('button', 'watchlist-remove-btn',
            { type: 'button', title: PFI18n.t('News.Js.Watchlist.RemoveTitle') }, [PFI18n.t('News.Js.Watchlist.Remove')]);
        remove.addEventListener('click', () => removeItem(item.id, item.url));
        node.append(element('div', 'watchlist-item-body', null, [
            element('h4', 'watchlist-item-title', null, [externalLink(item.url, null, [item.title || ''])]),
            element('div', 'watchlist-item-meta', null, [
                element('span', 'finance-hub-badge source-badge', null, [item.source || '']),
                element('span', 'finance-hub-badge category-badge', null, [item.category || ''])
            ]),
            element('div', 'watchlist-item-actions', null, [
                externalLink(item.url, 'btn btn-sm btn-outline finance-hub-read-more', [PFI18n.t('News.Js.Read')]),
                remove
            ])
        ]));
        return node;
    }

    /**
     * Remove a saved article and reset its save button on the news tab.
     * @param {number} id - The watchlist item's id.
     * @param {string} articleUrl - The article's URL.
     * @returns {Promise<void>}
     */
    async function removeItem (id, articleUrl) {
        try {
            await apiFetch(`/api/Watchlist/${ id}`, { method: 'DELETE' });
            watchlistItems = watchlistItems.filter(a => a.id !== id);
            savedArticleUrls.delete(articleUrl);
            renderWatchlist();
            document.querySelectorAll('.finance-hub-save-btn').forEach(btn => {
                if (btn.dataset.url === articleUrl) {
                    setSaveButtonState(btn, false);
                }
            });
        } catch (err) {
            // eslint-disable-next-line no-console
            console.error('Remove article failed:', err);
        }
    }

    /**
     * Draw the watchlist, or its empty message.
     * @returns {void}
     */
    function renderWatchlist () {
        const container = document.getElementById('watchlistGrid');
        if (!container) {
            return;
        }
        container.replaceChildren(...(watchlistItems.length === 0
            ? [watchlistMessage(PFI18n.t('News.Js.Watchlist.Empty'))]
            : watchlistItems.map(watchlistItem)));
    }

    // Load watchlist URLs on page init to populate savedArticleUrls
    loadWatchlistUrls();

    // =========================================================================
    // Enter key on search
    // =========================================================================
    const searchInput = document.querySelector('.finance-hub-search-input');
    if (searchInput && filterForm) {
        searchInput.addEventListener('keydown', e => {
            if (e.key === 'Enter') {
                e.preventDefault();
                filterForm.submit();
            }
        });
    }

})();
