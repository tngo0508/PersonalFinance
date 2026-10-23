/**
 * Finance News — Interactive features.
 * Tab switching, news refresh, save to watchlist.
 */
(function () {
    'use strict';

    // =========================================================================
    // Saved Articles Set — tracks which article URLs are already saved
    // =========================================================================
    const savedArticleUrls = new Set();

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
    function timeAgo (dateString) {
        const date = new Date(dateString);
        const now = new Date();
        const diffMs = now.getTime() - date.getTime();
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);
        if (diffMins < 1) {
            return 'Just now';
        }
        if (diffMins < 60) {
            return `${diffMins }m ago`;
        }
        if (diffHours < 24) {
            return `${diffHours }h ago`;
        }
        if (diffDays < 7) {
            return `${diffDays }d ago`;
        }
        return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
    }

    function escapeHtml (str) {
        if (!str) {
            return '';
        }
        const map = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' };
        return str.replace(/[&<>"']/g, m => map[m]);
    }

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
            throw new Error(err.error || `API error: ${ res.status}`);
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

    async function handleRefresh () {
        if (!refreshBtn) {
            return;
        }
        const btn = refreshBtn;
        const originalHtml = btn.innerHTML;
        btn.disabled = true;
        btn.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 2v6h-6"/><path d="M3 12a9 9 0 0 1 15-6.7L21 8"/><path d="M3 22v-6h6"/><path d="M21 12a9 9 0 0 1-15 6.7L3 16"/></svg> Refreshing...';

        try {
            const qs = buildQueryString();
            const url = `/News/Refresh${ qs ? `?${ qs}` : ''}`;
            const response = await fetch(url, { headers: { 'Accept': 'application/json' } });
            if (!response.ok) {
                const err = await response.json().catch(() => ({}));
                throw new Error(err.error || 'Unable to refresh the news right now. Please try again.');
            }
            const data = await response.json();
            showRefreshError(null);

            if (data.success && Array.isArray(data.articles)) {
                const grid = document.getElementById('newsGrid');
                if (grid) {
                    grid.style.opacity = '0.5';
                    grid.style.transition = 'opacity 0.15s ease';
                    requestAnimationFrame(() => {
                        grid.innerHTML = data.articles.map(renderArticleCard).join('');
                        grid.style.opacity = '1';
                        // Re-bind save buttons
                        grid.querySelectorAll('.finance-hub-save-btn').forEach(bindSaveButton);
                    });
                }
                const countEl = document.querySelector('.finance-hub-results-info strong');
                if (countEl) {
                    countEl.textContent = data.articles.length;
                }
            }
        } catch (err) {
            console.error('Refresh failed:', err);
            showRefreshError(err.message);
        } finally {
            btn.disabled = false;
            btn.innerHTML = originalHtml;
        }
    }

    /**
     * Shows a refresh failure above the articles, which stay as they were, or hides it.
     * @param {string|null} message - The message to show, or null to hide the alert.
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

    function renderArticleCard (article) {
        const desc = (article.description || '').length > 250 ? `${(article.description || '').slice(0, 250) }...` : article.description || '';
        const timeLabel = timeAgo(article.publishedAt);
        const isSaved = savedArticleUrls.has(article.url);
        let imgHtml = '';
        if (article.imageUrl) {
            imgHtml = `<a href="${ escapeHtml(article.url) }" class="finance-hub-card-img-link" target="_blank" rel="noopener noreferrer"><div class="finance-hub-card-img"><img src="${ escapeHtml(article.imageUrl) }" alt="" loading="lazy" onerror="this.parentElement.parentElement.style.display='none'"/></div></a>`;
        }
        const saveBtnClass = `btn btn-sm finance-hub-save-btn${ isSaved ? ' saved' : ''}`;
        const saveBtnSvg = isSaved
            ? '<svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor" stroke="currentColor" stroke-width="2"><path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z"/></svg> Saved'
            : '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z"/></svg> Save';
        return `<article class="finance-hub-card" data-article-url="${ escapeHtml(article.url) }">${
            imgHtml
        }<div class="finance-hub-card-body">` +
      `<div class="finance-hub-card-meta"><span class="finance-hub-badge source-badge">${ escapeHtml(article.source) }</span><span class="finance-hub-badge category-badge">${ escapeHtml(article.category) }</span></div>` +
      `<h3 class="finance-hub-card-title"><a href="${ escapeHtml(article.url) }" target="_blank" rel="noopener noreferrer">${ escapeHtml(article.title) }</a></h3>${
          desc ? `<p class="finance-hub-card-desc">${ escapeHtml(desc) }</p>` : ''
      }<div class="finance-hub-card-footer">` +
      `<time class="finance-hub-card-time" datetime="${ article.publishedAt }">` +
      '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>' +
      `<span class="article-time-label">${ timeLabel }</span></time>` +
      '<div class="finance-hub-card-actions">' +
      `<button type="button" class="${ saveBtnClass }" data-title="${ escapeHtml(article.title) }" data-url="${ escapeHtml(article.url) }" data-source="${ escapeHtml(article.source) }" data-category="${ escapeHtml(article.category) }" data-image-url="${ escapeHtml(article.imageUrl || '') }" title="Save to watchlist">${
          saveBtnSvg }</button>` +
      `<a href="${ escapeHtml(article.url) }" target="_blank" rel="noopener noreferrer" class="btn btn-sm btn-outline finance-hub-read-more">Read <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M5 12h14"/><path d="m12 5 7 7-7 7"/></svg></a>` +
      '</div></div></div></article>';
    }

    if (refreshBtn) {
        refreshBtn.addEventListener('click', handleRefresh);
        setInterval(handleRefresh, 5 * 60 * 1000);
    }

    // =========================================================================
    // Save to Watchlist
    // =========================================================================

    /**
   * Mark a save button as saved or unsaved based on the article URL.
   * @param {HTMLElement} btn - The save button element.
   * @param {boolean} isSaved - Whether the article is saved.
   * @returns {void}
   */
    function setSaveButtonState (btn, isSaved) {
        if (isSaved) {
            btn.classList.add('saved');
            btn.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor" stroke="currentColor" stroke-width="2"><path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z"/></svg> Saved';
        } else {
            btn.classList.remove('saved');
            btn.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z"/></svg> Save';
        }
    }

    /**
     * Shows on the save button that saving failed, with the reason in its tooltip and accessible label, then lets
     * the user try again.
     * @param {HTMLButtonElement} btn - The article's save button.
     * @param {string} message - Why the article could not be saved.
     */
    function showSaveFailed (btn, message) {
        btn.classList.add('save-failed');
        btn.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M12 8v4"/><path d="M12 16h.01"/></svg> Couldn’t save';
        btn.title = message;
        btn.setAttribute('aria-label', `Could not save the article: ${ message}`);
        setTimeout(() => {
            btn.classList.remove('save-failed');
            btn.title = 'Save to watchlist';
            btn.setAttribute('aria-label', 'Save article to watchlist');
            setSaveButtonState(btn, savedArticleUrls.has(btn.dataset.url));
        }, 5000);
    }

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
                console.error('Save article failed:', err);
                showSaveFailed(btn, err.message || 'Please try again.');
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
            console.error('Load watchlist URLs failed:', err);
        }
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
            console.error('Load watchlist failed:', err);
            container.innerHTML = '<div class="watchlist-empty"><p>Failed to load watchlist.</p></div>';
        }
    }

    function renderWatchlist () {
        const container = document.getElementById('watchlistGrid');
        if (!container) {
            return;
        }

        if (watchlistItems.length === 0) {
            container.innerHTML = '<div class="watchlist-empty"><p>No saved articles yet. Use the "Save" button on news articles to add them here.</p></div>';
            return;
        }

        container.innerHTML = watchlistItems.map(item => {
            let imgHtml = '';
            if (item.imageUrl) {
                imgHtml = `<div class="watchlist-item-img"><img src="${ escapeHtml(item.imageUrl) }" alt="" onerror="this.parentElement.style.display='none'"/></div>`;
            }
            return `<div class="watchlist-item" data-id="${ item.id }" data-url="${ escapeHtml(item.url) }">${
                imgHtml
            }<div class="watchlist-item-body">` +
        `<h4 class="watchlist-item-title"><a href="${ escapeHtml(item.url) }" target="_blank" rel="noopener noreferrer">${ escapeHtml(item.title) }</a></h4>` +
        '<div class="watchlist-item-meta">' +
        `<span class="finance-hub-badge source-badge">${ escapeHtml(item.source) }</span>` +
        `<span class="finance-hub-badge category-badge">${ escapeHtml(item.category) }</span>` +
        '</div>' +
        '<div class="watchlist-item-actions">' +
        `<a href="${ escapeHtml(item.url) }" target="_blank" rel="noopener noreferrer" class="btn btn-sm btn-outline finance-hub-read-more">Read</a>` +
        '<button type="button" class="watchlist-remove-btn" title="Remove from watchlist">Remove</button>' +
        '</div></div></div>';
        }).join('');

        container.querySelectorAll('.watchlist-remove-btn').forEach(btn => {
            btn.addEventListener('click', async () => {
                const item = btn.closest('.watchlist-item');
                const id = parseInt(item.dataset.id, 10);
                const articleUrl = item.dataset.url;
                try {
                    await apiFetch(`/api/Watchlist/${ id}`, { method: 'DELETE' });
                    watchlistItems = watchlistItems.filter(a => a.id !== id);
                    savedArticleUrls.delete(articleUrl);
                    renderWatchlist();
                    // Reset the corresponding save button on the news tab
                    document.querySelectorAll('.finance-hub-save-btn').forEach(btn => {
                        if (btn.dataset.url === articleUrl) {
                            setSaveButtonState(btn, false);
                        }
                    });
                } catch (err) {
                    console.error('Remove article failed:', err);
                }
            });
        });
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
