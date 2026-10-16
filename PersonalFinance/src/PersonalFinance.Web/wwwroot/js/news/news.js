/**
 * Personal Finance Hub — Interactive features.
 * Tab switching, budget planner, savings goals, investment calculator, watchlist.
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
      tabButtons.forEach(b => { b.classList.remove('active'); b.setAttribute('aria-selected', 'false'); });
      panels.forEach(p => p.classList.remove('active'));
      btn.classList.add('active');
      btn.setAttribute('aria-selected', 'true');
      const panel = document.getElementById('panel-' + tabId);
      if (panel) panel.classList.add('active');

      // Lazy-load data when switching to a tab
      if (tabId === 'savings' && !window._savingsLoaded) loadSavingsGoals();
      if (tabId === 'watchlist') refreshWatchlist();
      if (tabId === 'budget' && !window._budgetInitialized) initBudgetPlanner();
      if (tabId === 'calculator' && !window._calcInitialized) initCalculator();
    });
  });

  // =========================================================================
  // Utility Functions
  // =========================================================================
  function formatCurrency(value) {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', minimumFractionDigits: 0, maximumFractionDigits: 0 }).format(value);
  }

  function timeAgo(dateString) {
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);
    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return diffMins + 'm ago';
    if (diffHours < 24) return diffHours + 'h ago';
    if (diffDays < 7) return diffDays + 'd ago';
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }

  function escapeHtml(str) {
    if (!str) return '';
    const map = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' };
    return str.replace(/[&<>"']/g, m => map[m]);
  }

  async function apiFetch(url, options) {
    const res = await fetch(url, {
      headers: { 'Accept': 'application/json', 'Content-Type': 'application/json' },
      ...options
    });
    if (!res.ok) throw new Error('API error: ' + res.status);
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

  async function handleRefresh() {
    if (!refreshBtn) return;
    const btn = refreshBtn;
    const originalHtml = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 2v6h-6"/><path d="M3 12a9 9 0 0 1 15-6.7L21 8"/><path d="M3 22v-6h6"/><path d="M21 12a9 9 0 0 1-15 6.7L3 16"/></svg> Refreshing...';

    try {
      const qs = buildQueryString();
      const url = '/News/Refresh' + (qs ? '?' + qs : '');
      const response = await fetch(url, { headers: { 'Accept': 'application/json' } });
      const data = await response.json();

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
        if (countEl) countEl.textContent = data.articles.length;
      }
    } catch (err) {
      console.error('Refresh failed:', err);
    } finally {
      btn.disabled = false;
      btn.innerHTML = originalHtml;
    }
  }

  function renderArticleCard(article) {
    const desc = (article.description || '').length > 250 ? (article.description || '').slice(0, 250) + '...' : article.description || '';
    const timeLabel = timeAgo(article.publishedAt);
    const isSaved = savedArticleUrls.has(article.url);
    let imgHtml = '';
    if (article.imageUrl) {
      imgHtml = '<a href="' + escapeHtml(article.url) + '" class="finance-hub-card-img-link" target="_blank" rel="noopener noreferrer"><div class="finance-hub-card-img"><img src="' + escapeHtml(article.imageUrl) + '" alt="" loading="lazy" onerror="this.parentElement.parentElement.style.display=\'none\'"/></div></a>';
    }
    const saveBtnClass = 'btn btn-sm finance-hub-save-btn' + (isSaved ? ' saved' : '');
    const saveBtnSvg = isSaved
      ? '<svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor" stroke="currentColor" stroke-width="2"><path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z"/></svg> Saved'
      : '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z"/></svg> Save';
    return '<article class="finance-hub-card" data-article-url="' + escapeHtml(article.url) + '">' +
      imgHtml +
      '<div class="finance-hub-card-body">' +
      '<div class="finance-hub-card-meta"><span class="finance-hub-badge source-badge">' + escapeHtml(article.source) + '</span><span class="finance-hub-badge category-badge">' + escapeHtml(article.category) + '</span></div>' +
      '<h3 class="finance-hub-card-title"><a href="' + escapeHtml(article.url) + '" target="_blank" rel="noopener noreferrer">' + escapeHtml(article.title) + '</a></h3>' +
      (desc ? '<p class="finance-hub-card-desc">' + escapeHtml(desc) + '</p>' : '') +
      '<div class="finance-hub-card-footer">' +
      '<time class="finance-hub-card-time" datetime="' + article.publishedAt + '">' +
      '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>' +
      '<span class="article-time-label">' + timeLabel + '</span></time>' +
      '<div class="finance-hub-card-actions">' +
      '<button type="button" class="' + saveBtnClass + '" data-title="' + escapeHtml(article.title) + '" data-url="' + escapeHtml(article.url) + '" data-source="' + escapeHtml(article.source) + '" data-category="' + escapeHtml(article.category) + '" data-image-url="' + escapeHtml(article.imageUrl || '') + '" title="Save to watchlist">' +
      saveBtnSvg + '</button>' +
      '<a href="' + escapeHtml(article.url) + '" target="_blank" rel="noopener noreferrer" class="btn btn-sm btn-outline finance-hub-read-more">Read <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M5 12h14"/><path d="m12 5 7 7-7 7"/></svg></a>' +
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
  function setSaveButtonState(btn, isSaved) {
    if (isSaved) {
      btn.classList.add('saved');
      btn.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor" stroke="currentColor" stroke-width="2"><path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z"/></svg> Saved';
    } else {
      btn.classList.remove('saved');
      btn.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z"/></svg> Save';
    }
  }

  function bindSaveButton(btn) {
    // Set initial state from savedArticleUrls
    const url = btn.dataset.url;
    if (url && savedArticleUrls.has(url)) {
      setSaveButtonState(btn, true);
    }

    btn.addEventListener('click', async () => {
      // If already saved, do nothing
      if (savedArticleUrls.has(url)) return;

      const title = btn.dataset.title;
      const source = btn.dataset.source;
      const category = btn.dataset.category;
      const imageUrl = btn.dataset.imageUrl;

      try {
        const data = await apiFetch('/api/PersonalFinance/watchlist', {
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
            if (window._watchlistLoaded) renderWatchlist();
          }
        }
      } catch (err) {
        console.error('Save article failed:', err);
      }
    });
  }

  document.querySelectorAll('.finance-hub-save-btn').forEach(bindSaveButton);

  // =========================================================================
  // Budget Planner
  // =========================================================================
  const budgetCategories = [
    { key: 'housing', label: 'Housing', color: '#107c10', default: 30 },
    { key: 'food', label: 'Food', color: '#0078d4', default: 10 },
    { key: 'transportation', label: 'Transportation', color: '#ff8c00', default: 10 },
    { key: 'utilities', label: 'Utilities', color: '#8764b8', default: 5 },
    { key: 'insurance', label: 'Insurance', color: '#e74856', default: 5 },
    { key: 'healthcare', label: 'Healthcare', color: '#00b7c3', default: 5 },
    { key: 'savings', label: 'Savings', color: '#107c10', default: 15 },
    { key: 'debtRepayment', label: 'Debt Repayment', color: '#c24f4f', default: 5 },
    { key: 'entertainment', label: 'Entertainment', color: '#888888', default: 5 },
    { key: 'miscellaneous', label: 'Miscellaneous', color: '#6b6b6b', default: 10 }
  ];

  let budgetAllocations = {};
  let budgetChartInstance = null;

  function initBudgetPlanner() {
    if (window._budgetInitialized) return;
    window._budgetInitialized = true;

    // Initialize allocations
    budgetCategories.forEach(cat => {
      budgetAllocations[cat.key] = cat.default;
    });

    // Build slider UI
    const container = document.getElementById('budgetCategories');
    if (!container) return;

    container.innerHTML = budgetCategories.map(cat =>
      '<div class="budget-category-item">' +
      '<div class="budget-category-label"><span class="budget-category-dot" style="background:' + cat.color + '"></span>' + cat.label + '</div>' +
      '<input type="range" class="form-range budget-category-slider" data-key="' + cat.key + '" min="0" max="100" value="' + cat.default + '" />' +
      '<div class="budget-category-value" id="budgetVal_' + cat.key + '">' + cat.default + '%</div>' +
      '</div>'
    ).join('');

    // Bind slider events
    container.querySelectorAll('.budget-category-slider').forEach(slider => {
      slider.addEventListener('input', onBudgetSliderChange);
    });

    // Income input
    const incomeInput = document.getElementById('monthlyIncome');
    if (incomeInput) {
      incomeInput.addEventListener('input', updateBudgetDisplay);
    }

    // Save button
    const saveBtn = document.getElementById('budgetSaveBtn');
    if (saveBtn) saveBtn.addEventListener('click', saveBudget);

    updateBudgetDisplay();
    loadSavedBudget();
  }

  function onBudgetSliderChange(e) {
    const key = e.target.dataset.key;
    budgetAllocations[key] = parseInt(e.target.value, 10);
    updateBudgetDisplay();
  }

  function updateBudgetDisplay() {
    const income = parseFloat(document.getElementById('monthlyIncome')?.value) || 0;

    // Update value displays
    budgetCategories.forEach(cat => {
      const valEl = document.getElementById('budgetVal_' + cat.key);
      const slider = document.querySelector('.budget-category-slider[data-key="' + cat.key + '"]');
      if (valEl) valEl.textContent = budgetAllocations[cat.key] + '%';
      if (slider) slider.value = budgetAllocations[cat.key];
    });

    // Total
    const total = Object.values(budgetAllocations).reduce((a, b) => a + b, 0);
    const totalPercentEl = document.getElementById('budgetTotalPercent');
    const totalFillEl = document.getElementById('budgetTotalFill');
    const totalAmountEl = document.getElementById('budgetTotalAmount');

    if (totalPercentEl) totalPercentEl.textContent = total + '%';
    if (totalFillEl) {
      totalFillEl.style.width = Math.min(total, 100) + '%';
      totalFillEl.classList.toggle('over', total > 100);
    }
    if (totalAmountEl) totalAmountEl.textContent = formatCurrency(income * total / 100) + ' of ' + formatCurrency(income);

    drawBudgetChart();
  }

  function drawBudgetChart() {
    const canvas = document.getElementById('budgetChart');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    const size = 320;
    canvas.width = size * 2; // Retina
    canvas.height = size * 2;
    canvas.style.width = size + 'px';
    canvas.style.height = size + 'px';
    ctx.scale(2, 2);

    const cx = size / 2;
    const cy = size / 2;
    const radius = 120;
    const innerRadius = 70;

    ctx.clearRect(0, 0, size, size);

    const total = Object.values(budgetAllocations).reduce((a, b) => a + b, 0);
    if (total === 0) {
      ctx.beginPath();
      ctx.arc(cx, cy, radius, 0, Math.PI * 2);
      ctx.arc(cx, cy, innerRadius, 0, Math.PI * 2, true);
      ctx.fillStyle = '#e0e0e0';
      ctx.fill();
      return;
    }

    let startAngle = -Math.PI / 2;
    budgetCategories.forEach(cat => {
      const slice = (budgetAllocations[cat.key] / 100) * Math.PI * 2;
      ctx.beginPath();
      ctx.arc(cx, cy, radius, startAngle, startAngle + slice);
      ctx.arc(cx, cy, innerRadius, startAngle + slice, startAngle, true);
      ctx.closePath();
      ctx.fillStyle = cat.color;
      ctx.fill();
      startAngle += slice;
    });

    // Center text
    const income = parseFloat(document.getElementById('monthlyIncome')?.value) || 0;
    ctx.fillStyle = '#333';
    ctx.font = 'bold 18px system-ui, sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(formatCurrency(income * total / 100), cx, cy - 8);
    ctx.font = '12px system-ui, sans-serif';
    ctx.fillStyle = '#888';
    ctx.fillText(total + '% allocated', cx, cy + 12);

    // Legend
    const legendEl = document.getElementById('budgetChartLegend');
    if (legendEl) {
      legendEl.innerHTML = budgetCategories.map(cat =>
        '<div class="budget-legend-item"><span class="budget-legend-dot" style="background:' + cat.color + '"></span>' + cat.label + ' (' + budgetAllocations[cat.key] + '%)</div>'
      ).join('');
    }
  }

  async function loadSavedBudget() {
    const period = new Date().toISOString().slice(0, 7); // yyyy-MM
    try {
      const data = await apiFetch('/api/PersonalFinance/budget/' + period);
      if (data.allocations) {
        const incomeInput = document.getElementById('monthlyIncome');
        if (incomeInput && data.monthlyIncome) incomeInput.value = data.monthlyIncome;

        budgetCategories.forEach(cat => {
          const val = data.allocations[cat.key];
          if (val !== undefined) budgetAllocations[cat.key] = val;
        });
        updateBudgetDisplay();
      }
    } catch (err) {
      // No saved budget, use defaults
    }
  }

  async function saveBudget() {
    const btn = document.getElementById('budgetSaveBtn');
    const status = document.getElementById('budgetSaveStatus');
    if (!btn) return;

    const period = new Date().toISOString().slice(0, 7);
    const income = parseFloat(document.getElementById('monthlyIncome')?.value) || 0;

    const allocations = {};
    budgetCategories.forEach(cat => { allocations[cat.key] = budgetAllocations[cat.key]; });

    btn.disabled = true;
    try {
      await apiFetch('/api/PersonalFinance/budget', {
        method: 'POST',
        body: JSON.stringify({ period, monthlyIncome: income, allocations })
      });
      if (status) {
        status.textContent = '✓ Budget saved!';
        setTimeout(() => { if (status) status.textContent = ''; }, 3000);
      }
    } catch (err) {
      console.error('Save budget failed:', err);
      if (status) status.textContent = '✗ Save failed';
    } finally {
      btn.disabled = false;
    }
  }

  // =========================================================================
  // Savings Goals
  // =========================================================================
  let savingsGoals = [];

  async function loadSavingsGoals() {
    window._savingsLoaded = true;
    const container = document.getElementById('savingsGoalsList');
    if (!container) return;

    try {
      savingsGoals = await apiFetch('/api/PersonalFinance/savings-goals');
      renderSavingsGoals();
    } catch (err) {
      console.error('Load savings goals failed:', err);
      container.innerHTML = '<div class="savings-goals-empty"><p>Failed to load goals.</p></div>';
    }
  }

  function renderSavingsGoals() {
    const container = document.getElementById('savingsGoalsList');
    if (!container) return;

    if (savingsGoals.length === 0) {
      container.innerHTML = '<div class="savings-goals-empty"><p>No savings goals yet. Create one to start tracking!</p></div>';
      return;
    }

    container.innerHTML = savingsGoals.map(goal => {
      const progress = goal.targetAmount > 0 ? Math.min(100, (goal.currentAmount / goal.targetAmount) * 100) : 0;
      const isComplete = goal.isCompleted || progress >= 100;
      return '<div class="savings-goal-card' + (isComplete ? ' completed' : '') + '" data-id="' + goal.id + '">' +
        '<div class="savings-goal-header">' +
        '<h3 class="savings-goal-name">' + escapeHtml(goal.name) + '</h3>' +
        '<div class="savings-goal-actions">' +
        '<button type="button" class="goal-edit-btn" title="Update amount" aria-label="Update goal amount">' +
        '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg></button>' +
        '<button type="button" class="goal-delete-btn" title="Delete goal" aria-label="Delete goal">' +
        '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg></button>' +
        '</div></div>' +
        '<div class="savings-goal-amounts"><span class="savings-goal-current">' + formatCurrency(goal.currentAmount) + '</span><span class="savings-goal-target">of ' + formatCurrency(goal.targetAmount) + '</span></div>' +
        '<div class="savings-goal-progress-track"><div class="savings-goal-progress-fill' + (isComplete ? ' complete' : '') + '" style="width:' + progress + '%"></div></div>' +
        '<div class="savings-goal-meta"><span class="savings-goal-percent">' + Math.round(progress) + '%</span>' +
        (goal.targetDate ? '<span>Target: ' + new Date(goal.targetDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) + '</span>' : '') +
        '</div>' +
        '<div class="savings-goal-update-section">' +
        '<input type="number" class="form-control savings-goal-update-input" placeholder="New amount ($)" value="' + goal.currentAmount + '" min="0" step="50"/>' +
        '<button type="button" class="btn btn-primary savings-goal-update-btn">Update</button>' +
        '</div>' +
        '</div>';
    }).join('');

    // Bind events
    container.querySelectorAll('.goal-edit-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        const card = btn.closest('.savings-goal-card');
        card.classList.toggle('expanded');
      });
    });

    container.querySelectorAll('.goal-delete-btn').forEach(btn => {
      btn.addEventListener('click', async () => {
        const card = btn.closest('.savings-goal-card');
        const id = parseInt(card.dataset.id, 10);
        if (confirm('Delete this savings goal?')) {
          try {
            await apiFetch('/api/PersonalFinance/savings-goals/' + id, { method: 'DELETE' });
            savingsGoals = savingsGoals.filter(g => g.id !== id);
            renderSavingsGoals();
          } catch (err) {
            console.error('Delete goal failed:', err);
          }
        }
      });
    });

    container.querySelectorAll('.savings-goal-update-btn').forEach(btn => {
      btn.addEventListener('click', async () => {
        const card = btn.closest('.savings-goal-card');
        const id = parseInt(card.dataset.id, 10);
        const input = card.querySelector('.savings-goal-update-input');
        const newAmount = parseFloat(input.value) || 0;

        try {
          await apiFetch('/api/PersonalFinance/savings-goals/' + id, {
            method: 'PUT',
            body: JSON.stringify({ currentAmount: newAmount, isCompleted: false })
          });
          const goal = savingsGoals.find(g => g.id === id);
          if (goal) {
            goal.currentAmount = newAmount;
            goal.progressPercent = goal.targetAmount > 0 ? Math.min(100, (newAmount / goal.targetAmount) * 100) : 0;
          }
          renderSavingsGoals();
        } catch (err) {
          console.error('Update goal failed:', err);
        }
      });
    });
  }

  // Add Goal Modal
  const addGoalBtn = document.getElementById('addGoalBtn');
  if (addGoalBtn) {
    addGoalBtn.addEventListener('click', () => {
      const modal = new bootstrap.Modal(document.getElementById('addGoalModal'));
      modal.show();
    });
  }

  const createGoalBtn = document.getElementById('createGoalBtn');
  if (createGoalBtn) {
    createGoalBtn.addEventListener('click', async () => {
      const name = document.getElementById('goalName')?.value.trim();
      const targetAmount = parseFloat(document.getElementById('goalTarget')?.value) || 0;
      const currentAmount = parseFloat(document.getElementById('goalCurrent')?.value) || 0;
      const description = document.getElementById('goalDescription')?.value.trim() || null;
      const targetDateVal = document.getElementById('goalTargetDate')?.value;
      const targetDate = targetDateVal ? new Date(targetDateVal).toISOString() : null;

      if (!name || targetAmount <= 0) {
        alert('Please enter a goal name and a valid target amount.');
        return;
      }

      try {
        await apiFetch('/api/PersonalFinance/savings-goals', {
          method: 'POST',
          body: JSON.stringify({ name, targetAmount, currentAmount, description, targetDate })
        });

        const modal = bootstrap.Modal.getInstance(document.getElementById('addGoalModal'));
        if (modal) modal.hide();

        // Reset form
        document.getElementById('goalName').value = '';
        document.getElementById('goalTarget').value = '10000';
        document.getElementById('goalCurrent').value = '0';
        document.getElementById('goalDescription').value = '';
        document.getElementById('goalTargetDate').value = '';

        savingsGoals = [];
        window._savingsLoaded = false;
        loadSavingsGoals();
      } catch (err) {
        console.error('Create goal failed:', err);
        alert('Failed to create goal.');
      }
    });
  }

  // =========================================================================
  // Investment Calculator
  // =========================================================================
    let calcChartTooltip = {visible: false, x: 0, y: 0, text: ''};

    let calcChartResizeTimer = null;

  function initCalculator() {
    if (window._calcInitialized) return;
    window._calcInitialized = true;

    const inputs = [
      { rangeId: 'calcInitialRange', numId: 'calcInitial', valId: 'calcInitialValue', format: v => formatCurrency(v) },
      { rangeId: 'calcMonthlyRange', numId: 'calcMonthly', valId: 'calcMonthlyValue', format: v => formatCurrency(v) },
      { rangeId: 'calcRateRange', numId: 'calcRate', valId: 'calcRateValue', format: v => v + '%' },
      { rangeId: 'calcYearsRange', numId: 'calcYears', valId: 'calcYearsValue', format: v => v + ' years' }
    ];

    inputs.forEach(({ rangeId, numId, valId, format }) => {
      const range = document.getElementById(rangeId);
      const num = document.getElementById(numId);
      const val = document.getElementById(valId);
      if (!range || !num || !val) return;

      range.addEventListener('input', () => { num.value = range.value; val.textContent = format(range.value); calculateInvestment(); });
      num.addEventListener('input', () => { range.value = num.value; val.textContent = format(num.value); calculateInvestment(); });
    });

    calculateInvestment();
      setupCalcChartTooltip();
      setupCalcChartResize();
  }

    // ---- Resize handler for responsive chart ----
    function setupCalcChartResize() {
        const container = document.querySelector('.investment-calc-chart-container');
        if (!container) return;

        const observer = new ResizeObserver(() => {
            if (calcChartResizeTimer) cancelAnimationFrame(calcChartResizeTimer);
            calcChartResizeTimer = requestAnimationFrame(() => {
                const initial = parseFloat(document.getElementById('calcInitial')?.value) || 0;
                const monthly = parseFloat(document.getElementById('calcMonthly')?.value) || 0;
                const rate = parseFloat(document.getElementById('calcRate')?.value) || 0;
                const years = parseInt(document.getElementById('calcYears')?.value) || 0;
                drawCalcChart(initial, monthly, rate, years);
                calcChartResizeTimer = null;
            });
        });
        observer.observe(container);
  }

  function calculateInvestment() {
    const initial = parseFloat(document.getElementById('calcInitial')?.value) || 0;
    const monthly = parseFloat(document.getElementById('calcMonthly')?.value) || 0;
    const rate = parseFloat(document.getElementById('calcRate')?.value) || 0;
    const years = parseInt(document.getElementById('calcYears')?.value) || 0;

    const monthlyRate = rate / 100 / 12;
    const months = years * 12;

    let futureValue;
    if (monthlyRate === 0) {
      futureValue = initial + monthly * months;
    } else {
      futureValue = initial * Math.pow(1 + monthlyRate, months) + monthly * ((Math.pow(1 + monthlyRate, months) - 1) / monthlyRate);
    }

    const totalContributions = initial + monthly * months;
    const totalInterest = futureValue - totalContributions;

    const fvEl = document.getElementById('calcFutureValue');
    const tcEl = document.getElementById('calcTotalContributions');
    const tiEl = document.getElementById('calcTotalInterest');

    if (fvEl) fvEl.textContent = formatCurrency(futureValue);
    if (tcEl) tcEl.textContent = formatCurrency(totalContributions);
    if (tiEl) tiEl.textContent = formatCurrency(totalInterest);

    drawCalcChart(initial, monthly, rate, years);
  }

    function getCalcChartDimensions() {
        const canvas = document.getElementById('calcChart');
        if (!canvas) return null;
        const container = canvas.parentElement;
        if (!container) return null;
        const containerWidth = container.clientWidth - 24; // account for container padding
        const minWidth = 300;
        const maxWidth = 800;
        const w = Math.max(minWidth, Math.min(maxWidth, containerWidth));
        const h = Math.round(w * 0.7); // maintain aspect ratio
        return {w, h};
    }

  function drawCalcChart(initial, monthly, rate, years) {
    const canvas = document.getElementById('calcChart');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
      const dpr = window.devicePixelRatio || 1;
      const dims = getCalcChartDimensions();
      if (!dims) return;
      const w = dims.w;
      const h = dims.h;
      canvas.width = w * dpr;
      canvas.height = h * dpr;
    canvas.style.width = w + 'px';
    canvas.style.height = h + 'px';
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

      const padding = {top: 16, right: 24, bottom: 50, left: 68};
    const chartW = w - padding.left - padding.right;
    const chartH = h - padding.top - padding.bottom;

    ctx.clearRect(0, 0, w, h);

    // Generate data points
    const monthlyRate = rate / 100 / 12;
    const points = [];
    const contribPoints = [];
      const step = Math.max(1, Math.floor(years / 50));

    for (let y = 0; y <= years; y += step) {
      const m = y * 12;
      let fv;
      if (monthlyRate === 0) {
        fv = initial + monthly * m;
      } else {
        fv = initial * Math.pow(1 + monthlyRate, m) + monthly * ((Math.pow(1 + monthlyRate, m) - 1) / monthlyRate);
      }
      const contrib = initial + monthly * m;
      points.push({ x: y, y: fv });
      contribPoints.push({ x: y, y: contrib });
    }

    const maxVal = Math.max(...points.map(p => p.y), 1);
      const niceMax = niceScaleMax(maxVal);
      const gridLines = 5;

      // Helper: map data coords to canvas coords
      function toCanvasX(year) {
          return padding.left + (year / years) * chartW;
      }

      function toCanvasY(val) {
          return padding.top + chartH - (val / niceMax) * chartH;
      }

      // ---- Background ----
      ctx.fillStyle = '#fafbfc';
      roundRect(ctx, 0, 0, w, h, 8);
      ctx.fill();

      // ---- Grid lines (subtle dashed) ----
      ctx.strokeStyle = '#e8eaed';
      ctx.lineWidth = 1;
      ctx.setLineDash([4, 4]);
      for (let i = 0; i <= gridLines; i++) {
          const y = padding.top + (chartH / gridLines) * i;
      ctx.beginPath();
      ctx.moveTo(padding.left, y);
      ctx.lineTo(w - padding.right, y);
      ctx.stroke();

      // Y-axis labels
          const val = niceMax - (niceMax / gridLines) * i;
          ctx.fillStyle = '#6b7280';
          ctx.font = '11px system-ui, -apple-system, sans-serif';
      ctx.textAlign = 'right';
      ctx.textBaseline = 'middle';
          ctx.fillText(formatAxisValue(val), padding.left - 10, y);
    }
      ctx.setLineDash([]);

      // ---- X-axis labels ----
    ctx.textAlign = 'center';
    ctx.textBaseline = 'top';
      ctx.fillStyle = '#6b7280';
      ctx.font = '11px system-ui, -apple-system, sans-serif';
      const xStep = Math.max(1, Math.floor(years / 7));
    for (let y = 0; y <= years; y += xStep) {
        const x = toCanvasX(y);
        // Small tick mark
        const tickY = padding.top + chartH;
        ctx.beginPath();
        ctx.strokeStyle = '#d1d5db';
        ctx.lineWidth = 1;
        ctx.moveTo(x, tickY);
        ctx.lineTo(x, tickY + 4);
        ctx.stroke();

        ctx.fillText(y === 0 ? 'Now' : y + 'y', x, tickY + 8);
    }

      // ---- Smooth curve helper (Catmull-Rom spline) ----
      function drawSmoothCurve(data) {
          if (data.length < 2) return;
          const coords = data.map(p => ({x: toCanvasX(p.x), y: toCanvasY(p.y)}));

          ctx.beginPath();
          ctx.moveTo(coords[0].x, coords[0].y);

          for (let i = 0; i < coords.length - 1; i++) {
              const p0 = coords[Math.max(0, i - 1)];
              const p1 = coords[i];
              const p2 = coords[i + 1];
              const p3 = coords[Math.min(coords.length - 1, i + 2)];

              const cp1x = p1.x + (p2.x - p0.x) / 6;
              const cp1y = p1.y + (p2.y - p0.y) / 6;
              const cp2x = p2.x - (p3.x - p1.x) / 6;
              const cp2y = p2.y - (p3.y - p1.y) / 6;

              ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, p2.x, p2.y);
          }
      }

      // ---- Gradient fills ----
      // Contributions area (blue gradient)
      const contribGrad = ctx.createLinearGradient(0, padding.top, 0, padding.top + chartH);
      contribGrad.addColorStop(0, 'rgba(99, 132, 255, 0.25)');
      contribGrad.addColorStop(1, 'rgba(99, 132, 255, 0.02)');

      ctx.beginPath();
      ctx.moveTo(toCanvasX(0), padding.top + chartH);
      contribPoints.forEach(p => {
          ctx.lineTo(toCanvasX(p.x), toCanvasY(p.y));
    });
      ctx.lineTo(toCanvasX(years), padding.top + chartH);
    ctx.closePath();
      ctx.fillStyle = contribGrad;
    ctx.fill();

      // Total value area (green gradient)
      const valueGrad = ctx.createLinearGradient(0, padding.top, 0, padding.top + chartH);
      valueGrad.addColorStop(0, 'rgba(16, 185, 129, 0.3)');
      valueGrad.addColorStop(1, 'rgba(16, 185, 129, 0.02)');

    ctx.beginPath();
      ctx.moveTo(toCanvasX(0), padding.top + chartH);
      points.forEach(p => {
          ctx.lineTo(toCanvasX(p.x), toCanvasY(p.y));
    });
      ctx.lineTo(toCanvasX(years), padding.top + chartH);
    ctx.closePath();
      ctx.fillStyle = valueGrad;
      ctx.fill();

      // ---- Smooth lines ----
      // Contributions line (blue)
      drawSmoothCurve(contribPoints);
      ctx.strokeStyle = '#6366f1';
      ctx.lineWidth = 2.5;
      ctx.lineJoin = 'round';
      ctx.lineCap = 'round';
      ctx.stroke();

      // Total value line (green)
      drawSmoothCurve(points);
      ctx.strokeStyle = '#10b981';
      ctx.lineWidth = 2.5;
      ctx.lineJoin = 'round';
      ctx.lineCap = 'round';
      ctx.stroke();

      // ---- End-point dots ----
      function drawDot(data, color) {
          const last = data[data.length - 1];
          const cx = toCanvasX(last.x);
          const cy = toCanvasY(last.y);

          // Outer glow
          ctx.beginPath();
          ctx.arc(cx, cy, 6, 0, Math.PI * 2);
          ctx.fillStyle = color.replace(')', ', 0.2)').replace('rgb', 'rgba');
          ctx.fill();

          // Inner dot
      ctx.beginPath();
          ctx.arc(cx, cy, 3.5, 0, Math.PI * 2);
          ctx.fillStyle = color;
          ctx.fill();
          ctx.strokeStyle = '#fff';
          ctx.lineWidth = 1.5;
      ctx.stroke();
    }

      drawDot(contribPoints, '#6366f1');
      drawDot(points, '#10b981');

      // ---- Legend (top-right, pill style) ----
      const legendItems = [
          {label: 'Total Value', color: '#10b981'},
          {label: 'Contributions', color: '#6366f1'}
      ];
      const legendY = 8;
      const legendGap = 20;

      // Measure total width
      ctx.font = '600 10px system-ui, -apple-system, sans-serif';
      let totalLegendW = 0;
      const legendMeasures = legendItems.map(item => {
          const tw = ctx.measureText(item.label).width;
          totalLegendW += tw + 14 + legendGap;
          return tw;
      });
      totalLegendW -= legendGap;
      let legendX = w - totalLegendW - 8;

      legendItems.forEach((item, idx) => {
          const lx = legendX;

          // Dot
          ctx.beginPath();
          ctx.arc(lx + 5, legendY + 6, 4, 0, Math.PI * 2);
          ctx.fillStyle = item.color;
          ctx.fill();

          // Label
          ctx.fillStyle = '#374151';
          ctx.font = '600 10px system-ui, -apple-system, sans-serif';
          ctx.textAlign = 'left';
          ctx.textBaseline = 'middle';
          ctx.fillText(item.label, lx + 13, legendY + 6);

          legendX += legendMeasures[idx] + 14 + legendGap;
      });

      // ---- Store chart metadata for tooltip ----
      canvas._chartMeta = {padding, chartW, chartH, points, contribPoints, niceMax, years, toCanvasX, toCanvasY};
  }

    /**
     * Round a max value up to a "nice" number for axis scaling.
     */
    function niceScaleMax(rawMax) {
        if (rawMax <= 0) return 100;
        const magnitude = Math.pow(10, Math.floor(Math.log10(rawMax)));
        const normalized = rawMax / magnitude;
        let nice;
        if (normalized <= 1) nice = 1;
        else if (normalized <= 1.5) nice = 1.5;
        else if (normalized <= 2) nice = 2;
        else if (normalized <= 3) nice = 3;
        else if (normalized <= 5) nice = 5;
        else if (normalized <= 7.5) nice = 7.5;
        else nice = 10;
        return nice * magnitude;
    }

    /**
     * Format a currency value for axis labels (compact).
     */
    function formatAxisValue(val) {
        if (val >= 1000000) {
            return '$' + (val / 1000000).toFixed(1).replace(/\.0$/, '') + 'M';
        }
        if (val >= 1000) {
            return '$' + (val / 1000).toFixed(0) + 'k';
        }
        return '$' + val.toFixed(0);
    }

    /**
     * Draw a rounded rectangle path.
     */
    function roundRect(ctx, x, y, w, h, r) {
        ctx.beginPath();
        ctx.moveTo(x + r, y);
        ctx.lineTo(x + w - r, y);
        ctx.quadraticCurveTo(x + w, y, x + w, y + r);
        ctx.lineTo(x + w, y + h - r);
        ctx.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
        ctx.lineTo(x + r, y + h);
        ctx.quadraticCurveTo(x, y + h, x, y + h - r);
        ctx.lineTo(x, y + r);
        ctx.quadraticCurveTo(x, y, x + r, y);
        ctx.closePath();
    }

    // ---- Chart hover tooltip ----
    function setupCalcChartTooltip() {
        const canvas = document.getElementById('calcChart');
        if (!canvas) return;

        canvas.addEventListener('mousemove', (e) => {
            const meta = canvas._chartMeta;
            if (!meta) return;
            const rect = canvas.getBoundingClientRect();
            const mx = e.clientX - rect.left;
            const my = e.clientY - rect.top;

            const {padding, chartW, chartH, points, contribPoints, niceMax, years, toCanvasX, toCanvasY} = meta;

            // Check if mouse is inside chart area
            if (mx < padding.left || mx > padding.left + chartW || my < padding.top || my > padding.top + chartH) {
                calcChartTooltip.visible = false;
                redrawCalcChartWithTooltip();
                return;
            }

            // Find nearest year
            const ratio = (mx - padding.left) / chartW;
            const hoveredYear = Math.round(ratio * years);
            const clampedYear = Math.max(0, Math.min(years, hoveredYear));

            // Find matching data points
            const valuePoint = points.find(p => p.x === clampedYear) || nearestPoint(points, clampedYear);
            const contribPoint = contribPoints.find(p => p.x === clampedYear) || nearestPoint(contribPoints, clampedYear);

            if (!valuePoint) {
                calcChartTooltip.visible = false;
                redrawCalcChartWithTooltip();
                return;
            }

            const cx = toCanvasX(valuePoint.x);
            calcChartTooltip = {
                visible: true,
                x: cx,
                year: valuePoint.x,
                value: valuePoint.y,
                contribution: contribPoint ? contribPoint.y : 0
            };
            redrawCalcChartWithTooltip();
        });

        canvas.addEventListener('mouseleave', () => {
            calcChartTooltip.visible = false;
            redrawCalcChartWithTooltip();
        });
    }

    function nearestPoint(data, year) {
        let best = data[0];
        let bestDist = Math.abs(best.x - year);
        for (let i = 1; i < data.length; i++) {
            const d = Math.abs(data[i].x - year);
            if (d < bestDist) {
                bestDist = d;
                best = data[i];
            }
        }
        return best;
    }

    function redrawCalcChartWithTooltip() {
        // Re-read current values and redraw
        const initial = parseFloat(document.getElementById('calcInitial')?.value) || 0;
        const monthly = parseFloat(document.getElementById('calcMonthly')?.value) || 0;
        const rate = parseFloat(document.getElementById('calcRate')?.value) || 0;
        const years = parseInt(document.getElementById('calcYears')?.value) || 0;
        drawCalcChart(initial, monthly, rate, years);

        if (calcChartTooltip.visible) {
            drawCalcChartTooltip();
        }
    }

    function drawCalcChartTooltip() {
        const canvas = document.getElementById('calcChart');
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        const meta = canvas._chartMeta;
        if (!meta) return;

        const {toCanvasX, toCanvasY, padding, chartH} = meta;
        const {x: cx, year, value, contribution} = calcChartTooltip;

        // Vertical guide line
        ctx.beginPath();
        ctx.strokeStyle = 'rgba(107, 114, 128, 0.3)';
        ctx.lineWidth = 1;
        ctx.setLineDash([3, 3]);
        ctx.moveTo(cx, padding.top);
        ctx.lineTo(cx, padding.top + chartH);
        ctx.stroke();
        ctx.setLineDash([]);

        // Dot on value line
        const vy = toCanvasY(value);
        ctx.beginPath();
        ctx.arc(cx, vy, 5, 0, Math.PI * 2);
        ctx.fillStyle = '#10b981';
        ctx.fill();
        ctx.strokeStyle = '#fff';
        ctx.lineWidth = 2;
        ctx.stroke();

        // Dot on contribution line
        const cy = toCanvasY(contribution);
        ctx.beginPath();
        ctx.arc(cx, cy, 5, 0, Math.PI * 2);
        ctx.fillStyle = '#6366f1';
        ctx.fill();
        ctx.strokeStyle = '#fff';
        ctx.lineWidth = 2;
        ctx.stroke();

        // Tooltip box
        const lines = [
            year === 0 ? 'Start' : year + ' Years',
            'Value: ' + formatCurrency(value),
            'Contrib: ' + formatCurrency(contribution)
        ];

        ctx.font = '600 11px system-ui, -apple-system, sans-serif';
        const lineH = 18;
        const boxW = 150;
        const boxH = lines.length * lineH + 12;
        const boxPad = 10;

        let boxX = cx + 12;
        let boxY = vy - boxH / 2;

        // Keep tooltip inside chart bounds
        const canvasWidth = parseInt(canvas.style.width, 10) || 400;
        if (boxX + boxW > canvasWidth - padding.right) {
            boxX = cx - boxW - 12;
        }
        if (boxY < padding.top) {
            boxY = padding.top;
        }
        if (boxY + boxH > padding.top + chartH) {
            boxY = padding.top + chartH - boxH;
        }

        // Shadow
        ctx.shadowColor = 'rgba(0, 0, 0, 0.12)';
        ctx.shadowBlur = 12;
        ctx.shadowOffsetY = 4;

        // Background
        ctx.fillStyle = '#fff';
        roundRect(ctx, boxX, boxY, boxW, boxH, 8);
        ctx.fill();

        // Reset shadow
        ctx.shadowColor = 'transparent';
        ctx.shadowBlur = 0;
        ctx.shadowOffsetY = 0;

        // Border
        ctx.strokeStyle = '#e5e7eb';
        ctx.lineWidth = 1;
        roundRect(ctx, boxX, boxY, boxW, boxH, 8);
        ctx.stroke();

        // Text
        ctx.fillStyle = '#374151';
        ctx.textAlign = 'left';
        ctx.textBaseline = 'top';

        lines.forEach((line, i) => {
            const isTitle = i === 0;
            ctx.font = isTitle ? '700 11px system-ui, -apple-system, sans-serif' : '500 11px system-ui, -apple-system, sans-serif';
            ctx.fillStyle = isTitle ? '#111827' : '#4b5563';
            ctx.fillText(line, boxX + boxPad, boxY + boxPad + i * lineH);
        });
  }

  // =========================================================================
  // Watchlist
  // =========================================================================
  let watchlistItems = [];

  /**
   * Load the watchlist from the server and populate the savedArticleUrls set.
   * Called once on page init (silently) to know which articles are already saved.
   * @returns {Promise<void>}
   */
  async function loadWatchlistUrls() {
    try {
      const items = await apiFetch('/api/PersonalFinance/watchlist');
      watchlistItems = items;
      items.forEach(item => {
        if (item.url) savedArticleUrls.add(item.url);
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
  async function refreshWatchlist() {
    if (!window._watchlistLoaded) {
      window._watchlistLoaded = true;
    }
    const container = document.getElementById('watchlistGrid');
    if (!container) return;

    try {
      watchlistItems = await apiFetch('/api/PersonalFinance/watchlist');
      // Sync savedArticleUrls with latest server data
      savedArticleUrls.clear();
      watchlistItems.forEach(item => {
        if (item.url) savedArticleUrls.add(item.url);
      });
      renderWatchlist();
    } catch (err) {
      console.error('Load watchlist failed:', err);
      container.innerHTML = '<div class="watchlist-empty"><p>Failed to load watchlist.</p></div>';
    }
  }

  function renderWatchlist() {
    const container = document.getElementById('watchlistGrid');
    if (!container) return;

    if (watchlistItems.length === 0) {
      container.innerHTML = '<div class="watchlist-empty"><p>No saved articles yet. Use the "Save" button on news articles to add them here.</p></div>';
      return;
    }

    container.innerHTML = watchlistItems.map(item => {
      let imgHtml = '';
      if (item.imageUrl) {
        imgHtml = '<div class="watchlist-item-img"><img src="' + escapeHtml(item.imageUrl) + '" alt="" onerror="this.parentElement.style.display=\'none\'"/></div>';
      }
      return '<div class="watchlist-item" data-id="' + item.id + '" data-url="' + escapeHtml(item.url) + '">' +
        imgHtml +
        '<div class="watchlist-item-body">' +
        '<h4 class="watchlist-item-title"><a href="' + escapeHtml(item.url) + '" target="_blank" rel="noopener noreferrer">' + escapeHtml(item.title) + '</a></h4>' +
        '<div class="watchlist-item-meta">' +
        '<span class="finance-hub-badge source-badge">' + escapeHtml(item.source) + '</span>' +
        '<span class="finance-hub-badge category-badge">' + escapeHtml(item.category) + '</span>' +
        '</div>' +
        '<div class="watchlist-item-actions">' +
        '<a href="' + escapeHtml(item.url) + '" target="_blank" rel="noopener noreferrer" class="btn btn-sm btn-outline finance-hub-read-more">Read</a>' +
        '<button type="button" class="watchlist-remove-btn" title="Remove from watchlist">Remove</button>' +
        '</div></div></div>';
    }).join('');

    container.querySelectorAll('.watchlist-remove-btn').forEach(btn => {
      btn.addEventListener('click', async () => {
        const item = btn.closest('.watchlist-item');
        const id = parseInt(item.dataset.id, 10);
        const articleUrl = item.dataset.url;
        try {
          await apiFetch('/api/PersonalFinance/watchlist/' + id, { method: 'DELETE' });
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
