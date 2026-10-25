(function () {
    'use strict';

    /**
     * Notification module for the top bar.
     * Handles notification bell, unread count, and dropdown display.
     */
    window.NotificationModule = (function () {
        let bellButton;
        let dropdown;
        let badge;

        /**
         * Initialize the notification module.
         * @returns {void}
         */
        function init () {
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', setup);
            } else {
                setup();
            }
        }

        /**
         * Set up notification event listeners.
         * @returns {void}
         */
        function setup () {
            bellButton = document.getElementById('notificationBell');
            dropdown = document.getElementById('notificationDropdown');
            badge = document.getElementById('notificationBadge');

            if (!bellButton || !dropdown) {
                return;
            }

            bellButton.addEventListener('click', (e) => {
                e.stopPropagation();
                if (dropdown.classList.contains('show')) {
                    dropdown.classList.remove('show');
                } else {
                    loadNotifications();
                }
            });

            // Close dropdown on click outside
            document.addEventListener('click', (e) => {
                if (dropdown && !dropdown.contains(e.target) &&
                    bellButton && !bellButton.contains(e.target)) {
                    dropdown.classList.remove('show');
                }
            });

            // Load unread count on page load
            loadUnreadCount();
        }

        /**
         * Load unread notification count. A failure leaves the badge as it is.
         * @returns {Promise<void>}
         */
        async function loadUnreadCount () {
            try {
                const data = await getJson('/api/Notification/unread-count');
                updateBadge(data.count);
            } catch {
                // Silently fail
            }
        }

        /**
         * Update the notification badge.
         * @param {number} count - Unread count.
         * @returns {void}
         */
        function updateBadge (count) {
            if (!badge) {
                return;
            }
            if (count > 0) {
                badge.textContent = count > 99 ? '99+' : count;
                badge.classList.remove('hidden');
            } else {
                badge.classList.add('hidden');
            }
        }

        /**
         * Load and display notifications. A failure keeps the dropdown closed.
         * @returns {Promise<void>}
         */
        async function loadNotifications () {
            try {
                const data = await getJson('/api/Notification');
                displayNotifications(data);
                dropdown.classList.add('show');
            } catch {
                // Silently fail
            }
        }

        /**
         * GET a notification endpoint as JSON.
         * @param {string} url - Endpoint to call.
         * @returns {Promise<*>} The parsed body.
         */
        async function getJson (url) {
            const response = await send(url);
            return response.json();
        }

        /**
         * Create an element with a class and text content.
         * @param {string} tag - Tag name.
         * @param {string} className - Class attribute.
         * @param {string} text - Text content.
         * @returns {HTMLElement} The element.
         */
        function textElement (tag, className, text) {
            const element = document.createElement(tag);
            element.className = className;
            element.textContent = text;
            return element;
        }

        /**
         * Display notifications in the dropdown. All text goes in through textContent.
         * @param {Array} notifications - List of notification objects.
         * @returns {void}
         */
        function displayNotifications (notifications) {
            if (!dropdown) {
                return;
            }

            if (!notifications || notifications.length === 0) {
                dropdown.replaceChildren(textElement('div', 'notification-empty',
                    window.PFI18n.t('Shell.Js.Notifications.Empty')));
                return;
            }

            const header = textElement('div', 'notification-dropdown-header', '');
            header.appendChild(textElement('h6', '', window.PFI18n.t('Shell.Js.Notifications.Title')));
            const markAllBtn = textElement('button', 'btn btn-sm btn-link',
                window.PFI18n.t('Shell.Js.Notifications.MarkAllRead'));
            markAllBtn.id = 'markAllReadBtn';
            markAllBtn.addEventListener('click', () => {
                window.NotificationModule.markAllRead();
            });
            header.appendChild(markAllBtn);

            const items = notifications.map((notif) => {
                const item = textElement('div', notif.isRead ? 'notification-item' : 'notification-item unread', '');
                item.setAttribute('data-id', notif.id);
                item.appendChild(textElement('div', 'notification-title', notif.title));
                item.appendChild(textElement('div', 'notification-message', notif.message));
                item.appendChild(textElement('div', 'notification-time', getTimeAgo(notif.createdAt)));
                item.addEventListener('click', () => {
                    window.NotificationModule.markAsRead(parseInt(notif.id, 10));
                });
                return item;
            });

            dropdown.replaceChildren(header, ...items);
        }

        /**
         * Mark a notification as read.
         * @param {number} id - Notification ID.
         * @returns {Promise<void>}
         */
        async function markAsRead (id) {
            try {
                await postNotificationAction(`/api/Notification/${ id }/read`);
                loadUnreadCount();
            } catch {
                // Silently fail
            }
        }

        /**
         * Mark all notifications as read.
         * @returns {Promise<void>}
         */
        async function markAllRead () {
            try {
                await postNotificationAction('/api/Notification/mark-all-read');
                loadUnreadCount();
                loadNotifications();
            } catch {
                // Silently fail
            }
        }

        /**
         * POST a notification action with the page's antiforgery token.
         * @param {string} url - Endpoint to call.
         * @returns {Promise<void>}
         */
        async function postNotificationAction (url) {
            await send(url, {
                method: 'POST',
                headers: { 'RequestVerificationToken': window.PFUtils.getAntiForgeryToken() }
            });
        }

        /**
         * Call a notification endpoint; a non-success status throws.
         * @param {string} url - Endpoint to call.
         * @param {RequestInit} [options] - Fetch options.
         * @returns {Promise<Response>} The successful response.
         */
        async function send (url, options) {
            const response = await fetch(url, options);
            if (!response.ok) {
                throw new Error(`Notification request failed: ${response.status}`);
            }
            return response;
        }

        /**
         * Calculate time ago from a date string.
         * @param {string} dateStr - ISO date string.
         * @returns {string} Human-readable time ago.
         */
        function getTimeAgo (dateStr) {
            const date = new Date(dateStr);
            const now = new Date();
            const seconds = Math.floor((now - date) / 1000);

            if (seconds < 60) {
                return window.PFI18n.t('Shell.Js.Notifications.JustNow');
            }
            if (seconds < 3600) {
                return window.PFI18n.t('Shell.Js.Notifications.MinutesAgo', { count: Math.floor(seconds / 60) });
            }
            if (seconds < 86400) {
                return window.PFI18n.t('Shell.Js.Notifications.HoursAgo', { count: Math.floor(seconds / 3600) });
            }
            return window.PFI18n.t('Shell.Js.Notifications.DaysAgo', { count: Math.floor(seconds / 86400) });
        }

        return {
            init: init,
            markAsRead: markAsRead,
            markAllRead: markAllRead
        };
    })();

    // Auto-initialize
    window.NotificationModule.init();

})();
