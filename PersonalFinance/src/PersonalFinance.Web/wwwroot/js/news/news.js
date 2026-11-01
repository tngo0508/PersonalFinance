/* global PFI18n */
/**
 * Finance News — Interactive features.
 * The watchlist in the side rail, saving and unsaving articles, and hiding thumbnails that fail to load. The page's text
 * is its News.Js. strings (PFI18n); article titles, sources and topics are shown as the feeds give them.
 */
(function () {
    'use strict';

    const SVG_NS = 'http://www.w3.org/2000/svg';
    const BOOKMARK = [['path', { d: 'M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z' }]];
    const ALERT = [['circle', { cx: '12', cy: '12', r: '10' }], ['path', { d: 'M12 8v4' }], ['path', { d: 'M12 16h.01' }]];

    /**
     * The watchlist, newest first, as the server last returned it plus the saves made on this page.
     * @type {Array<{id: number, title: string, url: string, source: string}>}
     */
    let watchlistItems = [];

    /**
     * Article URLs with a save or remove in flight, so a double click sends one request.
     * @type {Set<string>}
     */
    const pending = new Set();

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
     * @returns {SVGSVGElement} The icon.
     */
    function icon (size, shapes) {
        const svg = document.createElementNS(SVG_NS, 'svg');
        [['width', String(size)], ['height', String(size)], ['viewBox', '0 0 24 24'], ['fill', 'none'],
            ['stroke', 'currentColor'], ['stroke-width', '2'], ['aria-hidden', 'true']]
            .forEach(([name, value]) => svg.setAttribute(name, value));
        shapes.forEach(([tag, attributes]) => {
            const shape = document.createElementNS(SVG_NS, tag);
            Object.entries(attributes).forEach(([name, value]) => shape.setAttribute(name, value));
            svg.append(shape);
        });
        return svg;
    }

    /**
     * Call the Watchlist API with the antiforgery token.
     * @param {string} url - The endpoint.
     * @param {RequestInit} [options] - Fetch options.
     * @returns {Promise<*>} The JSON response, or null for an empty one.
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
        const text = await res.text();
        return text ? JSON.parse(text) : null;
    }

    // =========================================================================
    // Save buttons
    // =========================================================================

    /**
     * The watchlist item saved for an article URL, if any.
     * @param {string} url - The article's URL.
     * @returns {Object|undefined} The item.
     */
    function savedItem (url) {
        return watchlistItems.find((item) => item.url === url);
    }

    /**
     * Show a save button as saved or not.
     * @param {HTMLButtonElement} btn - The button.
     * @param {boolean} isSaved - Whether its article is in the watchlist.
     * @returns {void}
     */
    function setSaveButtonState (btn, isSaved) {
        btn.setAttribute('aria-pressed', String(isSaved));
        btn.title = PFI18n.t(isSaved ? 'News.Js.Watchlist.RemoveTitle' : 'News.Js.Save.Title');
        btn.replaceChildren(icon(15, BOOKMARK),
            element('span', 'news-save-label', null, [PFI18n.t(isSaved ? 'News.Js.Save.Saved' : 'News.Js.Save.Button')]));
    }

    /**
     * Sync every save button with the watchlist.
     * @returns {void}
     */
    function syncSaveButtons () {
        document.querySelectorAll('.news-save-btn').forEach((btn) => {
            if (!btn.classList.contains('save-failed')) {
                setSaveButtonState(btn, !!savedItem(btn.dataset.url));
            }
        });
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
        btn.replaceChildren(icon(15, ALERT), element('span', 'news-save-label', null, [PFI18n.t('News.Js.Save.Failed')]));
        btn.title = message;
        btn.setAttribute('aria-label', PFI18n.t('News.Js.Save.FailedLabel', { message }));
        setTimeout(() => {
            btn.classList.remove('save-failed');
            btn.setAttribute('aria-label', PFI18n.t('News.Js.Save.Label'));
            setSaveButtonState(btn, !!savedItem(btn.dataset.url));
        }, 5000);
    }

    /**
     * Save an article to the watchlist, or remove it when it is already saved.
     * @param {HTMLButtonElement} btn - The article's save button.
     * @returns {Promise<void>}
     */
    async function toggleSave (btn) {
        const url = btn.dataset.url;
        if (pending.has(url)) {
            return;
        }
        const existing = savedItem(url);
        pending.add(url);
        btn.setAttribute('aria-busy', 'true');
        try {
            if (existing) {
                await removeItem(existing.id);
                return;
            }
            const data = await apiFetch('/api/Watchlist', {
                method: 'POST',
                body: JSON.stringify({
                    title: btn.dataset.title,
                    url,
                    source: btn.dataset.source,
                    category: btn.dataset.category,
                    imageUrl: btn.dataset.imageUrl || null
                })
            });
            if (data && data.success && data.id) {
                watchlistItems.unshift({ id: data.id, title: btn.dataset.title, url, source: btn.dataset.source });
                renderWatchlist();
            }
        } catch (err) {
            // eslint-disable-next-line no-console
            console.error('Save article failed:', err);
            showSaveFailed(btn, err.message || PFI18n.t('News.Js.TryAgain'));
        } finally {
            pending.delete(url);
            btn.removeAttribute('aria-busy');
        }
    }

    document.querySelectorAll('.news-save-btn').forEach((btn) => {
        btn.addEventListener('click', () => toggleSave(btn));
    });

    // =========================================================================
    // Watchlist rail
    // =========================================================================
    const list = document.getElementById('watchlistList');
    const message = document.getElementById('watchlistMessage');
    const count = document.getElementById('watchlistCount');

    /**
     * Remove a saved article.
     * @param {number} id - The watchlist item's id.
     * @returns {Promise<void>}
     */
    async function removeItem (id) {
        await apiFetch(`/api/Watchlist/${id}`, { method: 'DELETE' });
        watchlistItems = watchlistItems.filter((item) => item.id !== id);
        renderWatchlist();
    }

    /**
     * One saved article in the rail: its link, its source and a Remove button.
     * @param {Object} item - The watchlist item.
     * @returns {HTMLElement} The list item.
     */
    function watchlistItem (item) {
        const remove = element('button', 'news-watchlist-remove', {
            'type': 'button',
            'aria-label': PFI18n.t('News.Js.Watchlist.RemoveLabel', { title: item.title || '' })
        }, [PFI18n.t('News.Js.Watchlist.Remove')]);
        remove.addEventListener('click', async () => {
            remove.disabled = true;
            try {
                await removeItem(item.id);
                const heading = document.querySelector('.news-rail-card summary');
                if (heading) {
                    heading.focus();
                }
            } catch (err) {
                // eslint-disable-next-line no-console
                console.error('Remove article failed:', err);
                remove.disabled = false;
            }
        });
        return element('li', null, null, [
            element('a', null, { href: item.url, target: '_blank', rel: 'noopener noreferrer' }, [item.title || item.url]),
            element('span', 'news-watchlist-meta', null, [item.source || '', remove])
        ]);
    }

    /**
     * Draw the watchlist in the rail, its count, and every save button's state.
     * @returns {void}
     */
    function renderWatchlist () {
        if (list) {
            list.replaceChildren(...watchlistItems.map(watchlistItem));
        }
        if (message) {
            message.textContent = PFI18n.t('News.Js.Watchlist.Empty');
            message.hidden = watchlistItems.length > 0;
        }
        if (count) {
            count.textContent = PFI18n.formatNumber(watchlistItems.length);
            count.hidden = watchlistItems.length === 0;
        }
        syncSaveButtons();
    }

    /**
     * Load the watchlist into the rail and mark the saved articles.
     * @returns {Promise<void>}
     */
    async function loadWatchlist () {
        try {
            const items = await apiFetch('/api/Watchlist');
            watchlistItems = (Array.isArray(items) ? items : []).filter((item) => item && item.url);
            renderWatchlist();
        } catch (err) {
            // eslint-disable-next-line no-console
            console.error('Load watchlist failed:', err);
            if (message) {
                message.textContent = PFI18n.t('News.Js.Watchlist.LoadFailed');
            }
        }
    }

    loadWatchlist();

    // =========================================================================
    // Layout
    // =========================================================================

    // A thumbnail that fails to load leaves no empty frame.
    document.querySelectorAll('.news-thumb img').forEach((img) => {
        const drop = () => {
            const item = img.closest('.news-item');
            if (item) {
                item.classList.remove('has-img');
            }
            img.parentElement.remove();
        };
        if (img.complete && img.naturalWidth === 0) {
            drop();
        } else {
            img.addEventListener('error', drop);
        }
    });

    // On a narrow screen the rail sits above the articles, so its cards start closed.
    if (window.matchMedia('(max-width: 991.98px)').matches) {
        document.querySelectorAll('[data-news-rail]').forEach((card) => {
            card.open = false;
        });
    }
})();
