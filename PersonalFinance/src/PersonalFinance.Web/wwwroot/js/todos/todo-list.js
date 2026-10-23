/**
 * To-do List page: renders the Account's open To-dos, adds new ones and deletes them after a confirmation.
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
    let listError = null;
    let deleteModal = null;
    let deleteTitle = null;
    let deleteConfirmButton = null;
    let pendingDelete = null;
    let todosById = new Map();

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

        item.appendChild(renderEditButton(todo));
        item.appendChild(renderDeleteButton(todo));
        return item;
    }

    /**
     * Builds the Delete button for one To-do. Clicks are handled on the list (see handleListClick).
     * @param {{id: number, title: string}} todo - The To-do from the server.
     * @returns {HTMLButtonElement} The button.
     */
    function renderDeleteButton (todo) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'todo-item__delete';
        button.dataset.action = 'delete';
        button.dataset.todoId = String(todo.id);
        button.dataset.todoTitle = todo.title;
        button.textContent = 'Delete';
        button.setAttribute('aria-label', `Delete ${todo.title}`);
        return button;
    }

    /**
     * Builds the Edit button for one To-do. The list's click handler opens the inline editor.
     * @param {{id: number, title: string}} todo - The To-do from the server.
     * @returns {HTMLButtonElement} The button.
     */
    function renderEditButton (todo) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'btn btn-sm btn-link todo-item__edit';
        button.dataset.todoEdit = String(todo.id);
        button.textContent = 'Edit';
        button.setAttribute('aria-label', `Edit ${todo.title}`);
        return button;
    }

    /**
     * Replaces the list with the given open To-dos, in the order the server sent them, and shows the empty state
     * when there are none.
     * @param {Array<{id: number, title: string, dueDate: (string|null)}>} todos - The open To-dos.
     * @returns {void}
     */
    function renderList (todos) {
        todosById = new Map(todos.map(todo => [todo.id, todo]));
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
     * @param {string} [fallback] - The message to use when the response has none.
     * @returns {Promise<string>} A message that is safe to show.
     */
    async function readError (response, fallback = 'The To-do could not be added. Please try again.') {
        try {
            const body = await response.json();
            if (body && typeof body.error === 'string' && body.error) {
                return body.error;
            }
        } catch {
            // Not JSON: fall through to the generic message.
        }
        return fallback;
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
     * Shows an error above the list (for example a failed delete), or clears it.
     * @param {string|null} message - The message to show, or null to clear.
     * @returns {void}
     */
    function showListError (message) {
        listError.textContent = message || '';
        listError.hidden = !message;
    }

    /**
     * Asks the Account to confirm deleting a To-do. Nothing is deleted until they press Delete in the dialog.
     * @param {MouseEvent} event - A click inside the list.
     * @returns {void}
     */
    function handleListClick (event) {
        const button = event.target.closest('[data-action="delete"]');
        if (!button || !list.contains(button)) {
            return;
        }

        pendingDelete = { id: button.dataset.todoId, title: button.dataset.todoTitle };
        deleteTitle.textContent = pendingDelete.title;
        deleteConfirmButton.disabled = false;
        deleteModal.show();
    }

    /**
     * Deletes the confirmed To-do. On success, re-renders the list; on failure, keeps the list and shows why.
     * @returns {Promise<void>}
     */
    async function handleDeleteConfirm () {
        if (!pendingDelete || deleteConfirmButton.disabled) {
            return;
        }

        const todo = pendingDelete;
        deleteConfirmButton.disabled = true;
        showListError(null);
        try {
            const response = await fetch(`${page.dataset.deleteUrl}/${encodeURIComponent(todo.id)}`, {
                method: 'POST',
                headers: { RequestVerificationToken: window.PFUtils.getAntiForgeryToken(form) }
            });
            if (!response.ok) {
                showListError(await readError(response, 'The To-do could not be deleted. Please try again.'));
                return;
            }

            renderList(await response.json());
            titleInput.focus();
        } catch {
            showListError('The To-do could not be deleted. Check your connection and try again.');
        } finally {
            // Closing the dialog clears pendingDelete (see the hidden.bs.modal handler).
            deleteModal.hide();
        }
    }

    /**
     * Swaps a To-do's list item for an inline editor holding its title and due date. Only one editor is open at a
     * time: opening another puts the first back as it was.
     * @param {HTMLLIElement} item - The list item to edit.
     * @param {{id: number, title: string, dueDate: (string|null)}} todo - The To-do being edited.
     * @returns {void}
     */
    function openEditor (item, todo) {
        closeOpenEditor();

        const editor = document.createElement('form');
        editor.className = 'todo-edit';
        editor.noValidate = true;

        const titleId = `todoEditTitle${todo.id}`;
        const dateId = `todoEditDue${todo.id}`;
        const errorId = `todoEditError${todo.id}`;

        const titleLabel = document.createElement('label');
        titleLabel.className = 'visually-hidden';
        titleLabel.htmlFor = titleId;
        titleLabel.textContent = 'To-do';
        const title = document.createElement('input');
        title.type = 'text';
        title.id = titleId;
        title.className = 'form-control todo-edit__title';
        title.autocomplete = 'off';
        title.value = todo.title;
        title.setAttribute('aria-describedby', errorId);

        const dateLabel = document.createElement('label');
        dateLabel.className = 'visually-hidden';
        dateLabel.htmlFor = dateId;
        dateLabel.textContent = 'Due date (optional)';
        const dueDate = document.createElement('input');
        dueDate.type = 'date';
        dueDate.id = dateId;
        dueDate.className = 'form-control todo-edit__date';
        dueDate.value = todo.dueDate || '';

        const save = document.createElement('button');
        save.type = 'submit';
        save.className = 'btn btn-sm btn-primary';
        save.textContent = 'Save';

        const cancel = document.createElement('button');
        cancel.type = 'button';
        cancel.className = 'btn btn-sm btn-outline-secondary';
        cancel.textContent = 'Cancel';

        const error = document.createElement('p');
        error.id = errorId;
        error.className = 'todo-edit__error';
        error.setAttribute('role', 'alert');
        error.hidden = true;

        editor.append(titleLabel, title, dateLabel, dueDate, save, cancel, error);
        item.classList.add('todo-item--editing');
        item.replaceChildren(editor);

        const showEditError = message => {
            error.textContent = message || '';
            error.hidden = !message;
            title.setAttribute('aria-invalid', message ? 'true' : 'false');
        };
        const close = () => {
            const restored = renderItem(todo);
            item.replaceWith(restored);
            restored.querySelector('.todo-item__edit')?.focus();
        };

        editor.addEventListener('submit', event => {
            event.preventDefault();
            saveEdit(todo, { editor, title, dueDate, save, showEditError });
        });
        cancel.addEventListener('click', close);
        editor.addEventListener('keydown', event => {
            if (event.key === 'Escape') {
                close();
            }
        });
        title.addEventListener('input', () => {
            if (!error.hidden) {
                showEditError(null);
            }
        });

        title.focus();
        title.select();
    }

    /**
     * Puts back the To-do whose editor is open, if any, unchanged.
     * @returns {void}
     */
    function closeOpenEditor () {
        const open = list.querySelector('.todo-item--editing');
        const todo = open && todosById.get(Number(open.dataset.todoId));
        if (todo) {
            open.replaceWith(renderItem(todo));
        }
    }

    /**
     * Saves an edited To-do. On success, re-renders the list (re-sorted by the server); on failure, keeps the
     * editor open with what the Account typed and shows why.
     * @param {{id: number}} todo - The To-do being edited.
     * @param {{editor: HTMLFormElement, title: HTMLInputElement, dueDate: HTMLInputElement,
     *     save: HTMLButtonElement, showEditError: function(?string): void}} fields - The editor's controls.
     * @returns {Promise<void>}
     */
    async function saveEdit (todo, fields) {
        const saveButton = fields.save;
        if (saveButton.disabled) {
            return;
        }

        const title = fields.title.value.trim();
        const invalid = validateTitle(title);
        if (invalid) {
            fields.showEditError(invalid);
            fields.title.focus();
            return;
        }

        fields.showEditError(null);
        saveButton.disabled = true;
        try {
            const response = await fetch(`${page.dataset.updateUrl}/${encodeURIComponent(todo.id)}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    RequestVerificationToken: window.PFUtils.getAntiForgeryToken(form)
                },
                body: JSON.stringify({ title, dueDate: fields.dueDate.value || null })
            });
            if (!response.ok) {
                fields.showEditError(await readError(response, 'The To-do could not be saved. Please try again.'));
                return;
            }

            renderList(await response.json());
            list.querySelector(`[data-todo-edit="${todo.id}"]`)?.focus();
        } catch {
            fields.showEditError('The To-do could not be saved. Check your connection and try again.');
        } finally {
            saveButton.disabled = false;
        }
    }

    /**
     * Opens the inline editor when an Edit button in the list is clicked.
     * @param {MouseEvent} event - A click inside the list.
     * @returns {void}
     */
    function handleEditClick (event) {
        const button = event.target.closest('[data-todo-edit]');
        if (!button || !list.contains(button)) {
            return;
        }
        const item = button.closest('.todo-item');
        const todo = todosById.get(Number(button.dataset.todoEdit));
        if (item && todo) {
            openEditor(item, todo);
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
        list.addEventListener('click', handleEditClick);
        initDelete();
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

    /**
     * Wires up deleting with its confirmation dialog. Without the dialog markup or Bootstrap, the Delete buttons do
     * nothing rather than delete without asking.
     * @returns {void}
     */
    function initDelete () {
        listError = document.getElementById('todoListError');
        const modalElement = document.getElementById('todoDeleteModal');
        deleteTitle = document.getElementById('todoDeleteTitle');
        deleteConfirmButton = document.getElementById('todoDeleteConfirmBtn');
        if (!listError || !modalElement || !deleteTitle || !deleteConfirmButton || !window.bootstrap) {
            return;
        }

        deleteModal = window.bootstrap.Modal.getOrCreateInstance(modalElement);
        modalElement.addEventListener('hidden.bs.modal', () => {
            pendingDelete = null;
        });
        list.addEventListener('click', handleListClick);
        deleteConfirmButton.addEventListener('click', handleDeleteConfirm);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
