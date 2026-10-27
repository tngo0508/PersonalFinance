/**
 * Dashboard Today section (#412).
 * Draws the user's clock and the current month in the browser's time zone, in the page culture. Days already behind
 * the user this month are shaded, today is marked, and days with recent activity carry a dot. The clock ticks on the
 * minute; when the day changes the month is redrawn.
 */
(function () {
    'use strict';

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

    window.PFDashboardToday = {
        monthWeeks: monthWeeks,
        daysInMonth: daysInMonth,
        firstDayOfWeek: firstDayOfWeek,
        dayKey: dayKey,
        activityDays: activityDays
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
     * Show the time with the day period (AM/PM) set smaller, as the culture formats it.
     * @param {HTMLElement} clock - The time element.
     * @param {Date} now - The current time.
     * @returns {void}
     */
    function renderClock (clock, now) {
        const parts = new Intl.DateTimeFormat(i18n.culture, { hour: 'numeric', minute: '2-digit' }).formatToParts(now);
        clock.textContent = '';
        parts.forEach((part) => {
            if (part.type === 'dayPeriod') {
                clock.appendChild(element('span', 'dash-today__period', part.value));
            } else {
                clock.appendChild(document.createTextNode(part.value));
            }
        });
        clock.setAttribute('datetime', `${dayKey(now)}T${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`);
    }

    /**
     * Draw the date line, the days left in the month, and the month grid.
     * @param {HTMLElement} root - The section.
     * @param {Date} now - The current time.
     * @param {Set<string>} activity - Days with recent activity.
     * @returns {void}
     */
    function renderDay (root, now, activity) {
        const year = now.getFullYear();
        const month = now.getMonth();
        const today = now.getDate();
        const monthName = i18n.formatDate(now, { month: 'long' });
        const left = daysInMonth(year, month) - today;

        root.querySelector('[data-dash-today-date]').textContent =
            i18n.formatDate(now, { weekday: 'long', month: 'long', day: 'numeric' });
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
                    const number = element('span', classes.join(' '), String(day));
                    cell.appendChild(number);
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
        let shownDay = null;
        let timer = null;

        const tick = function () {
            const now = new Date();
            renderClock(clock, now);
            if (dayKey(now) !== shownDay) {
                shownDay = dayKey(now);
                renderDay(root, now, activity);
            }
            window.clearTimeout(timer);
            // Wake just after the next minute starts.
            timer = window.setTimeout(tick, 60000 - (now.getSeconds() * 1000 + now.getMilliseconds()) + 50);
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
