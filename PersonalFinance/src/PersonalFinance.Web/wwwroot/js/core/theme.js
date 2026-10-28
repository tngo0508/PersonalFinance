(function () {
    'use strict';

    /**
     * Theme mode module (issues #296 and #481): light, dark, or the device's setting.
     * The choice is per device, in localStorage; with none stored the page follows prefers-color-scheme.
     * An inline script in _Layout's <head> applies the stored theme before the first paint; this module keeps it in
     * step with the top-bar control (_ThemeSwitcher: an icon button opening a Light / Dark / System popover) and with
     * the device setting while no choice is stored.
     */
    window.ThemeMode = (function () {
        const STORAGE_KEY = 'theme';
        const THEMES = ['light', 'dark'];
        // The paper colour of each theme, for the browser chrome (meta theme-color); matches --lp-paper.
        const CHROME_COLORS = { light: '#ffffff', dark: '#0E1821' };
        // Arrow keys in the popover: the step to the next (1) or previous (-1) choice.
        const ARROW_STEPS = { ArrowDown: 1, ArrowRight: 1, ArrowUp: -1, ArrowLeft: -1 };
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
         * top-bar control. Dispatches a "themechange" event on document with the theme in effect.
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

            syncSwitchers(THEMES.indexOf(choice) >= 0 ? choice : 'system');
            if (changed) {
                document.dispatchEvent(new CustomEvent('themechange', { detail: { theme: theme } }));
            }
        }

        /**
         * Updates the top-bar control: the button's icon and name show the choice, and the popover checks it. Only the
         * checked choice is in the Tab order, as in a radio group.
         * @param {string} choice - 'light', 'dark' or 'system'.
         * @returns {void}
         */
        function syncSwitchers (choice) {
            document.querySelectorAll('[data-theme-button]').forEach((button) => {
                const label = button.getAttribute(`data-label-${choice}`);
                if (label) {
                    button.setAttribute('aria-label', label);
                    button.setAttribute('title', label);
                }
                button.querySelectorAll('[data-theme-icon]').forEach((icon) => {
                    icon.hidden = icon.getAttribute('data-theme-icon') !== choice;
                });
            });

            document.querySelectorAll('[data-theme-choice]').forEach((item) => {
                const isCurrent = item.getAttribute('data-theme-choice') === choice;
                item.setAttribute('aria-checked', isCurrent ? 'true' : 'false');
                item.setAttribute('tabindex', isCurrent ? '0' : '-1');
            });
        }

        /**
         * The control an element is part of, or null when it is outside every control.
         * @param {Element|null} element - Any element.
         * @returns {{root: Element, button: Element, popover: Element}|null}
         */
        function controlOf (element) {
            const root = element ? element.closest('[data-theme-switcher]') : null;
            if (!root) {
                return null;
            }
            const button = root.querySelector('[data-theme-button]');
            const popover = root.querySelector('[data-theme-popover]');
            return button && popover ? { root: root, button: button, popover: popover } : null;
        }

        /**
         * Opens the popover and moves focus to the checked choice.
         * @param {{button: Element, popover: Element}} control - The control.
         * @returns {void}
         */
        function open (control) {
            control.popover.hidden = false;
            control.button.setAttribute('aria-expanded', 'true');
            const checked = control.popover.querySelector('[aria-checked="true"]')
                || control.popover.querySelector('[data-theme-choice]');
            if (checked) {
                checked.focus();
            }
        }

        /**
         * Closes the popover.
         * @param {{button: Element, popover: Element}} control - The control.
         * @param {boolean} refocus - Return focus to the button.
         * @returns {void}
         */
        function close (control, refocus) {
            control.popover.hidden = true;
            control.button.setAttribute('aria-expanded', 'false');
            if (refocus) {
                control.button.focus();
            }
        }

        /**
         * Closes every open popover that does not hold the given element.
         * @param {Element|null} keep - An element whose control stays as it is, or null to close them all.
         * @param {boolean} refocus - Return focus to each closed control's button.
         * @returns {void}
         */
        function closeOthers (keep, refocus) {
            document.querySelectorAll('[data-theme-switcher]').forEach((root) => {
                const control = controlOf(root);
                if (control && !control.popover.hidden && !(keep && root.contains(keep))) {
                    close(control, refocus);
                }
            });
        }

        /**
         * Handles a click: the button opens or closes its popover, a choice applies and closes it, and a click outside
         * closes it without a choice.
         * @param {MouseEvent} event - The click.
         * @returns {void}
         */
        function onClick (event) {
            const target = event.target instanceof Element ? event.target : null;
            const control = controlOf(target);
            if (!control) {
                // Focus returns to the button unless the click put it on something else.
                const active = document.activeElement;
                closeOthers(null, !active || active === document.body);
                return;
            }
            closeOthers(target, false);

            const choice = target.closest('[data-theme-choice]');
            if (choice) {
                setChoice(choice.getAttribute('data-theme-choice'));
                close(control, true);
            } else if (target.closest('[data-theme-button]')) {
                if (control.popover.hidden) {
                    open(control);
                } else {
                    close(control, true);
                }
            }
        }

        /**
         * Handles keys in an open popover: Escape closes it and the arrow keys move between the choices. Enter and
         * Space on a choice are its own click.
         * @param {KeyboardEvent} event - The key press.
         * @returns {void}
         */
        function onKeydown (event) {
            const target = event.target instanceof Element ? event.target : null;
            const control = controlOf(target);
            if (!control || control.popover.hidden) {
                return;
            }
            if (event.key === 'Escape') {
                event.preventDefault();
                close(control, true);
                return;
            }
            const step = ARROW_STEPS[event.key];
            const current = target.closest('[data-theme-choice]');
            if (!step || !current) {
                return;
            }
            event.preventDefault();
            const items = Array.prototype.slice.call(control.popover.querySelectorAll('[data-theme-choice]'));
            const next = items[(items.indexOf(current) + step + items.length) % items.length];
            next.focus();
        }

        /**
         * Wires the control and the device-setting listener.
         * @returns {void}
         */
        function setup () {
            document.addEventListener('click', onClick);
            document.addEventListener('keydown', onKeydown);
            // Tabbing away from the control closes its popover.
            document.addEventListener('focusin', (event) => {
                closeOthers(event.target instanceof Element ? event.target : null, false);
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
