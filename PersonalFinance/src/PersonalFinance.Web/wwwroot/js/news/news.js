/**
 * Finance News page — AJAX refresh and interactive behavior.
 */
(function () {
  'use strict';

  const refreshBtn = document.getElementById('refreshBtn');
  const filterForm = document.getElementById('newsFilterForm');
  const alertCloseBtn = document.querySelector('.news-alert-close');

  // Dismiss alert when close button is clicked
  if (alertCloseBtn) {
    alertCloseBtn.addEventListener('click', function () {
      const alert = this.closest('.news-alert');
      if (alert) {
        alert.style.opacity = '0';
        alert.style.transform = 'translateY(-10px)';
        alert.style.transition = 'opacity 0.2s ease, transform 0.2s ease';
        setTimeout(() => alert.remove(), 200);
      }
    });
  }

  /**
   * Build a query string from the current filter form values.
   */
  function buildQueryString() {
    const params = new URLSearchParams();

    if (filterForm) {
      const search = filterForm.querySelector('input[name="search"]');
      const category = filterForm.querySelector('select[name="category"]');
      const source = filterForm.querySelector('select[name="source"]');

      if (search && search.value.trim()) params.set('search', search.value.trim());
      if (category && category.value) params.set('category', category.value);
      if (source && source.value) params.set('source', source.value);
    }

    return params.toString();
  }

  /**
   * Format a UTC ISO date to a relative time string.
   */
  function timeAgo(dateString) {
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    if (diffDays < 7) return `${diffDays}d ago`;
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }

  /**
   * Strip HTML tags from a string.
   */
  function stripHtml(html) {
    const tmp = document.createElement('div');
    tmp.innerHTML = html;
    return tmp.textContent || tmp.innerText || '';
  }

  /**
   * Truncate a string to a max length.
   */
  function truncate(str, max) {
    if (!str) return '';
    return str.length <= max ? str : str.slice(0, max) + '...';
  }

  /**
   * Render an article card from a data object.
   */
  function renderArticle(article) {
    const description = truncate(stripHtml(article.description || ''), 250);
    const timeLabel = timeAgo(article.publishedAt);

    let imageHtml = '';
    if (article.imageUrl) {
      imageHtml = `
        <a href="${escapeHtml(article.url)}" class="news-card-image-link" target="_blank" rel="noopener noreferrer">
          <div class="news-card-image">
            <img src="${escapeHtml(article.imageUrl)}" alt="" loading="lazy"
                 onerror="this.parentElement.parentElement.style.display='none'"/>
          </div>
        </a>`;
    }

    return `
      <article class="news-card">
        ${imageHtml}
        <div class="news-card-body">
          <div class="news-card-header">
            <span class="news-source-badge" title="${escapeHtml(article.source)}">
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M4 22h16a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2H8a2 2 0 0 0-2 2v16a2 2 0 0 1-2 2Zm0 0a2 2 0 0 1-2-2v-9c0-1.1.9-2 2-2h2.5a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H4Z"/>
              </svg>
              ${escapeHtml(article.source)}
            </span>
            <span class="news-category-badge">${escapeHtml(article.category)}</span>
          </div>
          <h3 class="news-card-title">
            <a href="${escapeHtml(article.url)}" target="_blank" rel="noopener noreferrer">
              ${escapeHtml(article.title)}
            </a>
          </h3>
          ${description ? `<p class="news-card-description">${escapeHtml(description)}</p>` : ''}
          <div class="news-card-footer">
            <time class="news-card-time" datetime="${article.publishedAt}" title="${new Date(article.publishedAt).toLocaleString()}">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/>
              </svg>
              ${timeLabel}
            </time>
            <a href="${escapeHtml(article.url)}" target="_blank" rel="noopener noreferrer" class="news-read-more" aria-label="Read full article: ${escapeHtml(article.title)}">
              Read full article
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M5 12h14"/><path d="m12 5 7 7-7 7"/>
              </svg>
            </a>
          </div>
        </div>
      </article>`;
  }

  /**
   * Escape HTML special characters.
   */
  function escapeHtml(str) {
    if (!str) return '';
    const map = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' };
    return str.replace(/[&<>"']/g, m => map[m]);
  }

  /**
   * Handle the refresh button click.
   */
  async function handleRefresh() {
    if (!refreshBtn) return;
    
    const btn = refreshBtn;
    const originalHtml = btn.innerHTML;

    // Show loading state
    btn.disabled = true;
    btn.innerHTML = `
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <path d="M21 2v6h-6"/><path d="M3 12a9 9 0 0 1 15-6.7L21 8"/>
        <path d="M3 22v-6h6"/><path d="M21 12a9 9 0 0 1-15 6.7L3 16"/>
      </svg>
      <span>Refreshing...</span>`;

    try {
      const qs = buildQueryString();
      const url = `/News/Refresh${qs ? '?' + qs : ''}`;

      const response = await fetch(url, {
        headers: { 'Accept': 'application/json' }
      });

      if (!response.ok) throw new Error('Network response was not ok');

      const data = await response.json();

      if (data.success && Array.isArray(data.articles)) {
        const grid = document.querySelector('.news-grid');
        if (grid) {
          // Fade out, update, fade in
          grid.style.opacity = '0.5';
          grid.style.transition = 'opacity 0.15s ease';
          
          requestAnimationFrame(() => {
            grid.innerHTML = data.articles.map(renderArticle).join('');
            grid.style.opacity = '1';
          });
        }

        // Update refresh time
        const refreshTime = document.querySelector('.news-refresh-time');
        if (refreshTime && data.lastRefreshed) {
          const timeAgoText = timeAgo(data.lastRefreshed);
          refreshTime.innerHTML = `
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/>
            </svg>
            ${timeAgoText}`;
        }

        // Update results count
        const resultsCount = document.querySelector('.news-results-count strong');
        if (resultsCount) {
          resultsCount.textContent = data.articles.length;
        }
        const resultsCountText = document.querySelector('.news-results-count');
        if (resultsCountText) {
          const count = data.articles.length;
          resultsCountText.innerHTML = `<strong>${count}</strong> ${count === 1 ? 'article' : 'articles'} found`;
        }

        // Brief success feedback
        btn.style.color = '#107c10';
        setTimeout(() => {
          btn.style.color = '';
        }, 1000);
      } else {
        console.warn('Refresh returned no articles');
      }
    } catch (error) {
      console.error('Failed to refresh news:', error);
      // Brief error feedback
      btn.style.color = '#c24f4f';
      setTimeout(() => {
        btn.style.color = '';
      }, 1000);
    } finally {
      btn.disabled = false;
      btn.innerHTML = originalHtml;
    }
  }

  if (refreshBtn) {
    refreshBtn.addEventListener('click', handleRefresh);
  }

  // Auto-refresh every 5 minutes
  let autoRefreshInterval;
  function startAutoRefresh() {
    autoRefreshInterval = setInterval(handleRefresh, 5 * 60 * 1000);
  }

  function stopAutoRefresh() {
    if (autoRefreshInterval) {
      clearInterval(autoRefreshInterval);
    }
  }

  // Start auto-refresh only if refresh button exists
  if (refreshBtn) {
    startAutoRefresh();

    // Stop auto-refresh when page is hidden
    document.addEventListener('visibilitychange', () => {
      if (document.hidden) {
        stopAutoRefresh();
      } else {
        startAutoRefresh();
      }
    });
  }

  // Enter key triggers search on input
  const searchInput = document.querySelector('.news-search-input');
  if (searchInput && filterForm) {
    searchInput.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        filterForm.submit();
      }
    });
  }

})();
