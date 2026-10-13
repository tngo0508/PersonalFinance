/**
 * Personal Finance Hub — Interactive features.
 * Tab switching, budget planner, savings goals, investment calculator, watchlist.
 */
(function () {
  'use strict';

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
      if (tabId === 'watchlist' && !window._watchlistLoaded) loadWatchlist();
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
    let imgHtml = '';
    if (article.imageUrl) {
      imgHtml = '<a href="' + escapeHtml(article.url) + '" class="finance-hub-card-img-link" target="_blank" rel="noopener noreferrer"><div class="finance-hub-card-img"><img src="' + escapeHtml(article.imageUrl) + '" alt="" loading="lazy" onerror="this.parentElement.parentElement.style.display=\'none\'"/></div></a>';
    }
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
      '<button type="button" class="btn btn-sm finance-hub-save-btn" data-title="' + escapeHtml(article.title) + '" data-url="' + escapeHtml(article.url) + '" data-source="' + escapeHtml(article.source) + '" data-category="' + escapeHtml(article.category) + '" data-image-url="' + escapeHtml(article.imageUrl || '') + '" title="Save to watchlist">' +
      '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z"/></svg> Save</button>' +
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
  function bindSaveButton(btn) {
    btn.addEventListener('click', async () => {
      const title = btn.dataset.title;
      const url = btn.dataset.url;
      const source = btn.dataset.source;
      const category = btn.dataset.category;
      const imageUrl = btn.dataset.imageUrl;

      try {
        const data = await apiFetch('/api/PersonalFinance/watchlist', {
          method: 'POST',
          body: JSON.stringify({ title, url, source, category, imageUrl })
        });

        if (data.success) {
          btn.classList.add('saved');
          btn.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor" stroke="currentColor" stroke-width="2"><path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z"/></svg> Saved';
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
      '<input type="range" class="form-range budget-category-slider" data-key="' + cat.key + '" min="0" max="100" value="' + cat.default + '/>' +
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
  let calcChartInstance = null;

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

  function drawCalcChart(initial, monthly, rate, years) {
    const canvas = document.getElementById('calcChart');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    const w = 400;
    const h = 250;
    canvas.width = w * 2;
    canvas.height = h * 2;
    canvas.style.width = w + 'px';
    canvas.style.height = h + 'px';
    ctx.scale(2, 2);

    const padding = { top: 20, right: 20, bottom: 40, left: 60 };
    const chartW = w - padding.left - padding.right;
    const chartH = h - padding.top - padding.bottom;

    ctx.clearRect(0, 0, w, h);

    // Generate data points
    const monthlyRate = rate / 100 / 12;
    const points = [];
    const contribPoints = [];
    const step = Math.max(1, Math.floor(years / 50)); // up to 50 points

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

    // Grid lines
    ctx.strokeStyle = '#e0e0e0';
    ctx.lineWidth = 0.5;
    for (let i = 0; i <= 4; i++) {
      const y = padding.top + (chartH / 4) * i;
      ctx.beginPath();
      ctx.moveTo(padding.left, y);
      ctx.lineTo(w - padding.right, y);
      ctx.stroke();

      // Y-axis labels
      const val = maxVal - (maxVal / 4) * i;
      ctx.fillStyle = '#888';
      ctx.font = '10px system-ui, sans-serif';
      ctx.textAlign = 'right';
      ctx.textBaseline = 'middle';
      ctx.fillText(formatCurrency(val), padding.left - 8, y);
    }

    // X-axis labels
    ctx.textAlign = 'center';
    ctx.textBaseline = 'top';
    const xStep = Math.max(1, Math.floor(years / 6));
    for (let y = 0; y <= years; y += xStep) {
      const x = padding.left + (y / years) * chartW;
      ctx.fillText(y + 'y', x, h - padding.bottom + 8);
    }

    // Draw contributions area
    ctx.beginPath();
    ctx.moveTo(padding.left, padding.top + chartH);
    contribPoints.forEach((p, i) => {
      const x = padding.left + (p.x / years) * chartW;
      const y = padding.top + chartH - (p.y / maxVal) * chartH;
      ctx.lineTo(x, y);
    });
    ctx.lineTo(padding.left + chartW, padding.top + chartH);
    ctx.closePath();
    ctx.fillStyle = 'rgba(0, 120, 212, 0.15)';
    ctx.fill();

    // Draw total value area
    ctx.beginPath();
    ctx.moveTo(padding.left, padding.top + chartH);
    points.forEach((p, i) => {
      const x = padding.left + (p.x / years) * chartW;
      const y = padding.top + chartH - (p.y / maxVal) * chartH;
      ctx.lineTo(x, y);
    });
    ctx.lineTo(padding.left + chartW, padding.top + chartH);
    ctx.closePath();
    ctx.fillStyle = 'rgba(16, 124, 16, 0.15)';
    ctx.fill();

    // Draw lines
    function drawLine(data, color) {
      ctx.beginPath();
      ctx.strokeStyle = color;
      ctx.lineWidth = 2;
      data.forEach((p, i) => {
        const x = padding.left + (p.x / years) * chartW;
        const y = padding.top + chartH - (p.y / maxVal) * chartH;
        if (i === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
      });
      ctx.stroke();
    }

    drawLine(contribPoints, '#0078d4');
    drawLine(points, '#107c10');

    // Legend
    ctx.font = '11px system-ui, sans-serif';
    ctx.textAlign = 'left';
    ctx.textBaseline = 'middle';

    ctx.fillStyle = '#107c10';
    ctx.fillRect(padding.left, h - 12, 12, 3);
    ctx.fillStyle = '#333';
    ctx.fillText('Total Value', padding.left + 16, h - 10);

    ctx.fillStyle = '#0078d4';
    ctx.fillRect(padding.left + 100, h - 12, 12, 3);
    ctx.fillStyle = '#333';
    ctx.fillText('Contributions', padding.left + 116, h - 10);
  }

  // =========================================================================
  // Watchlist
  // =========================================================================
  let watchlistItems = [];

  async function loadWatchlist() {
    window._watchlistLoaded = true;
    const container = document.getElementById('watchlistGrid');
    if (!container) return;

    try {
      watchlistItems = await apiFetch('/api/PersonalFinance/watchlist');
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
      return '<div class="watchlist-item" data-id="' + item.id + '">' +
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
        try {
          await apiFetch('/api/PersonalFinance/watchlist/' + id, { method: 'DELETE' });
          watchlistItems = watchlistItems.filter(a => a.id !== id);
          renderWatchlist();
        } catch (err) {
          console.error('Remove article failed:', err);
        }
      });
    });
  }

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
