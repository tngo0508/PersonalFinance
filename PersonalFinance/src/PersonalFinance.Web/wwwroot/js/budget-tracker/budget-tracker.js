/* global PFI18n */
(function () {
    'use strict';

    let currentYear = 2026;
    let currentMonth = 10;
    let currentSheet = null;
    let userCategories = [];
    let activeRequestId = 0;
    let isReordering = false;

    let modalAddCategoryInstance = null;
    let modalEditCategoryInstance = null;
    let modalActualOverrideInstance = null;
    let modalTransactionInstance = null;
    let modalDeleteConfirmInstance = null;

    let pendingDeleteAction = null;

    /**
     * Formats a decimal number into USD currency string with 2 decimal places.
     * @param {number|null} num - Number to format.
     * @returns {string} Formatted currency.
     */
    function formatCurrency (num) {
        return PFI18n.formatMoney(Number(num) || 0, 'USD');
    }

    /**
     * Formats a transaction's ISO date (yyyy-MM-dd) in the page culture, without shifting it across time zones.
     * @param {string} isoDate - The date as the server sends it.
     * @returns {string} The date in the culture, or the input when it is not an ISO date.
     */
    function formatTxDate (isoDate) {
        const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(isoDate || ''));
        if (!match) {
            return isoDate || '';
        }
        const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
        return PFI18n.formatDate(date, { year: 'numeric', month: '2-digit', day: '2-digit' });
    }

    /**
     * Names a month and year in the page culture, e.g. "September 2026" or "tháng 9 năm 2026".
     * @param {number} year - The year.
     * @param {number} month - The month, 1-12.
     * @returns {string} The month name and year.
     */
    function formatMonthYear (year, month) {
        return PFI18n.formatDate(new Date(year, month - 1, 1), { month: 'long', year: 'numeric' });
    }

    /**
     * Shows or hides the global loading spinner.
     * @param {boolean} show - Whether to display spinner.
     * @returns {void}
     */
    function setLoading (show) {
        const spinner = document.getElementById('btGlobalLoading');
        if (spinner) {
            spinner.classList.toggle('d-none', !show);
        }
    }

    /**
     * Displays a global error or status message banner.
     * @param {string|null} message - Message text.
     * @returns {void}
     */
    function showAlert (message) {
        const alertBox = document.getElementById('btAlert');
        const alertMsg = document.getElementById('btAlertMessage');
        if (!alertBox || !alertMsg) {
            return;
        }

        if (message) {
            alertMsg.textContent = message;
            alertBox.classList.remove('d-none');
            alertBox.classList.add('show');
        } else {
            alertBox.classList.add('d-none');
            alertBox.classList.remove('show');
        }
    }

    /**
     * Displays an in-modal error alert.
     * @param {string} alertId - The ID of the modal alert container.
     * @param {string|null} message - The error message, or null to hide.
     * @returns {void}
     */
    function showModalAlert (alertId, message) {
        const alertEl = document.getElementById(alertId);
        if (!alertEl) {
            return;
        }

        if (message) {
            alertEl.textContent = message;
            alertEl.classList.remove('d-none');
        } else {
            alertEl.textContent = '';
            alertEl.classList.add('d-none');
        }
    }

    /**
     * Initializes Lucide icon elements on the page.
     * @returns {void}
     */
    function refreshIcons () {
        if (window.lucide && typeof window.lucide.createIcons === 'function') {
            window.lucide.createIcons();
        }
    }

    /**
     * Creates a Lucide icon placeholder that refreshIcons() turns into an SVG.
     * @param {string} name - Lucide icon name.
     * @param {number} size - Width and height in pixels.
     * @returns {HTMLElement} The icon element.
     */
    function iconElement (name, size) {
        const icon = document.createElement('i');
        icon.dataset.lucide = name;
        icon.style.width = `${size}px`;
        icon.style.height = `${size}px`;
        return icon;
    }

    /**
     * Fetches the complete sheet for the given year and month.
     * @param {number} year - The target year.
     * @param {number} month - The target month.
     * @returns {Promise<void>}
     */
    async function loadSheet (year, month) {
        const requestId = ++activeRequestId;
        setLoading(true);
        showAlert(null);

        try {
            const response = await fetch(`/BudgetTracker/GetSheet?year=${year}&month=${month}`, {
                method: 'GET',
                headers: {
                    'Accept': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (!response.ok) {
                const errData = await response.json().catch(() => {
                    return {};
                });
                throw new Error(errData.error || PFI18n.t('BudgetTracker.Js.LoadFailedStatus', { status: response.status }));
            }

            const data = await response.json();
            if (requestId !== activeRequestId) {
                return;
            }

            currentYear = year;
            currentMonth = month;
            currentSheet = data;

            renderMonthSelector();
            renderSheet(data);
        } catch (err) {
            if (requestId === activeRequestId) {
                showAlert(err.message || PFI18n.t('BudgetTracker.Js.LoadFailed'));
                renderMonthSelector();
            }
        } finally {
            if (requestId === activeRequestId) {
                setLoading(false);
            }
        }
    }

    /**
     * Loads the user's active categories for transaction and modal dropdowns.
     * @returns {Promise<void>}
     */
    async function loadCategories () {
        try {
            const response = await fetch('/BudgetTracker/GetCategories?includeArchived=false', {
                method: 'GET',
                headers: {
                    'Accept': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (response.ok) {
                userCategories = await response.json();
            }
        } catch (_) {
            // Non-blocking background load
        }
    }

    /**
     * Synchronizes month and year dropdown selectors with current state.
     * @returns {void}
     */
    function renderMonthSelector () {
        const selMonth = document.getElementById('selectMonth');
        const selYear = document.getElementById('selectYear');
        if (selMonth) {
            selMonth.value = String(currentMonth);
        }
        if (selYear) {
            let optionExists = false;
            for (let i = 0; i < selYear.options.length; i++) {
                if (selYear.options[i].value === String(currentYear)) {
                    optionExists = true;
                    break;
                }
            }
            if (!optionExists) {
                const opt = document.createElement('option');
                opt.value = String(currentYear);
                opt.textContent = String(currentYear);
                selYear.appendChild(opt);
            }
            selYear.value = String(currentYear);
        }
    }

    /**
     * Renders the complete budget sheet into the DOM.
     * @param {Object} sheet - The MonthlyBudgetSheetDto object.
     * @returns {void}
     */
    function renderSheet (sheet) {
        renderInitPrompt(sheet);
        renderBars(sheet);
        renderSummaryTable('tbodyExpenses', sheet.expenses ? sheet.expenses.rows : [], 1);
        renderSummaryTable('tbodyIncome', sheet.income ? sheet.income.rows : [], 2);
        renderTotals(sheet);
        renderTransactions(sheet.transactions || []);
        refreshIcons();
    }

    /**
     * Applies and renders an updated sheet if matching current month view.
     * @param {Object} updatedSheet - Updated MonthlyBudgetSheetDto.
     * @returns {void}
     */
    function applyUpdatedSheet (updatedSheet) {
        if (updatedSheet && updatedSheet.year === currentYear && updatedSheet.month === currentMonth) {
            currentSheet = updatedSheet;
            renderSheet(updatedSheet);
        } else {
            loadSheet(currentYear, currentMonth);
        }
    }

    /**
     * Renders the new month initialization prompt banner.
     * @param {Object} sheet - Monthly budget sheet.
     * @returns {void}
     */
    function renderInitPrompt (sheet) {
        const promptBanner = document.getElementById('btInitPrompt');
        const btnCopy = document.getElementById('btnCopyPrevious');
        const btnCopyText = document.getElementById('btnCopyPreviousText');
        const desc = document.getElementById('initPromptDesc');

        if (!promptBanner) {
            return;
        }

        if (sheet.isInitialized) {
            promptBanner.classList.add('d-none');
            return;
        }

        promptBanner.classList.remove('d-none');

        if (sheet.canCopyFromPrevious && sheet.previousPlanYear && sheet.previousPlanMonth) {
            const previousMonth = formatMonthYear(sheet.previousPlanYear, sheet.previousPlanMonth);
            if (btnCopy) {
                btnCopy.classList.remove('d-none');
            }
            if (btnCopyText) {
                btnCopyText.textContent = PFI18n.t('BudgetTracker.Js.CopyFrom', { month: previousMonth });
            }
            if (desc) {
                desc.textContent = PFI18n.t('BudgetTracker.Js.InitPromptCopy', { month: previousMonth });
            }
        } else {
            if (btnCopy) {
                btnCopy.classList.add('d-none');
            }
            if (desc) {
                desc.textContent = PFI18n.t('BudgetTracker.Js.InitPromptBlank');
            }
        }
    }

    /**
     * Renders the horizontal Planned vs Actual comparison bars.
     * @param {Object} sheet - Monthly budget sheet.
     * @returns {void}
     */
    function renderBars (sheet) {
        const expPlanned = sheet.expenses ? sheet.expenses.totalPlanned : 0;
        const expActual = sheet.expenses ? sheet.expenses.totalActual : 0;
        const incPlanned = sheet.income ? sheet.income.totalPlanned : 0;
        const incActual = sheet.income ? sheet.income.totalActual : 0;

        // Expenses Bar
        const expNumbers = document.getElementById('expensesBarNumbers');
        const expBarPlanned = document.getElementById('expensePlannedBar');
        const expBarActual = document.getElementById('expenseActualBar');

        if (expNumbers) {
            expNumbers.textContent = PFI18n.t('BudgetTracker.Js.BarNumbers', {
                planned: formatCurrency(expPlanned),
                actual: formatCurrency(expActual)
            });
        }

        const expMax = Math.max(expPlanned, expActual, 1);
        if (expBarPlanned) {
            const pct = Math.min(100, Math.max(0, (expPlanned / expMax) * 100));
            expBarPlanned.style.width = `${pct.toFixed(1) }%`;
        }
        if (expBarActual) {
            const pct = Math.min(100, Math.max(0, (expActual / expMax) * 100));
            expBarActual.style.width = `${pct.toFixed(1) }%`;
        }

        // Income Bar
        const incNumbers = document.getElementById('incomeBarNumbers');
        const incBarPlanned = document.getElementById('incomePlannedBar');
        const incBarActual = document.getElementById('incomeActualBar');

        if (incNumbers) {
            incNumbers.textContent = PFI18n.t('BudgetTracker.Js.BarNumbers', {
                planned: formatCurrency(incPlanned),
                actual: formatCurrency(incActual)
            });
        }

        const incMax = Math.max(incPlanned, incActual, 1);
        if (incBarPlanned) {
            const pct = Math.min(100, Math.max(0, (incPlanned / incMax) * 100));
            incBarPlanned.style.width = `${pct.toFixed(1) }%`;
        }
        if (incBarActual) {
            const pct = Math.min(100, Math.max(0, (incActual / incMax) * 100));
            incBarActual.style.width = `${pct.toFixed(1) }%`;
        }
    }

    /**
     * Renders a category summary table (Expenses or Income).
     * @param {string} tbodyId - Target tbody element ID.
     * @param {Array} rows - List of SheetRowDto objects.
     * @param {number} kind - 1 for Expense, 2 for Income.
     * @returns {void}
     */
    function renderSummaryTable (tbodyId, rows, kind) {
        const tbody = document.getElementById(tbodyId);
        if (!tbody) {
            return;
        }

        tbody.textContent = '';

        if (!rows || rows.length === 0) {
            const tr = document.createElement('tr');
            const td = document.createElement('td');
            td.colSpan = 5;
            td.className = 'text-center text-muted py-3';
            td.textContent = PFI18n.t('BudgetTracker.Js.NoCategories');
            tr.appendChild(td);
            tbody.appendChild(tr);
            return;
        }

        const isMonthInitialized = Boolean(currentSheet && currentSheet.isInitialized);
        const activeRows = rows.filter((r) => {
            return !r.isArchived;
        });

        rows.forEach((row) => {
            const tr = document.createElement('tr');
            tr.dataset.categoryId = String(row.categoryId);

            // Category name cell
            const tdName = document.createElement('td');
            const nameSpan = document.createElement('span');
            nameSpan.className = 'bt-category-name';
            nameSpan.textContent = row.name;
            tdName.appendChild(nameSpan);

            if (row.isArchived) {
                const badge = document.createElement('span');
                badge.className = 'bt-archived-badge ms-1';
                badge.textContent = PFI18n.t('BudgetTracker.Js.Archived');
                tdName.appendChild(badge);
            }
            tr.appendChild(tdName);

            // Planned amount cell (interactive inline edit button)
            const tdPlanned = document.createElement('td');
            tdPlanned.className = 'text-end bt-planned-cell';

            const btnPlanned = document.createElement('button');
            btnPlanned.type = 'button';
            btnPlanned.className = 'bt-planned-btn';
            btnPlanned.textContent = formatCurrency(row.planned);
            btnPlanned.setAttribute('aria-label', PFI18n.t('BudgetTracker.Js.EditPlannedAria', { name: row.name }));
            btnPlanned.title = isMonthInitialized
                ? PFI18n.t('BudgetTracker.Js.EditPlannedTitle')
                : PFI18n.t('BudgetTracker.Js.InitializeFirst');
            btnPlanned.disabled = !isMonthInitialized;

            btnPlanned.addEventListener('click', (e) => {
                e.preventDefault();
                if (isMonthInitialized) {
                    startInlinePlannedEdit(tdPlanned, row.categoryId, row.planned, row.name);
                }
            });

            tdPlanned.appendChild(btnPlanned);
            tr.appendChild(tdPlanned);

            // Actual amount cell with Override badge and Revert button
            const tdActual = document.createElement('td');
            tdActual.className = 'text-end';

            const actualWrapper = document.createElement('div');
            actualWrapper.className = 'bt-actual-cell';

            if (row.actualIsOverride) {
                const manualBadge = document.createElement('span');
                manualBadge.className = 'bt-manual-badge';
                manualBadge.textContent = PFI18n.t('BudgetTracker.Js.ManualBadge');
                manualBadge.title = PFI18n.t('BudgetTracker.Js.ManualBadgeTitle');
                actualWrapper.appendChild(manualBadge);
            }

            const actualVal = document.createElement('span');
            actualVal.textContent = formatCurrency(row.actual);
            actualWrapper.appendChild(actualVal);

            // Button to open override modal
            const btnOverride = document.createElement('button');
            btnOverride.type = 'button';
            btnOverride.className = 'bt-btn-icon';
            btnOverride.title = row.actualIsOverride
                ? PFI18n.t('BudgetTracker.Js.OverrideEditTitle')
                : PFI18n.t('BudgetTracker.Js.OverrideSetTitle');
            btnOverride.setAttribute('aria-label', row.actualIsOverride
                ? PFI18n.t('BudgetTracker.Js.OverrideEditAria', { name: row.name })
                : PFI18n.t('BudgetTracker.Js.OverrideSetAria', { name: row.name }));
            btnOverride.appendChild(iconElement('edit-3', 13));
            btnOverride.disabled = !isMonthInitialized;
            btnOverride.addEventListener('click', (e) => {
                e.stopPropagation();
                if (isMonthInitialized) {
                    openActualOverrideModal(row);
                }
            });
            actualWrapper.appendChild(btnOverride);

            // Revert override button (if manual override exists)
            if (row.actualIsOverride) {
                const btnRevert = document.createElement('button');
                btnRevert.type = 'button';
                btnRevert.className = 'bt-btn-icon';
                btnRevert.title = PFI18n.t('BudgetTracker.Js.RevertTitle');
                btnRevert.setAttribute('aria-label', PFI18n.t('BudgetTracker.Js.RevertAria', { name: row.name }));
                btnRevert.appendChild(iconElement('rotate-ccw', 13));
                btnRevert.addEventListener('click', async (e) => {
                    e.stopPropagation();
                    await revertActualOverride(row.categoryId);
                });
                actualWrapper.appendChild(btnRevert);
            }

            tdActual.appendChild(actualWrapper);
            tr.appendChild(tdActual);

            // Diff cell
            const tdDiff = document.createElement('td');
            tdDiff.className = 'text-end';
            tdDiff.textContent = formatCurrency(row.diff);

            if (row.diff > 0) {
                tdDiff.classList.add('bt-diff-positive');
            } else if (row.diff < 0) {
                tdDiff.classList.add('bt-diff-negative');
            } else {
                tdDiff.classList.add('bt-diff-neutral');
            }
            tr.appendChild(tdDiff);

            // Actions cell (Move Up, Move Down, Rename, Archive)
            const tdActions = document.createElement('td');
            tdActions.className = 'text-end';

            const actionsWrapper = document.createElement('div');
            actionsWrapper.className = 'bt-category-actions';

            if (!row.isArchived) {
                const activeIndex = activeRows.indexOf(row);

                // Move Up button
                const btnMoveUp = document.createElement('button');
                btnMoveUp.type = 'button';
                btnMoveUp.className = 'bt-btn-icon bt-move-btn';
                btnMoveUp.title = PFI18n.t('BudgetTracker.Js.MoveUpTitle');
                btnMoveUp.setAttribute('aria-label', PFI18n.t('BudgetTracker.Js.MoveUpAria', { name: row.name }));
                btnMoveUp.appendChild(iconElement('arrow-up', 13));
                btnMoveUp.dataset.boundary = String(activeIndex === 0);
                btnMoveUp.disabled = isReordering || activeIndex === 0;
                btnMoveUp.addEventListener('click', async (e) => {
                    e.stopPropagation();
                    await handleReorderCategory(row, -1, kind);
                });
                actionsWrapper.appendChild(btnMoveUp);

                // Move Down button
                const btnMoveDown = document.createElement('button');
                btnMoveDown.type = 'button';
                btnMoveDown.className = 'bt-btn-icon bt-move-btn';
                btnMoveDown.title = PFI18n.t('BudgetTracker.Js.MoveDownTitle');
                btnMoveDown.setAttribute('aria-label', PFI18n.t('BudgetTracker.Js.MoveDownAria', { name: row.name }));
                btnMoveDown.appendChild(iconElement('arrow-down', 13));
                btnMoveDown.dataset.boundary = String(activeIndex === activeRows.length - 1);
                btnMoveDown.disabled = isReordering || activeIndex === activeRows.length - 1;
                btnMoveDown.addEventListener('click', async (e) => {
                    e.stopPropagation();
                    await handleReorderCategory(row, 1, kind);
                });
                actionsWrapper.appendChild(btnMoveDown);

                // Rename button
                const btnRename = document.createElement('button');
                btnRename.type = 'button';
                btnRename.className = 'bt-btn-icon text-primary';
                btnRename.title = PFI18n.t('BudgetTracker.Js.RenameTitle');
                btnRename.setAttribute('aria-label', PFI18n.t('BudgetTracker.Js.RenameAria', { name: row.name }));
                btnRename.appendChild(iconElement('edit-2', 13));
                btnRename.addEventListener('click', (e) => {
                    e.stopPropagation();
                    openEditCategoryModal(row, kind);
                });
                actionsWrapper.appendChild(btnRename);

                // Archive button
                const btnDelCat = document.createElement('button');
                btnDelCat.type = 'button';
                btnDelCat.className = 'bt-btn-icon text-danger';
                btnDelCat.title = PFI18n.t('BudgetTracker.Js.ArchiveTitle');
                btnDelCat.setAttribute('aria-label', PFI18n.t('BudgetTracker.Js.ArchiveAria', { name: row.name }));
                btnDelCat.appendChild(iconElement('trash-2', 13));
                btnDelCat.addEventListener('click', (e) => {
                    e.stopPropagation();
                    confirmArchiveCategory(row);
                });
                actionsWrapper.appendChild(btnDelCat);
            }

            tdActions.appendChild(actionsWrapper);
            tr.appendChild(tdActions);
            tbody.appendChild(tr);
        });
    }

    /**
     * Renders section totals in summary table footers.
     * @param {Object} sheet - Monthly budget sheet.
     * @returns {void}
     */
    function renderTotals (sheet) {
        const expPlanned = sheet.expenses ? sheet.expenses.totalPlanned : 0;
        const expActual = sheet.expenses ? sheet.expenses.totalActual : 0;
        const expDiff = sheet.expenses ? sheet.expenses.totalDiff : 0;

        const incPlanned = sheet.income ? sheet.income.totalPlanned : 0;
        const incActual = sheet.income ? sheet.income.totalActual : 0;
        const incDiff = sheet.income ? sheet.income.totalDiff : 0;

        const elTotExpPlan = document.getElementById('totalExpensePlanned');
        const elTotExpAct = document.getElementById('totalExpenseActual');
        const elTotExpDiff = document.getElementById('totalExpenseDiff');

        const elTotIncPlan = document.getElementById('totalIncomePlanned');
        const elTotIncAct = document.getElementById('totalIncomeActual');
        const elTotIncDiff = document.getElementById('totalIncomeDiff');

        if (elTotExpPlan) {
            elTotExpPlan.textContent = formatCurrency(expPlanned);
        }
        if (elTotExpAct) {
            elTotExpAct.textContent = formatCurrency(expActual);
        }
        if (elTotExpDiff) {
            elTotExpDiff.textContent = formatCurrency(expDiff);
            applyDiffClass(elTotExpDiff, expDiff);
        }

        if (elTotIncPlan) {
            elTotIncPlan.textContent = formatCurrency(incPlanned);
        }
        if (elTotIncAct) {
            elTotIncAct.textContent = formatCurrency(incActual);
        }
        if (elTotIncDiff) {
            elTotIncDiff.textContent = formatCurrency(incDiff);
            applyDiffClass(elTotIncDiff, incDiff);
        }
    }

    /**
     * Applies the appropriate diff styling class to an element.
     * @param {HTMLElement} el - Target element.
     * @param {number} diff - Diff amount.
     * @returns {void}
     */
    function applyDiffClass (el, diff) {
        el.classList.remove('bt-diff-positive', 'bt-diff-negative', 'bt-diff-neutral');
        if (diff > 0) {
            el.classList.add('bt-diff-positive');
        } else if (diff < 0) {
            el.classList.add('bt-diff-negative');
        } else {
            el.classList.add('bt-diff-neutral');
        }
    }

    /**
     * Starts inline editing for a planned amount cell.
     * @param {HTMLElement} cell - The table cell.
     * @param {number} categoryId - The category ID.
     * @param {number} currentVal - The current planned amount.
     * @param {string} categoryName - Category name for labels.
     * @returns {void}
     */
    function startInlinePlannedEdit (cell, categoryId, currentVal, categoryName) {
        const input = document.createElement('input');
        input.type = 'number';
        input.step = '0.01';
        input.min = '0';
        input.max = '9999999999999999.99';
        input.className = 'form-control form-control-sm bt-inline-input';
        input.value = (Number(currentVal) || 0).toFixed(2);
        input.setAttribute('aria-label', PFI18n.t('BudgetTracker.Js.PlannedInputAria', { name: categoryName }));

        cell.textContent = '';
        cell.appendChild(input);
        input.focus();
        input.select();

        let isSaved = false;

        async function savePlanned () {
            if (isSaved) {
                return;
            }
            isSaved = true;

            const newVal = parseFloat(input.value);
            const originalVal = Number(currentVal) || 0;

            // If invalid value
            if (isNaN(newVal) || newVal < 0) {
                showAlert(PFI18n.t('BudgetTracker.Js.PlannedInvalid'));
                renderSheet(currentSheet);
                return;
            }

            // Skip server call if value is unchanged
            if (Math.abs(newVal - originalVal) < 0.0001) {
                renderSheet(currentSheet);
                return;
            }

            // Do not save if month is not initialized
            if (!currentSheet || !currentSheet.isInitialized) {
                renderSheet(currentSheet);
                return;
            }

            try {
                setLoading(true);
                const response = await fetch(
                    `/BudgetTracker/SetPlanned?year=${currentYear}&month=${currentMonth}&categoryId=${categoryId}`,
                    {
                        method: 'PUT',
                        headers: {
                            'Content-Type': 'application/json',
                            'RequestVerificationToken': window.PFUtils.getAntiForgeryToken(),
                            'X-Requested-With': 'XMLHttpRequest'
                        },
                        body: JSON.stringify({ amount: newVal })
                    }
                );

                if (!response.ok) {
                    const err = await response.json().catch(() => {
                        return {};
                    });
                    throw new Error(err.error || PFI18n.t('BudgetTracker.Js.PlannedFailed'));
                }

                const updatedSheet = await response.json();
                applyUpdatedSheet(updatedSheet);
            } catch (err) {
                showAlert(err.message || PFI18n.t('BudgetTracker.Js.PlannedFailed'));
                renderSheet(currentSheet);
            } finally {
                setLoading(false);
            }
        }

        input.addEventListener('blur', () => {
            savePlanned();
        });

        input.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                input.blur();
            } else if (e.key === 'Escape') {
                e.preventDefault();
                isSaved = true;
                renderSheet(currentSheet);
            }
        });
    }

    /**
     * Renders side-by-side expense and income transactions.
     * @param {Array} transactions - List of BudgetTransactionDto objects.
     * @returns {void}
     */
    function renderTransactions (transactions) {
        const txList = transactions || [];
        const expenseTxs = txList.filter((t) => {
            return t.kind === 1;
        });
        const incomeTxs = txList.filter((t) => {
            return t.kind === 2;
        });

        const txCountBadge = document.getElementById('txCountBadge');
        const expCountBadge = document.getElementById('expenseTxCountBadge');
        const incCountBadge = document.getElementById('incomeTxCountBadge');

        if (txCountBadge) {
            txCountBadge.textContent = String(txList.length);
        }
        if (expCountBadge) {
            expCountBadge.textContent = String(expenseTxs.length);
        }
        if (incCountBadge) {
            incCountBadge.textContent = String(incomeTxs.length);
        }

        renderTransactionTable('tbodyExpenseTx', 'emptyExpenseTx', expenseTxs, 1);
        renderTransactionTable('tbodyIncomeTx', 'emptyIncomeTx', incomeTxs, 2);
    }

    /**
     * Renders a specific transaction table (Expense or Income).
     * @param {string} tbodyId - Tbody element ID.
     * @param {string} emptyId - Empty state container ID.
     * @param {Array} list - Filtered list of transactions.
     * @param {number} kind - 1 for Expense, 2 for Income.
     * @returns {void}
     */
    function renderTransactionTable (tbodyId, emptyId, list, kind) {
        const tbody = document.getElementById(tbodyId);
        const emptyState = document.getElementById(emptyId);

        if (!tbody) {
            return;
        }

        tbody.textContent = '';

        if (!list || list.length === 0) {
            if (emptyState) {
                emptyState.classList.remove('d-none');
            }
            return;
        }

        if (emptyState) {
            emptyState.classList.add('d-none');
        }

        list.forEach((tx) => {
            const tr = document.createElement('tr');

            // Date
            const tdDate = document.createElement('td');
            tdDate.textContent = formatTxDate(tx.date);
            tr.appendChild(tdDate);

            // Amount
            const tdAmount = document.createElement('td');
            tdAmount.className = `text-end bt-tx-amount ${ kind === 1 ? 'bt-tx-amount--expense' : 'bt-tx-amount--income'}`;
            tdAmount.textContent = formatCurrency(tx.amount);
            tr.appendChild(tdAmount);

            // Description
            const tdDesc = document.createElement('td');
            tdDesc.textContent = tx.description || '—';
            tr.appendChild(tdDesc);

            // Category
            const tdCat = document.createElement('td');
            tdCat.textContent = tx.categoryName;
            tr.appendChild(tdCat);

            // Actions (Edit, Delete)
            const tdActions = document.createElement('td');
            tdActions.className = 'text-end';

            const btnEdit = document.createElement('button');
            btnEdit.type = 'button';
            btnEdit.className = 'bt-btn-icon text-primary me-1';
            btnEdit.title = PFI18n.t('BudgetTracker.Js.EditTransactionTitle');
            btnEdit.setAttribute('aria-label', PFI18n.t('BudgetTracker.Js.EditTransactionAria', { name: tx.description || tx.categoryName }));
            btnEdit.appendChild(iconElement('edit-2', 14));
            btnEdit.addEventListener('click', () => {
                openEditTransactionModal(tx);
            });
            tdActions.appendChild(btnEdit);

            const btnDel = document.createElement('button');
            btnDel.type = 'button';
            btnDel.className = 'bt-btn-icon text-danger';
            btnDel.title = PFI18n.t('BudgetTracker.Js.DeleteTransactionTitle');
            btnDel.setAttribute('aria-label', PFI18n.t('BudgetTracker.Js.DeleteTransactionAria', { name: tx.description || tx.categoryName }));
            btnDel.appendChild(iconElement('trash-2', 14));
            btnDel.addEventListener('click', () => {
                confirmDeleteTransaction(tx);
            });
            tdActions.appendChild(btnDel);

            tr.appendChild(tdActions);
            tbody.appendChild(tr);
        });
    }

    /**
     * Reorders category up or down within its kind list.
     * @param {Object} row - Category row DTO.
     * @param {number} direction - -1 for up, 1 for down.
     * @param {number} kind - 1 Expense, 2 Income.
     * @returns {Promise<void>}
     */
    async function handleReorderCategory (row, direction, kind) {
        if (isReordering) {
            return;
        }

        const sameKindCats = userCategories.filter((c) => {
            return c.kind === kind;
        });
        const currentIndex = sameKindCats.findIndex((c) => {
            return c.id === row.categoryId;
        });
        const targetIndex = currentIndex + direction;

        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= sameKindCats.length) {
            return;
        }

        const updates = buildReorderUpdates(sameKindCats, currentIndex, targetIndex);

        setReordering(true);
        setLoading(true);

        let failureMessage = null;
        try {
            // Run every update to completion (even if one fails) so the refresh below sees a settled server state.
            const results = await Promise.allSettled(updates.map((u) => {
                return putCategorySortOrder(u.category, u.sortOrder);
            }));
            const failed = results.find((r) => {
                return r.status === 'rejected';
            });
            if (failed) {
                failureMessage = failed.reason && failed.reason.message
                    ? failed.reason.message
                    : PFI18n.t('BudgetTracker.Js.ReorderFailed');
            }
        } catch (err) {
            failureMessage = err && err.message ? err.message : PFI18n.t('BudgetTracker.Js.ReorderFailed');
        }

        try {
            // Refresh on success and on partial failure so the client never keeps stale ordering.
            await loadCategories();
            await loadSheet(currentYear, currentMonth);
        } catch (_) {
            // loadSheet reports its own errors
        } finally {
            setReordering(false);
            setLoading(false);
        }

        if (failureMessage) {
            showAlert(failureMessage);
        }
    }

    /**
     * Computes the sort-order updates needed to swap two neighbouring active categories.
     * Swaps the stored values; when both are tied, renumbers the whole kind deterministically.
     * @param {Array<Object>} cats - Active categories of one kind in display order.
     * @param {number} currentIndex - Index of the category being moved.
     * @param {number} targetIndex - Index of the neighbour it swaps with.
     * @returns {Array<{category: Object, sortOrder: number}>} Updates to send.
     */
    function buildReorderUpdates (cats, currentIndex, targetIndex) {
        const sourceCat = cats[currentIndex];
        const targetCat = cats[targetIndex];

        if (sourceCat.sortOrder !== targetCat.sortOrder) {
            return [
                { category: sourceCat, sortOrder: targetCat.sortOrder },
                { category: targetCat, sortOrder: sourceCat.sortOrder }
            ];
        }

        const updates = [];
        cats.forEach((c, i) => {
            let assignedOrder = (i + 1) * 10;
            if (i === currentIndex) {
                assignedOrder = (targetIndex + 1) * 10;
            } else if (i === targetIndex) {
                assignedOrder = (currentIndex + 1) * 10;
            }
            if (c.sortOrder !== assignedOrder || i === currentIndex || i === targetIndex) {
                updates.push({ category: c, sortOrder: assignedOrder });
            }
        });
        return updates;
    }

    /**
     * Persists a new sort order for a category, preserving its name and kind.
     * @param {Object} category - Category DTO.
     * @param {number} sortOrder - New sort order.
     * @returns {Promise<void>}
     * @throws {Error} When the server rejects the update.
     */
    async function putCategorySortOrder (category, sortOrder) {
        const response = await fetch(`/BudgetTracker/UpdateCategory?id=${category.id}`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': window.PFUtils.getAntiForgeryToken(),
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: JSON.stringify({
                name: category.name,
                kind: category.kind,
                sortOrder: sortOrder
            })
        });

        if (!response.ok) {
            const err = await response.json().catch(() => {
                return {};
            });
            throw new Error(err.error || PFI18n.t('BudgetTracker.Js.ReorderFailed'));
        }
    }

    /**
     * Marks a reorder as started or finished and syncs the move controls accordingly.
     * @param {boolean} active - True while a reorder is in progress.
     * @returns {void}
     */
    function setReordering (active) {
        isReordering = active;
        setReorderControlsDisabled(active);
    }

    /**
     * Disables or restores all move up/down controls while a reorder is in progress.
     * Boundary buttons (first up / last down) stay disabled when restored.
     * @param {boolean} disabled - True to disable every move control.
     * @returns {void}
     */
    function setReorderControlsDisabled (disabled) {
        document.querySelectorAll('.bt-move-btn').forEach((btn) => {
            btn.disabled = disabled || btn.dataset.boundary === 'true';
        });
    }

    /**
     * Opens modal to edit/rename a category.
     * @param {Object} row - Category row DTO.
     * @param {number} kind - Category kind.
     * @returns {void}
     */
    function openEditCategoryModal (row, kind) {
        showModalAlert('alertEditCategory', null);
        const idInput = document.getElementById('editCategoryId');
        const sortInput = document.getElementById('editCategorySortOrder');
        const kindInput = document.getElementById('editCategoryKind');
        const nameInput = document.getElementById('inputEditCategoryName');
        const kindSelect = document.getElementById('selectEditCategoryKind');

        const cat = userCategories.find((c) => {
            return c.id === row.categoryId;
        });
        const currentSortOrder = cat ? cat.sortOrder : 0;
        const currentKind = cat ? cat.kind : kind;

        if (idInput) {
            idInput.value = String(row.categoryId);
        }
        if (sortInput) {
            sortInput.value = String(currentSortOrder);
        }
        if (kindInput) {
            kindInput.value = String(currentKind);
        }
        if (nameInput) {
            nameInput.value = row.name;
        }
        if (kindSelect) {
            kindSelect.value = String(currentKind);
        }

        if (modalEditCategoryInstance) {
            modalEditCategoryInstance.show();
        }
    }

    /**
     * Handles updating category from edit modal submission.
     * @returns {Promise<void>}
     */
    async function handleEditCategory () {
        const idInput = document.getElementById('editCategoryId');
        const sortInput = document.getElementById('editCategorySortOrder');
        const kindInput = document.getElementById('editCategoryKind');
        const nameInput = document.getElementById('inputEditCategoryName');
        const kindSelect = document.getElementById('selectEditCategoryKind');
        const btnSubmit = document.getElementById('btnSubmitEditCategory');

        const catId = idInput ? parseInt(idInput.value, 10) : 0;
        const sortOrder = sortInput ? parseInt(sortInput.value, 10) : 0;
        const name = nameInput ? nameInput.value.trim() : '';
        const kind = kindInput ? parseInt(kindInput.value, 10) : (kindSelect ? parseInt(kindSelect.value, 10) : 1);

        if (!name) {
            showModalAlert('alertEditCategory', PFI18n.t('BudgetTracker.Js.CategoryNameRequired'));
            return;
        }

        try {
            if (btnSubmit) {
                btnSubmit.disabled = true;
            }
            setLoading(true);
            showModalAlert('alertEditCategory', null);

            const response = await fetch(`/BudgetTracker/UpdateCategory?id=${catId}`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': window.PFUtils.getAntiForgeryToken(),
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({
                    name: name,
                    kind: kind,
                    sortOrder: sortOrder
                })
            });

            if (!response.ok) {
                const err = await response.json().catch(() => {
                    return {};
                });
                throw new Error(err.error || PFI18n.t('BudgetTracker.Js.UpdateCategoryFailed'));
            }

            if (modalEditCategoryInstance) {
                modalEditCategoryInstance.hide();
            }

            await loadCategories();
            await loadSheet(currentYear, currentMonth);
        } catch (err) {
            showModalAlert('alertEditCategory', err.message || PFI18n.t('BudgetTracker.Js.UpdateCategoryFailed'));
        } finally {
            if (btnSubmit) {
                btnSubmit.disabled = false;
            }
            setLoading(false);
        }
    }

    /**
     * Opens the Actual Override modal.
     * @param {Object} row - Category row DTO.
     * @returns {void}
     */
    function openActualOverrideModal (row) {
        showModalAlert('alertActualOverride', null);
        const catIdInput = document.getElementById('overrideCategoryId');
        const catNameLabel = document.getElementById('overrideCategoryName');
        const amountInput = document.getElementById('inputOverrideAmount');
        const btnRevert = document.getElementById('btnModalRevertOverride');

        if (catIdInput) {
            catIdInput.value = String(row.categoryId);
        }
        if (catNameLabel) {
            catNameLabel.textContent = row.name;
        }
        if (amountInput) {
            amountInput.value = (Number(row.actual) || 0).toFixed(2);
        }

        if (btnRevert) {
            btnRevert.classList.toggle('d-none', !row.actualIsOverride);
            btnRevert.onclick = async function () {
                if (modalActualOverrideInstance) {
                    modalActualOverrideInstance.hide();
                }
                await revertActualOverride(row.categoryId);
            };
        }

        if (modalActualOverrideInstance) {
            modalActualOverrideInstance.show();
        }
    }

    /**
     * Clears a manual actual override for a category.
     * @param {number} categoryId - Category ID.
     * @returns {Promise<void>}
     */
    async function revertActualOverride (categoryId) {
        try {
            setLoading(true);
            const response = await fetch(
                `/BudgetTracker/SetActualOverride?year=${currentYear}&month=${currentMonth}&categoryId=${categoryId}`,
                {
                    method: 'PUT',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': window.PFUtils.getAntiForgeryToken(),
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: JSON.stringify({ amount: null })
                }
            );

            if (!response.ok) {
                const err = await response.json().catch(() => {
                    return {};
                });
                throw new Error(err.error || PFI18n.t('BudgetTracker.Js.RevertFailed'));
            }

            const updatedSheet = await response.json();
            applyUpdatedSheet(updatedSheet);
        } catch (err) {
            showAlert(err.message || PFI18n.t('BudgetTracker.Js.RevertFailed'));
        } finally {
            setLoading(false);
        }
    }

    /**
     * Opens modal to add a new transaction.
     * @param {number|null} defaultKind - Default kind (1 Expense, 2 Income).
     * @returns {void}
     */
    function openAddTransactionModal (defaultKind) {
        showModalAlert('alertTransaction', null);
        const title = document.getElementById('modalTransactionTitle');
        const idInput = document.getElementById('inputTxId');
        const catSelect = document.getElementById('selectTxCategory');
        const dateInput = document.getElementById('inputTxDate');
        const amountInput = document.getElementById('inputTxAmount');
        const descInput = document.getElementById('inputTxDescription');

        if (title) {
            title.textContent = PFI18n.t('BudgetTracker.Js.AddTransaction');
        }
        if (idInput) {
            idInput.value = '';
        }
        if (amountInput) {
            amountInput.value = '';
        }
        if (descInput) {
            descInput.value = '';
        }

        // Default date to current local date if current month matches, otherwise 1st of viewed month
        const today = new Date();
        const yearStr = String(today.getFullYear());
        const monthStr = String(today.getMonth() + 1).padStart(2, '0');
        const dayStr = String(today.getDate()).padStart(2, '0');
        const isCurrentMonth = today.getFullYear() === currentYear && (today.getMonth() + 1) === currentMonth;
        const defaultDateStr = isCurrentMonth
            ? `${yearStr}-${monthStr}-${dayStr}`
            : `${currentYear}-${String(currentMonth).padStart(2, '0')}-01`;

        if (dateInput) {
            dateInput.value = defaultDateStr;
        }

        populateCategoryDropdown(catSelect, null, defaultKind);

        if (modalTransactionInstance) {
            modalTransactionInstance.show();
        }
    }

    /**
     * Opens modal to edit an existing transaction.
     * @param {Object} tx - Transaction DTO.
     * @returns {void}
     */
    function openEditTransactionModal (tx) {
        showModalAlert('alertTransaction', null);
        const title = document.getElementById('modalTransactionTitle');
        const idInput = document.getElementById('inputTxId');
        const catSelect = document.getElementById('selectTxCategory');
        const dateInput = document.getElementById('inputTxDate');
        const amountInput = document.getElementById('inputTxAmount');
        const descInput = document.getElementById('inputTxDescription');

        if (title) {
            title.textContent = PFI18n.t('BudgetTracker.Js.EditTransaction');
        }
        if (idInput) {
            idInput.value = String(tx.id);
        }
        if (dateInput) {
            dateInput.value = tx.date;
        }
        if (amountInput) {
            amountInput.value = (Number(tx.amount) || 0).toFixed(2);
        }
        if (descInput) {
            descInput.value = tx.description || '';
        }

        populateCategoryDropdown(catSelect, tx.categoryId, tx.kind);

        if (modalTransactionInstance) {
            modalTransactionInstance.show();
        }
    }

    /**
     * Populates the category selector in the transaction modal.
     * @param {HTMLSelectElement} selectEl - Target select element.
     * @param {number|null} selectedId - Currently selected category ID.
     * @param {number|null} _filterKind - Optional kind filter.
     * @returns {void}
     */
    function populateCategoryDropdown (selectEl, selectedId, _filterKind) {
        if (!selectEl) {
            return;
        }

        selectEl.textContent = '';

        const defaultOpt = document.createElement('option');
        defaultOpt.value = '';
        defaultOpt.textContent = PFI18n.t('BudgetTracker.Js.SelectCategory');
        selectEl.appendChild(defaultOpt);

        const expGroup = document.createElement('optgroup');
        expGroup.label = PFI18n.t('BudgetTracker.Js.Expenses');

        const incGroup = document.createElement('optgroup');
        incGroup.label = PFI18n.t('BudgetTracker.Js.Income');

        userCategories.forEach((cat) => {
            const opt = document.createElement('option');
            opt.value = String(cat.id);
            opt.textContent = cat.name;
            if (selectedId && cat.id === selectedId) {
                opt.selected = true;
            }

            if (cat.kind === 1) {
                expGroup.appendChild(opt);
            } else {
                incGroup.appendChild(opt);
            }
        });

        if (expGroup.children.length > 0) {
            selectEl.appendChild(expGroup);
        }
        if (incGroup.children.length > 0) {
            selectEl.appendChild(incGroup);
        }
    }

    /**
     * Prompts for confirmation to archive a category.
     * @param {Object} row - Category row.
     * @returns {void}
     */
    function confirmArchiveCategory (row) {
        showModalAlert('alertDeleteConfirm', null);
        const msg = document.getElementById('deleteConfirmMessage');
        const title = document.getElementById('modalDeleteConfirmTitle');
        const btnDelete = document.getElementById('btnConfirmDelete');

        if (title) {
            title.textContent = PFI18n.t('BudgetTracker.Js.ArchiveCategory');
        }
        if (btnDelete) {
            btnDelete.textContent = PFI18n.t('BudgetTracker.Js.Archive');
        }
        if (msg) {
            msg.textContent = PFI18n.t('BudgetTracker.Js.ArchiveConfirm', { name: row.name });
        }

        pendingDeleteAction = async function () {
            try {
                if (btnDelete) {
                    btnDelete.disabled = true;
                }
                setLoading(true);
                const response = await fetch(`/BudgetTracker/DeleteCategory?id=${row.categoryId}`, {
                    method: 'DELETE',
                    headers: {
                        'RequestVerificationToken': window.PFUtils.getAntiForgeryToken(),
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                if (!response.ok) {
                    const err = await response.json().catch(() => {
                        return {};
                    });
                    throw new Error(err.error || PFI18n.t('BudgetTracker.Js.ArchiveFailed'));
                }

                pendingDeleteAction = null;
                if (modalDeleteConfirmInstance) {
                    modalDeleteConfirmInstance.hide();
                }

                await loadCategories();
                await loadSheet(currentYear, currentMonth);
            } catch (err) {
                showModalAlert('alertDeleteConfirm', err.message || PFI18n.t('BudgetTracker.Js.ArchiveFailed'));
            } finally {
                if (btnDelete) {
                    btnDelete.disabled = false;
                }
                setLoading(false);
            }
        };

        if (modalDeleteConfirmInstance) {
            modalDeleteConfirmInstance.show();
        }
    }

    /**
     * Prompts for confirmation to delete a transaction.
     * @param {Object} tx - Transaction DTO.
     * @returns {void}
     */
    function confirmDeleteTransaction (tx) {
        showModalAlert('alertDeleteConfirm', null);
        const msg = document.getElementById('deleteConfirmMessage');
        const title = document.getElementById('modalDeleteConfirmTitle');
        const btnDelete = document.getElementById('btnConfirmDelete');

        if (title) {
            title.textContent = PFI18n.t('BudgetTracker.Js.DeleteTransaction');
        }
        if (btnDelete) {
            btnDelete.textContent = PFI18n.t('BudgetTracker.Js.Delete');
        }
        if (msg) {
            msg.textContent = PFI18n.t('BudgetTracker.Js.DeleteTransactionConfirm', {
                name: tx.description || tx.categoryName,
                amount: formatCurrency(tx.amount)
            });
        }

        pendingDeleteAction = async function () {
            try {
                if (btnDelete) {
                    btnDelete.disabled = true;
                }
                setLoading(true);
                const response = await fetch(`/BudgetTracker/DeleteTransaction?id=${tx.id}`, {
                    method: 'DELETE',
                    headers: {
                        'RequestVerificationToken': window.PFUtils.getAntiForgeryToken(),
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                if (!response.ok) {
                    const err = await response.json().catch(() => {
                        return {};
                    });
                    throw new Error(err.error || PFI18n.t('BudgetTracker.Js.DeleteTransactionFailed'));
                }

                pendingDeleteAction = null;
                if (modalDeleteConfirmInstance) {
                    modalDeleteConfirmInstance.hide();
                }

                await loadSheet(currentYear, currentMonth);
            } catch (err) {
                showModalAlert('alertDeleteConfirm', err.message || PFI18n.t('BudgetTracker.Js.DeleteTransactionFailed'));
            } finally {
                if (btnDelete) {
                    btnDelete.disabled = false;
                }
                setLoading(false);
            }
        };

        if (modalDeleteConfirmInstance) {
            modalDeleteConfirmInstance.show();
        }
    }

    /**
     * Attaches DOM event listeners.
     * @returns {void}
     */
    function setupEventListeners () {
        // Month Navigation
        const btnPrev = document.getElementById('btnPrevMonth');
        const btnNext = document.getElementById('btnNextMonth');
        const selMonth = document.getElementById('selectMonth');
        const selYear = document.getElementById('selectYear');

        if (btnPrev) {
            btnPrev.addEventListener('click', () => {
                let m = currentMonth - 1;
                let y = currentYear;
                if (m < 1) {
                    m = 12;
                    y -= 1;
                }
                loadSheet(y, m);
            });
        }

        if (btnNext) {
            btnNext.addEventListener('click', () => {
                let m = currentMonth + 1;
                let y = currentYear;
                if (m > 12) {
                    m = 1;
                    y += 1;
                }
                loadSheet(y, m);
            });
        }

        if (selMonth) {
            selMonth.addEventListener('change', () => {
                loadSheet(currentYear, parseInt(selMonth.value, 10));
            });
        }

        if (selYear) {
            selYear.addEventListener('change', () => {
                loadSheet(parseInt(selYear.value, 10), currentMonth);
            });
        }

        // Alert dismiss
        const btnDismiss = document.getElementById('btnDismissAlert');
        if (btnDismiss) {
            btnDismiss.addEventListener('click', () => {
                showAlert(null);
            });
        }

        // New month prompt buttons
        const btnCopyPrev = document.getElementById('btnCopyPrevious');
        const btnStartBlank = document.getElementById('btnStartBlank');

        if (btnCopyPrev) {
            btnCopyPrev.addEventListener('click', async () => {
                await initializeMonth(true);
            });
        }

        if (btnStartBlank) {
            btnStartBlank.addEventListener('click', async () => {
                await initializeMonth(false);
            });
        }

        // Add Category Buttons
        const btnAddExpCat = document.getElementById('btnAddExpenseCategory');
        const btnAddIncCat = document.getElementById('btnAddIncomeCategory');
        const formAddCat = document.getElementById('formAddCategory');
        const inputCatName = document.getElementById('inputCategoryName');
        const selCatKind = document.getElementById('selectCategoryKind');

        if (btnAddExpCat) {
            btnAddExpCat.addEventListener('click', () => {
                showModalAlert('alertAddCategory', null);
                if (inputCatName) {
                    inputCatName.value = '';
                }
                if (selCatKind) {
                    selCatKind.value = '1';
                }
                if (modalAddCategoryInstance) {
                    modalAddCategoryInstance.show();
                }
            });
        }

        if (btnAddIncCat) {
            btnAddIncCat.addEventListener('click', () => {
                showModalAlert('alertAddCategory', null);
                if (inputCatName) {
                    inputCatName.value = '';
                }
                if (selCatKind) {
                    selCatKind.value = '2';
                }
                if (modalAddCategoryInstance) {
                    modalAddCategoryInstance.show();
                }
            });
        }

        if (formAddCat) {
            formAddCat.addEventListener('submit', async (e) => {
                e.preventDefault();
                await handleCreateCategory();
            });
        }

        // Edit Category Form
        const formEditCat = document.getElementById('formEditCategory');
        if (formEditCat) {
            formEditCat.addEventListener('submit', async (e) => {
                e.preventDefault();
                await handleEditCategory();
            });
        }

        // Actual Override Form
        const formOverride = document.getElementById('formActualOverride');
        if (formOverride) {
            formOverride.addEventListener('submit', async (e) => {
                e.preventDefault();
                await handleSaveActualOverride();
            });
        }

        // Transactions Add button & Form
        const btnAddTx = document.getElementById('btnAddTransaction');
        const formTx = document.getElementById('formTransaction');

        if (btnAddTx) {
            btnAddTx.addEventListener('click', () => {
                openAddTransactionModal(null);
            });
        }

        if (formTx) {
            formTx.addEventListener('submit', async (e) => {
                e.preventDefault();
                await handleSaveTransaction();
            });
        }

        // Delete confirmation button
        const btnConfirmDel = document.getElementById('btnConfirmDelete');
        if (btnConfirmDel) {
            btnConfirmDel.addEventListener('click', async () => {
                if (pendingDeleteAction) {
                    await pendingDeleteAction();
                }
            });
        }

        const modalDelEl = document.getElementById('modalDeleteConfirm');
        if (modalDelEl) {
            modalDelEl.addEventListener('hidden.bs.modal', () => {
                pendingDeleteAction = null;
                showModalAlert('alertDeleteConfirm', null);
            });
        }
    }

    /**
     * Initializes the month with or without copying from previous month.
     * @param {boolean} copyFromPrevious - Whether to copy planned amounts.
     * @returns {Promise<void>}
     */
    async function initializeMonth (copyFromPrevious) {
        const btnCopy = document.getElementById('btnCopyPrevious');
        const btnBlank = document.getElementById('btnStartBlank');

        try {
            if (btnCopy) {
                btnCopy.disabled = true;
            }
            if (btnBlank) {
                btnBlank.disabled = true;
            }
            setLoading(true);

            const response = await fetch('/BudgetTracker/InitializeMonth', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': window.PFUtils.getAntiForgeryToken(),
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({
                    year: currentYear,
                    month: currentMonth,
                    copyFromPrevious: copyFromPrevious
                })
            });

            if (!response.ok) {
                const err = await response.json().catch(() => {
                    return {};
                });
                throw new Error(err.error || PFI18n.t('BudgetTracker.Js.InitializeFailed'));
            }

            const updatedSheet = await response.json();
            applyUpdatedSheet(updatedSheet);
        } catch (err) {
            showAlert(err.message || PFI18n.t('BudgetTracker.Js.InitializeFailed'));
        } finally {
            if (btnCopy) {
                btnCopy.disabled = false;
            }
            if (btnBlank) {
                btnBlank.disabled = false;
            }
            setLoading(false);
        }
    }

    /**
     * Handles adding a new category from modal submission.
     * @returns {Promise<void>}
     */
    async function handleCreateCategory () {
        const nameInput = document.getElementById('inputCategoryName');
        const kindSelect = document.getElementById('selectCategoryKind');
        const btnSubmit = document.getElementById('btnSubmitCategory');

        const name = nameInput ? nameInput.value.trim() : '';
        const kind = kindSelect ? parseInt(kindSelect.value, 10) : 1;

        if (!name) {
            showModalAlert('alertAddCategory', PFI18n.t('BudgetTracker.Js.CategoryNameRequired'));
            return;
        }

        // Append to the end of the same category kind
        const sameKindCats = userCategories.filter((c) => {
            return c.kind === kind;
        });
        let maxSortOrder = 0;
        sameKindCats.forEach((c) => {
            if (c.sortOrder > maxSortOrder) {
                maxSortOrder = c.sortOrder;
            }
        });
        const newSortOrder = maxSortOrder + 10;

        try {
            if (btnSubmit) {
                btnSubmit.disabled = true;
            }
            setLoading(true);
            showModalAlert('alertAddCategory', null);

            const response = await fetch('/BudgetTracker/CreateCategory', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': window.PFUtils.getAntiForgeryToken(),
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({
                    name: name,
                    kind: kind,
                    sortOrder: newSortOrder
                })
            });

            if (!response.ok) {
                const err = await response.json().catch(() => {
                    return {};
                });
                throw new Error(err.error || PFI18n.t('BudgetTracker.Js.CreateCategoryFailed'));
            }

            if (modalAddCategoryInstance) {
                modalAddCategoryInstance.hide();
            }

            await loadCategories();
            await loadSheet(currentYear, currentMonth);
        } catch (err) {
            showModalAlert('alertAddCategory', err.message || PFI18n.t('BudgetTracker.Js.CreateCategoryFailed'));
        } finally {
            if (btnSubmit) {
                btnSubmit.disabled = false;
            }
            setLoading(false);
        }
    }

    /**
     * Handles setting manual actual override from modal submission.
     * @returns {Promise<void>}
     */
    async function handleSaveActualOverride () {
        const catIdInput = document.getElementById('overrideCategoryId');
        const amountInput = document.getElementById('inputOverrideAmount');
        const btnSubmit = document.getElementById('btnSubmitOverride');

        const catId = catIdInput ? parseInt(catIdInput.value, 10) : 0;
        const amount = amountInput ? parseFloat(amountInput.value) : 0;

        if (isNaN(amount) || amount < 0) {
            showModalAlert('alertActualOverride', PFI18n.t('BudgetTracker.Js.OverrideInvalid'));
            return;
        }

        try {
            if (btnSubmit) {
                btnSubmit.disabled = true;
            }
            setLoading(true);
            showModalAlert('alertActualOverride', null);

            const response = await fetch(
                `/BudgetTracker/SetActualOverride?year=${currentYear}&month=${currentMonth}&categoryId=${catId}`,
                {
                    method: 'PUT',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': window.PFUtils.getAntiForgeryToken(),
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: JSON.stringify({ amount: amount })
                }
            );

            if (!response.ok) {
                const err = await response.json().catch(() => {
                    return {};
                });
                throw new Error(err.error || PFI18n.t('BudgetTracker.Js.OverrideFailed'));
            }

            if (modalActualOverrideInstance) {
                modalActualOverrideInstance.hide();
            }

            const updatedSheet = await response.json();
            applyUpdatedSheet(updatedSheet);
        } catch (err) {
            showModalAlert('alertActualOverride', err.message || PFI18n.t('BudgetTracker.Js.OverrideFailed'));
        } finally {
            if (btnSubmit) {
                btnSubmit.disabled = false;
            }
            setLoading(false);
        }
    }

    /**
     * Handles saving (create or update) a transaction from modal submission.
     * @returns {Promise<void>}
     */
    async function handleSaveTransaction () {
        const idInput = document.getElementById('inputTxId');
        const catSelect = document.getElementById('selectTxCategory');
        const dateInput = document.getElementById('inputTxDate');
        const amountInput = document.getElementById('inputTxAmount');
        const descInput = document.getElementById('inputTxDescription');
        const btnSubmit = document.getElementById('btnSubmitTransaction');

        const txId = idInput && idInput.value ? parseInt(idInput.value, 10) : null;
        const categoryId = catSelect ? parseInt(catSelect.value, 10) : 0;
        const date = dateInput ? dateInput.value : '';
        const amount = amountInput ? parseFloat(amountInput.value) : 0;
        const desc = descInput ? descInput.value.trim() : '';

        if (!categoryId) {
            showModalAlert('alertTransaction', PFI18n.t('BudgetTracker.Js.CategoryRequired'));
            return;
        }
        if (!date) {
            showModalAlert('alertTransaction', PFI18n.t('BudgetTracker.Js.DateRequired'));
            return;
        }
        if (isNaN(amount) || amount <= 0) {
            showModalAlert('alertTransaction', PFI18n.t('BudgetTracker.Js.AmountInvalid'));
            return;
        }

        try {
            if (btnSubmit) {
                btnSubmit.disabled = true;
            }
            setLoading(true);
            showModalAlert('alertTransaction', null);

            const payload = {
                categoryId: categoryId,
                date: date,
                amount: amount,
                description: desc
            };

            const url = txId
                ? `/BudgetTracker/UpdateTransaction?id=${txId}`
                : '/BudgetTracker/CreateTransaction';
            const method = txId ? 'PUT' : 'POST';

            const response = await fetch(url, {
                method: method,
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': window.PFUtils.getAntiForgeryToken(),
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                const err = await response.json().catch(() => {
                    return {};
                });
                throw new Error(err.error || PFI18n.t('BudgetTracker.Js.SaveTransactionFailed'));
            }

            if (modalTransactionInstance) {
                modalTransactionInstance.hide();
            }

            // Refresh the current sheet view
            await loadSheet(currentYear, currentMonth);
        } catch (err) {
            showModalAlert('alertTransaction', err.message || PFI18n.t('BudgetTracker.Js.SaveTransactionFailed'));
        } finally {
            if (btnSubmit) {
                btnSubmit.disabled = false;
            }
            setLoading(false);
        }
    }

    /**
     * Initializes the Budget Tracker UI on DOM ready.
     * @returns {void}
     */
    function init () {
        const container = document.getElementById('budgetTrackerContainer');
        if (!container) {
            return;
        }

        currentYear = parseInt(container.dataset.year, 10) || 2026;
        currentMonth = parseInt(container.dataset.month, 10) || 10;

        // Initialize Bootstrap Modals
        const elModalCat = document.getElementById('modalAddCategory');
        if (elModalCat && typeof bootstrap !== 'undefined') {
            modalAddCategoryInstance = new bootstrap.Modal(elModalCat);
        }

        const elModalEditCat = document.getElementById('modalEditCategory');
        if (elModalEditCat && typeof bootstrap !== 'undefined') {
            modalEditCategoryInstance = new bootstrap.Modal(elModalEditCat);
        }

        const elModalOverride = document.getElementById('modalActualOverride');
        if (elModalOverride && typeof bootstrap !== 'undefined') {
            modalActualOverrideInstance = new bootstrap.Modal(elModalOverride);
        }

        const elModalTx = document.getElementById('modalTransaction');
        if (elModalTx && typeof bootstrap !== 'undefined') {
            modalTransactionInstance = new bootstrap.Modal(elModalTx);
        }

        const elModalDel = document.getElementById('modalDeleteConfirm');
        if (elModalDel && typeof bootstrap !== 'undefined') {
            modalDeleteConfirmInstance = new bootstrap.Modal(elModalDel);
        }

        setupEventListeners();

        // Load initial categories
        loadCategories();

        if (window.__initialSheetData) {
            currentSheet = window.__initialSheetData;
            renderMonthSelector();
            renderSheet(currentSheet);
        } else {
            loadSheet(currentYear, currentMonth);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
