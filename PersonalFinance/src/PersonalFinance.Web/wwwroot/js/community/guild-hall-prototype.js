/**
 * PROTOTYPE (#420) - throwaway interactions for the Guild Hall variants.
 * Stub state in memory only; nothing is sent to the server. Do not merge to main.
 */
(function () {
    'use strict';

    const NAMES = ['Amber Wren', 'Steady Heron', 'Quiet Kestrel', 'Gentle Fox', 'Patient Willow', 'Warm Cedar'];
    const AMOUNT = /([$€£₫]\s?\d)|(\d[\d.,]*\s?(k|usd|vnd|dollars?|đ)\b)/i;
    const VARIANTS = ['A', 'B', 'C'];

    /**
     * Wires every prototype interaction on the page.
     */
    function init () {
        let nameIndex = 0;
        document.querySelectorAll('[data-gh-reroll]').forEach((button) => {
            button.addEventListener('click', () => {
                nameIndex = (nameIndex + 1) % NAMES.length;
                document.querySelectorAll('[data-gh-self]').forEach((el) => {
                    el.textContent = NAMES[nameIndex];
                });
            });
        });

        document.querySelectorAll('[data-gh-react]').forEach((button) => {
            button.addEventListener('click', () => {
                const pressed = button.getAttribute('aria-pressed') === 'true';
                button.setAttribute('aria-pressed', String(!pressed));
                const count = button.querySelector('[data-gh-count]');
                if (count) {
                    count.textContent = String(Number(count.textContent) + (pressed ? -1 : 1));
                }
            });
        });

        document.querySelectorAll('[data-gh-poll]').forEach((poll) => {
            poll.querySelectorAll('[data-gh-vote]').forEach((option) => {
                option.addEventListener('click', () => {
                    poll.querySelector('.gh-poll-options').hidden = true;
                    poll.querySelector('.gh-poll-options').style.display = 'none';
                    poll.querySelector('.gh-poll-results').hidden = false;
                    poll.querySelector('.gh-poll-note').hidden = false;
                });
            });
        });

        document.querySelectorAll('[data-gh-checkin]').forEach((button) => {
            button.addEventListener('click', () => {
                const challenge = button.closest('[data-gh-challenge]');
                const on = !button.classList.contains('is-on');
                const delta = on ? 1 : -1;
                button.classList.toggle('is-on', on);
                button.textContent = on ? button.dataset.on : button.dataset.off;
                if (!challenge) {
                    return;
                }
                const group = challenge.querySelector('[data-gh-group-count]');
                const streak = challenge.querySelector('[data-gh-streak]');
                if (group) {
                    group.textContent = String(Number(group.textContent) + delta);
                }
                if (streak) {
                    streak.textContent = String(Number(streak.textContent) + delta);
                }
                const unlit = challenge.querySelector('.gh-lantern:not(.is-lit)');
                const lit = challenge.querySelectorAll('.gh-lantern.is-lit');
                if (on && unlit) {
                    unlit.classList.add('is-lit');
                } else if (!on && lit.length > 0) {
                    lit[lit.length - 1].classList.remove('is-lit');
                }
            });
        });

        document.querySelectorAll('.gh-form').forEach((form) => {
            const nudge = form.querySelector('[data-gh-nudge]');
            form.addEventListener('input', () => {
                const fields = form.querySelectorAll('[data-gh-amount-watch]');
                const hasAmount = Array.prototype.some.call(fields, (field) => {
                    return AMOUNT.test(field.value);
                });
                if (nudge) {
                    nudge.hidden = !hasAmount;
                }
            });
        });

        document.querySelectorAll('[data-gh-room]').forEach((tab) => {
            tab.addEventListener('click', () => {
                document.querySelectorAll('[data-gh-room]').forEach((other) => {
                    const active = other === tab;
                    other.classList.toggle('is-active', active);
                    other.setAttribute('aria-selected', String(active));
                });
                document.querySelectorAll('[data-gh-panel]').forEach((panel) => {
                    panel.hidden = panel.dataset.ghPanel !== tab.dataset.ghRoom;
                });
            });
        });

        initSwitcher();
    }

    /**
     * Cycles the ?variant= search param from the floating bar and the arrow keys.
     */
    function initSwitcher () {
        const params = new URLSearchParams(window.location.search);
        const current = Math.max(0, VARIANTS.indexOf((params.get('variant') || 'A').toUpperCase()));

        function go (step) {
            params.set('variant', VARIANTS[(current + step + VARIANTS.length) % VARIANTS.length]);
            window.location.search = params.toString();
        }

        document.querySelectorAll('[data-gh-variant-step]').forEach((button) => {
            button.addEventListener('click', () => {
                go(Number(button.dataset.ghVariantStep));
            });
        });

        document.addEventListener('keydown', (event) => {
            const target = event.target;
            if (target && (target.matches('input, textarea, select') || target.isContentEditable)) {
                return;
            }
            if (event.key === 'ArrowLeft') {
                go(-1);
            } else if (event.key === 'ArrowRight') {
                go(1);
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
