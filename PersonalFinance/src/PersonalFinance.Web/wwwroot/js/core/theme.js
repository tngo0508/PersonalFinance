(function () {
    'use strict';

    /**
     * Theme mode module (issue #296): light, dark, or the device's setting.
     * The choice is per device, in localStorage; with none stored the page follows prefers-color-scheme.
     * An inline script in _Layout's <head> applies the stored theme before the first paint; this module keeps it in
     * step with the switchers (_ThemeSwitcher) and with the device setting while no choice is stored.
     */
    window.ThemeMode = (function () {
        const STORAGE_KEY = 'theme';
        const THEMES = ['light', 'dark'];
        // The paper colour of each theme, for the browser chrome (meta theme-color); matches --lp-paper.
        const CHROME_COLORS = { light: '#ffffff', dark: '#0E1821' };
        const systemQuery = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;

        /**
         * The stored choice: 'light', 'dark', or 'system' when none is stored or storage is unavailable.
         * @returns {string}
         */
        function getChoice () {
            try {
                const stored = localStorage.getItem(STORAGE_KEY);
                return THEMES.indexOf(stored) >= 0 ? stored : 'system';
            } catch (e) {
                return 'system';
            }
        }

        /**
         * The theme in effect for a choice: 'light' or 'dark'.
         * @param {string} choice - 'light', 'dark' or 'system'.
         * @returns {string}
         */
        function resolve (choice) {
            if (THEMES.indexOf(choice) >= 0) {
                return choice;
            }
            return systemQuery && systemQuery.matches ? 'dark' : 'light';
        }

        /**
         * Stores a choice and applies it. 'system' clears the stored choice.
         * @param {string} choice - 'light', 'dark' or 'system'.
         * @returns {void}
         */
        function setChoice (choice) {
            try {
                if (THEMES.indexOf(choice) >= 0) {
                    localStorage.setItem(STORAGE_KEY, choice);
                } else {
                    localStorage.removeItem(STORAGE_KEY);
                }
            } catch (e) {
                // Storage blocked: the theme still changes for this page.
            }
            apply(choice);
        }

        /**
         * Sets the theme on <html> (data-theme for the Ledger tokens, data-bs-theme for Bootstrap) and syncs the
         * switchers. Dispatches a "themechange" event on document with the theme in effect.
         * @param {string} choice - 'light', 'dark' or 'system'.
         * @returns {void}
         */
        function apply (choice) {
            const theme = resolve(choice);
            const root = document.documentElement;
            const changed = root.getAttribute('data-theme') !== theme;
            root.setAttribute('data-theme', theme);
            root.setAttribute('data-bs-theme', theme);

            const meta = document.querySelector('meta[name="theme-color"]');
            if (meta) {
                meta.setAttribute('content', CHROME_COLORS[theme]);
            }

            syncSwitchers(choice, theme);
            if (changed) {
                document.dispatchEvent(new CustomEvent('themechange', { detail: { theme: theme } }));
            }
        }

        /**
         * Marks the current choice in the account menu and updates the top bar's one-button toggle.
         * @param {string} choice - The stored choice.
         * @param {string} theme - The theme in effect.
         * @returns {void}
         */
        function syncSwitchers (choice, theme) {
            document.querySelectorAll('[data-theme-choice]').forEach((button) => {
                const isCurrent = button.getAttribute('data-theme-choice') === choice;
                button.classList.toggle('active', isCurrent);
                button.setAttribute('aria-current', isCurrent ? 'true' : 'false');
                const check = button.querySelector('.theme-switcher__check');
                if (check) {
                    check.hidden = !isCurrent;
                }
            });

            document.querySelectorAll('[data-theme-toggle]').forEach((button) => {
                const isDark = theme === 'dark';
                const label = button.getAttribute(isDark ? 'data-label-to-light' : 'data-label-to-dark');
                button.setAttribute('aria-pressed', isDark ? 'true' : 'false');
                if (label) {
                    button.setAttribute('aria-label', label);
                    button.setAttribute('title', label);
                }
                const sun = button.querySelector('.theme-toggle__sun');
                const moon = button.querySelector('.theme-toggle__moon');
                // The icon shows the theme the button switches to.
                if (sun) {
                    sun.hidden = !isDark;
                }
                if (moon) {
                    moon.hidden = isDark;
                }
            });
        }

        /**
         * Wires the switchers and the device-setting listener.
         * @returns {void}
         */
        function setup () {
            document.addEventListener('click', (event) => {
                const target = event.target instanceof Element ? event.target : null;
                if (!target) {
                    return;
                }
                const choiceButton = target.closest('[data-theme-choice]');
                if (choiceButton) {
                    setChoice(choiceButton.getAttribute('data-theme-choice'));
                    return;
                }
                if (target.closest('[data-theme-toggle]')) {
                    setChoice(resolve(getChoice()) === 'dark' ? 'light' : 'dark');
                }
            });

            if (systemQuery) {
                const onSystemChange = function () {
                    if (getChoice() === 'system') {
                        apply('system');
                    }
                };
                if (systemQuery.addEventListener) {
                    systemQuery.addEventListener('change', onSystemChange);
                } else if (systemQuery.addListener) {
                    systemQuery.addListener(onSystemChange);
                }
            }

            // Another tab changed the choice.
            window.addEventListener('storage', (event) => {
                if (event.key === STORAGE_KEY) {
                    apply(getChoice());
                }
            });

            apply(getChoice());
        }

        /**
         * Initialize the theme module once the DOM is ready.
         * @returns {void}
         */
        function init () {
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', setup);
            } else {
                setup();
            }
        }

        return {
            init: init,
            getChoice: getChoice,
            setChoice: setChoice,
            current: function () { return resolve(getChoice()); }
        };
    })();

    window.ThemeMode.init();
})();
