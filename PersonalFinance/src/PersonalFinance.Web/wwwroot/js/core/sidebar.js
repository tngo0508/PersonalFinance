(function () {
    'use strict';

    /**
     * Sidebar navigation module.
     * Handles sidebar toggle, mobile behavior, and active state management.
     */
    window.SidebarNav = (function () {
        const STORAGE_KEY = 'sidebar-collapsed';

        /**
         * Initialize the sidebar navigation.
         * @returns {void}
         */
        function init() {
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', setup);
            } else {
                setup();
            }
        }

        /**
         * Set up sidebar event listeners and initial state.
         * @returns {void}
         */
        function setup() {
            var sidebar = document.getElementById('sidebar');
            var toggleBtn = document.getElementById('sidebarToggle');
            var overlay = document.getElementById('sidebarOverlay');
            var mobileToggle = document.getElementById('mobileMenuToggle');

            if (!sidebar) return;

            // Restore collapsed state from localStorage
            var collapsed = localStorage.getItem(STORAGE_KEY);
            if (collapsed === 'true') {
                sidebar.classList.add('collapsed');
            }

            // Desktop toggle
            if (toggleBtn) {
                toggleBtn.addEventListener('click', function () {
                    sidebar.classList.toggle('collapsed');
                    localStorage.setItem(STORAGE_KEY, sidebar.classList.contains('collapsed'));
                });
            }

            // Mobile menu toggle
            if (mobileToggle) {
                mobileToggle.addEventListener('click', function () {
                    sidebar.classList.add('mobile-open');
                    if (overlay) overlay.classList.add('show');
                });
            }

            // Close mobile sidebar on overlay click
            if (overlay) {
                overlay.addEventListener('click', function () {
                    sidebar.classList.remove('mobile-open');
                    overlay.classList.remove('show');
                });
            }

            // Highlight active nav item
            highlightActive();
        }

        /**
         * Highlight the active navigation item based on current URL.
         * @returns {void}
         */
        function highlightActive() {
            var currentPath = window.location.pathname;
            var links = document.querySelectorAll('.sidebar-link');

            links.forEach(function (link) {
                var href = link.getAttribute('href');
                if (href && currentPath.includes(href)) {
                    link.classList.add('active');
                }
            });

            // Also highlight bottom nav on mobile
            var bottomLinks = document.querySelectorAll('.bottom-nav-item');
            bottomLinks.forEach(function (link) {
                var href = link.getAttribute('href');
                if (href && currentPath.includes(href)) {
                    link.classList.add('active');
                }
            });
        }

        return {
            init: init
        };
    })();

    // Auto-initialize
    window.SidebarNav.init();

})();
