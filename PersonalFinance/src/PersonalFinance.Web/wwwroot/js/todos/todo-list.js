/**
 * To-do List page: renders the Account's open To-dos and adds new ones.
 * The server owns validation and ordering: every change posts to the Web TodosController, which answers with the
 * refreshed open list, and the page re-renders it with renderList().
 */
(function () {
    'use strict';

    const dueDateFormat = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', year: 'numeric' });

    let page = null;
    let list = null;
    let emptyState = null;
    let form = null;
    let titleInput = null;
    let dueDateInput = null;
    let addButton = null;
    let errorBox = null;

    /**
     * Turns an ISO yyyy-MM-dd due date into a local calendar date, without a time-zone shift.
     * @param {string} isoDate - The due date as sent by the server.
     * @returns {Date} The same calendar date at local midnight.
     */
    function parseDueDate (isoDate) {
        const [year, month, day] = isoDate.split('-').map(Number);
        return new Date(year, month - 1, day);
    }

    const dueStatusLabels = { overdue: 'Overdue', 'due-today': 'Due today' };

    /**
     * Builds the Overdue or Due today badge for a To-do, from the Account's local today. The status is spelled out
     * in text so it never relies on colour alone.
     * @param {{dueDate: (string|null), completedAt: (string|null)}} todo - The To-do from the server.
     * @returns {HTMLSpanElement|null} The badge, or null when the To-do needs no marking.
     */
    function renderDueStatus (todo) {
        const status = window.PFTodoDueStatus ? window.PFTodoDueStatus.classify(todo) : null;
        if (!status) {
            return null;
        }
        const badge = document.createElement('span');
        badge.className = `todo-item__status todo-item__status--${status}`;
        badge.dataset.status = status;
        badge.textContent = dueStatusLabels[status];
        return badge;
    }

    /**
     * Builds the list item for one To-do. Text goes in with textContent only.
     * @param {{id: number, title: string, dueDate: (string|null)}} todo - The To-do from the server.
     * @returns {HTMLLIElement} The list item.
     */
    function renderItem (todo) {
        const item = document.createElement('li');
        item.className = 'todo-item';
        item.dataset.todoId = String(todo.id);

        const title = document.createElement('span');
        title.className = 'todo-item__title';
        title.textContent = todo.title;
        item.appendChild(title);

        if (todo.dueDate) {
            item.dataset.dueDate = todo.dueDate;
            const due = document.createElement('time');
            due.className = 'todo-item__due';
            due.dateTime = todo.dueDate;
            due.textContent = `Due ${dueDateFormat.format(parseDueDate(todo.dueDate))}`;
            item.appendChild(due);

            const badge = renderDueStatus(todo);
            if (badge) {
                item.dataset.dueStatus = badge.dataset.status;
                item.appendChild(badge);
            }
        }

        return item;
    }

    /**
     * Replaces the list with the given open To-dos, in the order the server sent them, and shows the empty state
     * when there are none.
     * @param {Array<{id: number, title: string, dueDate: (string|null)}>} todos - The open To-dos.
     * @returns {void}
     */
    function renderList (todos) {
        list.replaceChildren(...todos.map(renderItem));
        list.hidden = todos.length === 0;
        emptyState.hidden = todos.length > 0;
    }

    /**
     * Disables the Add button while a To-do is being saved, so one submit adds one To-do.
     * @param {boolean} busy - Whether a save is in progress.
     * @returns {void}
     */
    function setBusy (busy) {
        addButton.disabled = busy;
    }

    /**
     * Shows an error under the add form, or clears it.
     * @param {string|null} message - The message to show, or null to clear.
     * @returns {void}
     */
    function showError (message) {
        errorBox.textContent = message || '';
        errorBox.hidden = !message;
        titleInput.setAttribute('aria-invalid', message ? 'true' : 'false');
    }

    /**
     * Checks the title the way the server will: trimmed, required, at most the maximum length.
     * @param {string} title - The trimmed title.
     * @returns {string|null} The error message, or null when the title is valid.
     */
    function validateTitle (title) {
        if (!title) {
            return form.dataset.msgRequired;
        }
        if (title.length > Number(form.dataset.maxLength)) {
            return form.dataset.msgTooLong;
        }
        return null;
    }

    /**
     * Reads the error message from a failed response.
     * @param {Response} response - The failed response.
     * @returns {Promise<string>} A message that is safe to show.
     */
    async function readError (response) {
        try {
            const body = await response.json();
            if (body && typeof body.error === 'string' && body.error) {
                return body.error;
            }
        } catch {
            // Not JSON: fall through to the generic message.
        }
        return 'The To-do could not be added. Please try again.';
    }

    /**
     * Adds the To-do in the form. On success, re-renders the list and clears the form; on failure, keeps what the
     * Account typed and shows why.
     * @param {SubmitEvent} event - The form submit (the Add button or Enter).
     * @returns {Promise<void>}
     */
    async function handleSubmit (event) {
        event.preventDefault();
        if (addButton.disabled) {
            return;
        }

        const title = titleInput.value.trim();
        const invalid = validateTitle(title);
        if (invalid) {
            showError(invalid);
            titleInput.focus();
            return;
        }

        showError(null);
        setBusy(true);
        try {
            const response = await fetch(page.dataset.createUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    RequestVerificationToken: window.PFUtils.getAntiForgeryToken(form)
                },
                body: JSON.stringify({ title, dueDate: dueDateInput.value || null })
            });
            if (!response.ok) {
                showError(await readError(response));
                return;
            }

            renderList(await response.json());
            form.reset();
            titleInput.focus();
        } catch {
            showError('The To-do could not be added. Check your connection and try again.');
        } finally {
            setBusy(false);
        }
    }

    /**
     * Reads the To-dos the server rendered into the page.
     * @returns {Array<object>} The open To-dos, or an empty list when the data is missing.
     */
    function readInitialTodos () {
        const data = document.getElementById('todo-data');
        try {
            return data ? JSON.parse(data.textContent) : [];
        } catch {
            return [];
        }
    }

    /**
     * Wires up the page once the DOM is ready.
     * @returns {void}
     */
    function init () {
        page = document.getElementById('todoPage');
        list = document.getElementById('todoList');
        emptyState = document.getElementById('todoEmpty');
        form = document.getElementById('todoAddForm');
        titleInput = document.getElementById('todoTitle');
        dueDateInput = document.getElementById('todoDueDate');
        addButton = document.getElementById('todoAddBtn');
        errorBox = document.getElementById('todoAddError');
        if (!page || !list || !emptyState || !form || !titleInput || !dueDateInput || !addButton || !errorBox) {
            return;
        }

        renderList(readInitialTodos());
        form.addEventListener('submit', handleSubmit);
        titleInput.addEventListener('input', () => {
            if (!errorBox.hidden) {
                showError(null);
            }
        });

        const emptyAddButton = document.getElementById('todoEmptyAddBtn');
        if (emptyAddButton) {
            emptyAddButton.addEventListener('click', () => titleInput.focus());
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
