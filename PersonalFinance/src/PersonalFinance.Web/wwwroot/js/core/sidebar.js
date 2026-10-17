(function () {
    'use strict';

    /**
     * Sidebar navigation module.
     * Handles sidebar toggle, mobile behavior, local storage persistence, and active state management.
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
            const sidebar = document.getElementById('sidebar');
            const toggleBtn = document.getElementById('sidebarToggle');
            const overlay = document.getElementById('sidebarOverlay');
            const mobileToggle = document.getElementById('mobileMenuToggle');

            if (!sidebar) {
                return;
            }

            // Restore collapsed state from localStorage
            const collapsed = localStorage.getItem(STORAGE_KEY);
            if (collapsed === 'true') {
                sidebar.classList.add('collapsed');
                document.documentElement.classList.add('sidebar-collapsed');
                if (toggleBtn) {
                    toggleBtn.setAttribute('aria-expanded', 'false');
                }
            } else {
                sidebar.classList.remove('collapsed');
                document.documentElement.classList.remove('sidebar-collapsed');
                if (toggleBtn) {
                    toggleBtn.setAttribute('aria-expanded', 'true');
                }
            }

            // Desktop toggle
            if (toggleBtn) {
                toggleBtn.addEventListener('click', function () {
                    const isCollapsed = sidebar.classList.toggle('collapsed');
                    document.documentElement.classList.toggle('sidebar-collapsed', isCollapsed);
                    localStorage.setItem(STORAGE_KEY, isCollapsed.toString());
                    toggleBtn.setAttribute('aria-expanded', (!isCollapsed).toString());
                });
            }

            // Mobile menu toggle
            if (mobileToggle) {
                mobileToggle.addEventListener('click', function () {
                    const isOpen = sidebar.classList.toggle('mobile-open');
                    if (overlay) {
                        overlay.classList.toggle('show', isOpen);
                    }
                    mobileToggle.setAttribute('aria-expanded', isOpen.toString());
                });
            }

            // Close mobile sidebar helper
            function closeMobileSidebar() {
                sidebar.classList.remove('mobile-open');
                if (overlay) {
                    overlay.classList.remove('show');
                }
                if (mobileToggle) {
                    mobileToggle.setAttribute('aria-expanded', 'false');
                }
            }

            // Close mobile sidebar on overlay click
            if (overlay) {
                overlay.addEventListener('click', function () {
                    closeMobileSidebar();
                });
            }

            // Keyboard accessibility (Escape key closes mobile sidebar)
            document.addEventListener('keydown', function (e) {
                if (e.key === 'Escape' || e.key === 'Esc') {
                    if (sidebar.classList.contains('mobile-open')) {
                        closeMobileSidebar();
                        if (mobileToggle) {
                            mobileToggle.focus();
                        }
                    }
                }
            });

            // Highlight active nav item
            highlightActive();
        }

        /**
         * Highlight the active navigation item based on current URL.
         * @returns {void}
         */
        function highlightActive() {
            const currentPath = window.location.pathname.toLowerCase();
            const links = document.querySelectorAll('.sidebar-link');

            links.forEach(function (link) {
                const href = link.getAttribute('href');
                if (!href) {
                    return;
                }
                const lowerHref = href.toLowerCase();
                if (lowerHref === '/' || lowerHref === '/dashboard') {
                    if (currentPath === '/' || currentPath === '/dashboard') {
                        link.classList.add('active');
                        link.setAttribute('aria-current', 'page');
                    }
                } else if (matchPath(currentPath, lowerHref)) {
                    link.classList.add('active');
                    link.setAttribute('aria-current', 'page');
                }
            });

            // Also highlight bottom nav on mobile
            const bottomLinks = document.querySelectorAll('.bottom-nav-item');
            bottomLinks.forEach(function (link) {
                const href = link.getAttribute('href');
                if (!href) {
                    return;
                }
                const lowerHref = href.toLowerCase();
                if (lowerHref === '/' || lowerHref === '/dashboard') {
                    if (currentPath === '/' || currentPath === '/dashboard') {
                        link.classList.add('active');
                    }
                } else if (matchPath(currentPath, lowerHref)) {
                    link.classList.add('active');
                }
            });
        }

        /**
         * Match a current path against a navigation href, ensuring the href matches
         * up to the next path segment boundary so /Budget does not match /BudgetTracker.
         * @param {string} currentPath - The current URL pathname (lowercased).
         * @param {string} href - The navigation link href (lowercased).
         * @returns {boolean} True if the path matches the href exactly or as a prefix segment.
         */
        function matchPath(currentPath, href) {
            if (currentPath === href) {
                return true;
            }
            // Match only if the current path starts with the href followed by a '/' or ends there.
            // e.g., /Budget matches /Budget but NOT /BudgetTracker.
            // /Budget/123 matches /Budget.
            return currentPath.startsWith(href + '/');
        }

        return {
            init: init
        };
    })();

    // Auto-initialize
    window.SidebarNav.init();

})();
