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

            bellButton.addEventListener('click', function (e) {
                e.stopPropagation();
                if (dropdown.classList.contains('show')) {
                    dropdown.classList.remove('show');
                } else {
                    loadNotifications();
                }
            });

            // Close dropdown on click outside
            document.addEventListener('click', function (e) {
                if (dropdown && !dropdown.contains(e.target) &&
                    bellButton && !bellButton.contains(e.target)) {
                    dropdown.classList.remove('show');
                }
            });

            // Load unread count on page load
            loadUnreadCount();
        }

        /**
         * Load unread notification count.
         * @returns {void}
         */
        function loadUnreadCount () {
            fetch('/api/Notification/unread-count')
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error('Failed');
                    }
                    return response.json();
                })
                .then(function (data) {
                    updateBadge(data.count);
                })
                .catch(function () {
                    // Silently fail
                });
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
         * Load and display notifications.
         * @returns {void}
         */
        function loadNotifications () {
            fetch('/api/Notification')
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error('Failed');
                    }
                    return response.json();
                })
                .then(function (data) {
                    displayNotifications(data);
                    dropdown.classList.add('show');
                })
                .catch(function () {
                    // Silently fail
                });
        }

        /**
         * Display notifications in the dropdown.
         * @param {Array} notifications - List of notification objects.
         * @returns {void}
         */
        function displayNotifications (notifications) {
            if (!dropdown) {
                return;
            }

            if (!notifications || notifications.length === 0) {
                dropdown.innerHTML = '<div class="notification-empty">No notifications</div>';
                return;
            }

            let html = '<div class="notification-dropdown-header">' +
                '<h6>Notifications</h6>' +
                '<button class="btn btn-sm btn-link" id="markAllReadBtn">Mark all read</button>' +
                '</div>';

            notifications.forEach(function (notif) {
                const timeAgo = getTimeAgo(notif.createdAt);
                html += '<div class="notification-item ' + (notif.isRead ? '' : 'unread') + '" ' +
                    'data-id="' + notif.id + '">' +
                    '<div class="notification-title">' + escapeHtml(notif.title) + '</div>' +
                    '<div class="notification-message">' + escapeHtml(notif.message) + '</div>' +
                    '<div class="notification-time">' + timeAgo + '</div>' +
                    '</div>';
            });

            // Attach event listeners after setting innerHTML
            dropdown.innerHTML = html;
            const markAllBtn = dropdown.querySelector('#markAllReadBtn');
            if (markAllBtn) {
                markAllBtn.addEventListener('click', function () {
                    window.NotificationModule.markAllRead();
                });
            }
            dropdown.querySelectorAll('.notification-item').forEach(function (item) {
                item.addEventListener('click', function () {
                    const id = item.getAttribute('data-id');
                    if (id) {
                        window.NotificationModule.markAsRead(parseInt(id, 10));
                    }
                });
            });
        }

        /**
         * Mark a notification as read.
         * @param {number} id - Notification ID.
         * @returns {void}
         */
        function markAsRead (id) {
            fetch('/api/Notification/' + id + '/read', { method: 'POST' })
                .then(function () {
                    loadUnreadCount();
                })
                .catch(function () {
                    // Silently fail
                });
        }

        /**
         * Mark all notifications as read.
         * @returns {void}
         */
        function markAllRead () {
            fetch('/api/Notification/mark-all-read', { method: 'POST' })
                .then(function () {
                    loadUnreadCount();
                    loadNotifications();
                })
                .catch(function () {
                    // Silently fail
                });
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
                return 'Just now';
            }
            if (seconds < 3600) {
                return Math.floor(seconds / 60) + 'm ago';
            }
            if (seconds < 86400) {
                return Math.floor(seconds / 3600) + 'h ago';
            }
            return Math.floor(seconds / 86400) + 'd ago';
        }

        /**
         * Escape HTML to prevent XSS.
         * @param {string} text - The text to escape.
         * @returns {string} Escaped text.
         */
        function escapeHtml (text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
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
