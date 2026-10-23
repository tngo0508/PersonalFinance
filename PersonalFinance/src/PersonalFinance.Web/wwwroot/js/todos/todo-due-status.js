/**
 * To-do due status: classifies a To-do's due date against the Account's local today.
 * Derived in the browser only; the server never stores or computes it.
 */
(function () {
    'use strict';

    /**
     * Formats a date as yyyy-MM-dd in local time, so it compares with the server's date-only due dates.
     * @param {Date} date - Any moment on the local calendar day.
     * @returns {string} The local calendar date.
     */
    function toLocalIsoDate (date) {
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${date.getFullYear()}-${month}-${day}`;
    }

    /**
     * Classifies a To-do by its due date relative to today.
     * @param {{dueDate: (string|null), completedAt: (string|null)}} todo - The To-do from the server.
     * @param {Date} [today] - The current moment; defaults to now.
     * @returns {('overdue'|'due-today'|null)} The due status; null when the To-do is completed, undated or due
     *     later.
     */
    function classify (todo, today = new Date()) {
        if (!todo.dueDate || todo.completedAt) {
            return null;
        }
        const localToday = toLocalIsoDate(today);
        if (todo.dueDate < localToday) {
            return 'overdue';
        }
        if (todo.dueDate === localToday) {
            return 'due-today';
        }
        return null;
    }

    window.PFTodoDueStatus = { classify };
})();
