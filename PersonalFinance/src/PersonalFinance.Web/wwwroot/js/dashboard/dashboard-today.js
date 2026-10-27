/**
 * Dashboard Today section (#412).
 * Draws the user's clock and the current month in the browser's time zone, in the page culture, and puts a value on
 * the time: a tape of the day's hours, what the user's net worth has earned by compounding since midnight, the days
 * left to FI, and how far through the month and the month's budget the user is. Money lines appear only when the
 * dashboard data has the figures behind them. The clock ticks every second; the tape and the month meters move on the
 * minute; when the day changes the month is redrawn.
 */
(function () {
    'use strict';

    const CURRENCY = 'USD';
    const HOURS_IN_DAY = 24;

    /**
     * Weeks of a month, each seven day numbers with null for the cells outside the month.
     * @param {number} year - Full year.
     * @param {number} month - Month, 0 (January) to 11.
     * @param {number} firstDay - Weekday the week starts on, 0 (Sunday) to 6.
     * @returns {Array<Array<number|null>>} The weeks, top to bottom.
     */
    function monthWeeks (year, month, firstDay) {
        const length = daysInMonth(year, month);
        const offset = (new Date(year, month, 1).getDay() - firstDay + 7) % 7;
        const weeks = [];
        for (let start = 1 - offset; start <= length; start += 7) {
            const week = [];
            for (let day = start; day < start + 7; day++) {
                week.push(day >= 1 && day <= length ? day : null);
            }
            weeks.push(week);
        }
        return weeks;
    }

    /**
     * Number of days in a month.
     * @param {number} year - Full year.
     * @param {number} month - Month, 0 to 11.
     * @returns {number} 28 to 31.
     */
    function daysInMonth (year, month) {
        return new Date(year, month + 1, 0).getDate();
    }

    /**
     * Weekday a culture starts its week on, from Intl.Locale week info where the browser has it; otherwise Sunday for
     * the United States and Monday elsewhere.
     * @param {string} culture - BCP 47 culture, e.g. "en-US" or "vi-VN".
     * @returns {number} 0 (Sunday) to 6.
     */
    function firstDayOfWeek (culture) {
        try {
            const locale = new Intl.Locale(culture);
            const info = typeof locale.getWeekInfo === 'function' ? locale.getWeekInfo() : locale.weekInfo;
            if (info && info.firstDay) {
                return info.firstDay % 7;
            }
        } catch (error) {
            // Unknown culture or no Intl.Locale: use the fallback below.
        }
        return /-US$/i.test(culture) ? 0 : 1;
    }

    /**
     * Local calendar day of a date as yyyy-mm-dd.
     * @param {Date} date - The date.
     * @returns {string} The day key.
     */
    function dayKey (date) {
        const pad = (value) => String(value).padStart(2, '0');
        return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
    }

    /**
     * Days with recent activity, read from the date part of each item so a time zone never moves it to another day.
     * @param {Array<{date: string}>} activity - The dashboard's recentActivity.
     * @returns {Set<string>} yyyy-mm-dd keys.
     */
    function activityDays (activity) {
        const days = new Set();
        (Array.isArray(activity) ? activity : []).forEach((item) => {
            const match = /^\d{4}-\d{2}-\d{2}/.exec(item && item.date ? String(item.date) : '');
            if (match) {
                days.add(match[0]);
            }
        });
        return days;
    }

    /**
     * Share of the local day already gone, measured from midnight to the next midnight so a daylight-saving day of 23
     * or 25 hours still ends at 1.
     * @param {Date} now - The current time.
     * @returns {number} 0 (midnight) up to 1.
     */
    function dayFraction (now) {
        const midnight = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        const next = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1);
        return (now - midnight) / (next - midnight);
    }

    /**
     * Share of the month already gone, counting today's elapsed part.
     * @param {Date} now - The current time.
     * @returns {number} 0 up to 1.
     */
    function monthFraction (now) {
        return (now.getDate() - 1 + dayFraction(now)) / daysInMonth(now.getFullYear(), now.getMonth());
    }

    /**
     * What a net worth earns in one day at an annual return, compounded daily so 365 days give exactly the annual
     * return. Null when there is nothing positive to compound: no net worth, a negative one, or no positive return.
     * @param {number} netWorth - Current net worth.
     * @param {number} annualReturnPercent - Annual return in percent, e.g. 7 for 7%.
     * @returns {number|null} The day's growth.
     */
    function dailyGrowth (netWorth, annualReturnPercent) {
        const worth = Number(netWorth);
        const rate = Number(annualReturnPercent);
        if (!(worth > 0) || !(rate > 0)) {
            return null;
        }
        return worth * (Math.pow(1 + rate / 100, 1 / 365) - 1);
    }

    /**
     * Approximate days until FI from the whole years the Base projection needs. Null when FI is reached or never is.
     * @param {number|null} fiYears - Years from the current age to the age the Base projection reaches FI.
     * @param {boolean} reached - Whether the FI Target is already reached.
     * @returns {number|null} The days.
     */
    function daysToFi (fiYears, reached) {
        if (reached || !(Number(fiYears) > 0)) {
            return null;
        }
        return Math.round(Number(fiYears) * 365.25);
    }

    /**
     * How this month's spending compares with how much of the month is gone. Null unless the monthly budget is for
     * the user's current month and plans some expenses, so another month's figures never pass for this one's.
     * @param {{year: number, month: number, budgetedExpenses: number, actualExpenses: number}|null} budget - The
     *     dashboard's monthlyBudget (month 1 to 12).
     * @param {Date} now - The current time.
     * @returns {{spentShare: number, monthShare: number, remaining: number, perDay: number, daysLeft: number,
     *     status: string}|null} status is "onPace", "ahead" (spending faster than the month) or "over" (past the plan).
     */
    function budgetPace (budget, now) {
        if (!budget || budget.year !== now.getFullYear() || budget.month !== now.getMonth() + 1) {
            return null;
        }
        const planned = Number(budget.budgetedExpenses);
        if (!(planned > 0)) {
            return null;
        }
        const spent = Math.max(0, Number(budget.actualExpenses) || 0);
        const spentShare = spent / planned;
        const monthShare = monthFraction(now);
        const remaining = planned - spent;
        // Today counts: its budget is still there to spend.
        const daysLeft = daysInMonth(now.getFullYear(), now.getMonth()) - now.getDate() + 1;
        let status = 'onPace';
        if (spent > planned) {
            status = 'over';
        } else if (spentShare > monthShare) {
            status = 'ahead';
        }
        return {
            spentShare: spentShare,
            monthShare: monthShare,
            remaining: remaining,
            perDay: remaining > 0 ? remaining / daysLeft : 0,
            daysLeft: daysLeft,
            status: status
        };
    }

    /**
     * A typical day of the Net Worth settings: the year's income, spending and savings over 365 days, and the day's
     * compounding. freedomHours is the time of living a day's saving pays for later (savings over spending, in
     * hours). Each figure is null when the settings do not have a positive value for it.
     * @param {Object|null} netWorth - The dashboard's netWorth summary.
     * @returns {{earned: number|null, spent: number|null, saved: number|null, compounding: number|null,
     *     freedomHours: number|null}} The day's figures.
     */
    function dayLedger (netWorth) {
        const perDay = (annual) => (Number(annual) > 0 ? Number(annual) / 365 : null);
        const worth = netWorth || {};
        const spent = perDay(worth.annualSpending);
        const saved = perDay(worth.annualSavings);
        return {
            earned: perDay(worth.annualIncome),
            spent: spent,
            saved: saved,
            compounding: dailyGrowth(worth.currentNetWorth, worth.baseRoi),
            freedomHours: spent !== null && saved !== null ? saved / spent * 24 : null
        };
    }

    window.PFDashboardToday = {
        monthWeeks: monthWeeks,
        daysInMonth: daysInMonth,
        firstDayOfWeek: firstDayOfWeek,
        dayKey: dayKey,
        activityDays: activityDays,
        dayFraction: dayFraction,
        monthFraction: monthFraction,
        dailyGrowth: dailyGrowth,
        daysToFi: daysToFi,
        budgetPace: budgetPace,
        dayLedger: dayLedger
    };

    if (!window.PFDashboard || !window.PFI18n) {
        return;
    }

    const i18n = window.PFI18n;

    /**
     * Create an element with a class and optional text.
     * @param {string} tag - Tag name.
     * @param {string} [className] - Class attribute.
     * @param {string} [text] - Text content.
     * @returns {HTMLElement} The element.
     */
    function element (tag, className, text) {
        const node = document.createElement(tag);
        if (className) {
            node.className = className;
        }
        if (text !== undefined) {
            node.textContent = text;
        }
        return node;
    }

    /**
     * Format a share (0.84) as a whole percentage in the culture.
     * @param {number} share - The share.
     * @returns {string} The percentage, e.g. "84%".
     */
    function percent (share) {
        return i18n.formatNumber(share, { style: 'percent', maximumFractionDigits: 0 });
    }

    /**
     * Show the time with the seconds and the day period (AM/PM) set smaller, as the culture formats it.
     * @param {HTMLElement} clock - The time element.
     * @param {Date} now - The current time.
     * @returns {void}
     */
    function renderClock (clock, now) {
        const parts = new Intl.DateTimeFormat(i18n.culture, { hour: 'numeric', minute: '2-digit', second: '2-digit' })
            .formatToParts(now);
        clock.textContent = '';
        parts.forEach((part, index) => {
            const next = parts[index + 1];
            if (part.type === 'second' || (part.type === 'literal' && next && next.type === 'second')) {
                clock.appendChild(element('span', 'dash-today__seconds', part.value));
            } else if (part.type === 'dayPeriod') {
                clock.appendChild(element('span', 'dash-today__period', part.value));
            } else {
                clock.appendChild(document.createTextNode(part.value));
            }
        });
        const pad = (value) => String(value).padStart(2, '0');
        clock.setAttribute('datetime', `${dayKey(now)}T${pad(now.getHours())}:${pad(now.getMinutes())}:${pad(now.getSeconds())}`);
    }

    /**
     * The short name of the browser's time zone (PDT, GMT+7), or an empty string where the browser has none.
     * @param {Date} now - The current time.
     * @returns {string} The zone name.
     */
    function zoneName (now) {
        const zone = new Intl.DateTimeFormat(i18n.culture, { timeZoneName: 'short' }).formatToParts(now)
            .find((part) => part.type === 'timeZoneName');
        return zone ? zone.value : '';
    }

    /**
     * Fill the tape of the day's hours: hours gone are inked, the current hour fills as it passes.
     * @param {HTMLElement} root - The section.
     * @param {Date} now - The current time.
     * @returns {void}
     */
    function renderTape (root, now) {
        const tape = root.querySelector('[data-dash-today-tape]');
        if (tape.children.length !== HOURS_IN_DAY) {
            tape.textContent = '';
            for (let hour = 0; hour < HOURS_IN_DAY; hour++) {
                tape.appendChild(element('span', 'dash-today__hour'));
            }
        }
        const fraction = dayFraction(now);
        const position = fraction * HOURS_IN_DAY;
        Array.prototype.forEach.call(tape.children, (cell, hour) => {
            cell.classList.toggle('is-past', hour + 1 <= position);
            cell.classList.toggle('is-now', hour < position && hour + 1 > position);
            cell.style.setProperty('--dash-today-fill', `${Math.round(Math.min(1, Math.max(0, position - hour)) * 100)}%`);
        });
        const minutesLeft = Math.max(0, Math.round((1 - fraction) * HOURS_IN_DAY * 60));
        root.querySelector('[data-dash-today-tape-label]').textContent = i18n.t('Dashboard.Js.Today.DayGone', {
            percent: percent(fraction),
            hours: i18n.formatNumber(Math.floor(minutesLeft / 60)),
            minutes: i18n.formatNumber(minutesLeft % 60)
        });
    }

    /**
     * Show what the net worth has earned by compounding since midnight.
     * @param {HTMLElement} root - The section.
     * @param {Date} now - The current time.
     * @param {number|null} perDay - The day's growth, or null to hide the line.
     * @returns {void}
     */
    function renderEarned (root, now, perDay) {
        if (perDay === null) {
            return;
        }
        root.querySelector('[data-dash-today-earned]').textContent =
            `+${i18n.formatMoney(perDay * dayFraction(now), CURRENCY)}`;
    }

    /**
     * Fill the month meters: how much of the month is gone and, for the current month's budget, how much of it is
     * spent against that pace.
     * @param {HTMLElement} root - The section.
     * @param {Date} now - The current time.
     * @param {Object|null} budget - The dashboard's monthlyBudget.
     * @returns {void}
     */
    function renderMeters (root, now, budget) {
        const monthShare = monthFraction(now);
        root.querySelector('[data-dash-today-month-bar]').style.width = `${(monthShare * 100).toFixed(1)}%`;
        root.querySelector('[data-dash-today-month-value]').textContent =
            i18n.t('Dashboard.Js.Today.MonthValue', { percent: percent(monthShare) });

        const pace = budgetPace(budget, now);
        const spendRow = root.querySelector('[data-dash-today-spend]');
        const paceLine = root.querySelector('[data-dash-today-pace]');
        spendRow.hidden = pace === null;
        paceLine.hidden = pace === null;
        if (pace === null) {
            return;
        }

        const bar = root.querySelector('[data-dash-today-spend-bar]');
        bar.style.width = `${(Math.min(1, pace.spentShare) * 100).toFixed(1)}%`;
        bar.dataset.status = pace.status;
        root.querySelector('[data-dash-today-pace-mark]').style.left = `${(pace.monthShare * 100).toFixed(1)}%`;
        root.querySelector('[data-dash-today-spend-value]').textContent = i18n.t('Dashboard.Js.Today.SpendValue', {
            spent: i18n.formatMoney(budget.actualExpenses, CURRENCY, { whole: true }),
            budget: i18n.formatMoney(budget.budgetedExpenses, CURRENCY, { whole: true })
        });

        const statusKeys = { onPace: 'OnPace', ahead: 'AheadOfPace', over: 'OverBudget' };
        const status = root.querySelector('[data-dash-today-pace-status]');
        status.textContent = i18n.t(`Dashboard.Js.Today.${statusKeys[pace.status]}`);
        paceLine.dataset.status = pace.status;
        root.querySelector('[data-dash-today-pace-text]').textContent = pace.status === 'over'
            ? i18n.t('Dashboard.Js.Today.Over', { amount: i18n.formatMoney(-pace.remaining, CURRENCY, { whole: true }) })
            : i18n.plural('Dashboard.Js.Today.Remaining', pace.daysLeft, {
                amount: i18n.formatMoney(pace.remaining, CURRENCY, { whole: true }),
                perDay: i18n.formatMoney(pace.perDay, CURRENCY, { whole: true })
            });
    }

    /**
     * Draw the date lines, the days left in the month, and the month grid.
     * @param {HTMLElement} root - The section.
     * @param {Date} now - The current time.
     * @param {Set<string>} activity - Days with recent activity.
     * @returns {void}
     */
    function renderDay (root, now, activity) {
        const year = now.getFullYear();
        const month = now.getMonth();
        const today = now.getDate();
        const length = daysInMonth(year, month);
        const monthName = i18n.formatDate(now, { month: 'long' });
        const left = length - today;
        const zone = zoneName(now);

        root.querySelector('[data-dash-today-eyebrow]').textContent = zone
            ? i18n.t('Dashboard.Js.Today.LocalTime', { zone: zone })
            : i18n.formatDate(now, { weekday: 'long' });
        root.querySelector('[data-dash-today-date]').textContent =
            i18n.formatDate(now, { weekday: 'long', month: 'long', day: 'numeric' });
        root.querySelector('[data-dash-today-month]').textContent =
            i18n.formatDate(now, { month: 'long', year: 'numeric' });
        root.querySelector('[data-dash-today-day-of]').textContent = i18n.t('Dashboard.Js.Today.DayOf', {
            day: i18n.formatNumber(today),
            total: i18n.formatNumber(length)
        });
        root.querySelector('[data-dash-today-left]').textContent = left === 0
            ? i18n.t('Dashboard.Js.Today.LastDay', { month: monthName })
            : i18n.plural('Dashboard.Js.Today.DaysLeft', left, { month: monthName });
        root.querySelector('[data-dash-today-caption]').textContent =
            i18n.formatDate(now, { month: 'long', year: 'numeric' });

        const firstDay = firstDayOfWeek(i18n.culture);
        const weekdays = root.querySelector('[data-dash-today-weekdays]');
        weekdays.textContent = '';
        for (let column = 0; column < 7; column++) {
            // 2023-01-01 was a Sunday, so day 1 + n is weekday n.
            const sample = new Date(2023, 0, 1 + (firstDay + column) % 7);
            const header = element('th');
            header.scope = 'col';
            const initial = element('span', null, i18n.formatDate(sample, { weekday: 'narrow' }));
            initial.setAttribute('aria-hidden', 'true');
            header.appendChild(initial);
            header.appendChild(element('span', 'visually-hidden', i18n.formatDate(sample, { weekday: 'long' })));
            weekdays.appendChild(header);
        }

        const activityLabel = i18n.t('Dashboard.Js.Today.Activity');
        let anyActivity = false;
        const body = root.querySelector('[data-dash-today-days]');
        body.textContent = '';
        monthWeeks(year, month, firstDay).forEach((week) => {
            const row = element('tr');
            week.forEach((day, column) => {
                const cell = element('td');
                if (day !== null) {
                    const classes = ['dash-today__day'];
                    const weekday = new Date(year, month, day).getDay();
                    if (weekday === 0 || weekday === 6) {
                        classes.push('is-weekend');
                    }
                    if (day < today) {
                        classes.push('is-past');
                        // The shaded run of past days is rounded where it starts and stops on each row.
                        if (day === 1 || column === 0) {
                            classes.push('is-run-start');
                        }
                        if (day === today - 1 || column === 6) {
                            classes.push('is-run-end');
                        }
                    } else if (day === today) {
                        classes.push('is-today');
                        cell.setAttribute('aria-current', 'date');
                    }
                    const hasActivity = activity.has(dayKey(new Date(year, month, day)));
                    if (hasActivity) {
                        classes.push('has-activity');
                        anyActivity = true;
                    }
                    cell.appendChild(element('span', classes.join(' '), String(day)));
                    if (hasActivity) {
                        cell.appendChild(element('span', 'visually-hidden', `, ${activityLabel}`));
                    }
                }
                row.appendChild(cell);
            });
            body.appendChild(row);
        });

        const legend = root.querySelector('[data-dash-today-legend]');
        legend.textContent = i18n.t('Dashboard.Js.Today.Legend');
        legend.hidden = !anyActivity;
    }

    /**
     * Fill the lines that only change with the data: the day's growth rate and its basis, and the road to FI.
     * @param {HTMLElement} root - The section.
     * @param {Object|null} netWorth - The dashboard's netWorth summary.
     * @param {number|null} perDay - The day's growth.
     * @returns {void}
     */
    function renderWorth (root, netWorth, perDay) {
        const worth = root.querySelector('[data-dash-today-worth]');
        worth.hidden = perDay === null;
        if (perDay !== null) {
            root.querySelector('[data-dash-today-per-day]').textContent = i18n.t('Dashboard.Js.Today.PerDay', {
                amount: i18n.formatMoney(perDay, CURRENCY),
                hourly: i18n.formatMoney(perDay / HOURS_IN_DAY, CURRENCY)
            });
            root.querySelector('[data-dash-today-basis]').textContent = i18n.t('Dashboard.Js.Today.Basis', {
                rate: i18n.formatNumber(netWorth.baseRoi / 100, { style: 'percent', maximumFractionDigits: 2 }),
                netWorth: i18n.formatMoney(netWorth.currentNetWorth, CURRENCY, { whole: true })
            });
        }

        const fi = root.querySelector('[data-dash-today-fi]');
        const days = netWorth ? daysToFi(netWorth.fiYears, netWorth.fiTargetReached) : null;
        fi.hidden = days === null || !netWorth.fiReachedAge;
        if (!fi.hidden) {
            fi.textContent = i18n.t('Dashboard.Js.Today.Fi', {
                age: i18n.formatNumber(netWorth.fiReachedAge),
                days: i18n.formatNumber(days)
            });
        }
    }

    /**
     * Fill the day ledger under the welcome: a row for each figure the settings have, and what a day's saving buys.
     * @param {Object|null} netWorth - The dashboard's netWorth summary.
     * @returns {void}
     */
    function renderLedger (netWorth) {
        const ledger = document.querySelector('[data-dash-ledger]');
        if (!ledger) {
            return;
        }
        const day = dayLedger(netWorth);
        const signs = { earned: '+', spent: '−', saved: '', compounding: '+' };
        let any = false;
        Object.keys(signs).forEach((name) => {
            const row = ledger.querySelector(`[data-dash-ledger-row="${name}"]`);
            row.hidden = day[name] === null;
            if (!row.hidden) {
                any = true;
                row.querySelector('[data-dash-ledger-value]').textContent =
                    signs[name] + i18n.formatMoney(day[name], CURRENCY);
            }
        });

        const freedom = ledger.querySelector('[data-dash-ledger-freedom]');
        freedom.hidden = day.freedomHours === null;
        if (!freedom.hidden) {
            const minutes = Math.round(day.freedomHours * 60);
            const duration = minutes < 24 * 60
                ? i18n.t('Dashboard.Js.Today.Duration', {
                    hours: i18n.formatNumber(Math.floor(minutes / 60)),
                    minutes: i18n.formatNumber(minutes % 60)
                })
                : i18n.plural('Dashboard.Js.Today.DurationDays', Math.round(day.freedomHours / 24 * 10) / 10);
            freedom.textContent = i18n.t('Dashboard.Js.Today.Freedom', { duration: duration });
        }
        ledger.hidden = !any;
    }

    /**
     * Fill the Today section and keep it current.
     * @param {Object} data - The dashboard data.
     * @returns {void}
     */
    function init (data) {
        const root = document.querySelector('[data-dash-today]');
        if (!root) {
            return;
        }
        const clock = root.querySelector('[data-dash-today-clock]');
        const activity = activityDays(data.recentActivity);
        const netWorth = data.netWorth || null;
        const budget = data.monthlyBudget || null;
        const perDay = netWorth ? dailyGrowth(netWorth.currentNetWorth, netWorth.baseRoi) : null;
        let shownDay = null;
        let shownMinute = null;
        let timer = null;

        renderWorth(root, netWorth, perDay);
        renderLedger(netWorth);

        const tick = function () {
            const now = new Date();
            renderClock(clock, now);
            renderEarned(root, now, perDay);
            if (dayKey(now) !== shownDay) {
                shownDay = dayKey(now);
                renderDay(root, now, activity);
            }
            const minute = `${shownDay}T${now.getHours()}:${now.getMinutes()}`;
            if (minute !== shownMinute) {
                shownMinute = minute;
                renderTape(root, now);
                renderMeters(root, now, budget);
            }
            window.clearTimeout(timer);
            // Wake just after the next second starts.
            timer = window.setTimeout(tick, 1000 - now.getMilliseconds() + 20);
        };

        tick();
        root.hidden = false;
        // Timers are throttled in background tabs; catch up as soon as the tab is visible again.
        document.addEventListener('visibilitychange', () => {
            if (!document.hidden) {
                tick();
            }
        });
    }

    window.PFDashboard.registerSection('today', init);
})();
