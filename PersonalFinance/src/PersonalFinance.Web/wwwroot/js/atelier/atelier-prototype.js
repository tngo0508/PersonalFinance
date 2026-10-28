/**
 * PROTOTYPE (#496) - throwaway interactions for the Atelier variants.
 * Stub state in memory only; nothing is sent to the server. Do not merge to main.
 */
(function () {
    'use strict';

    const VARIANTS = ['A', 'B', 'C'];

    /**
     * Turns a Review card over and offers Knew it / Not yet.
     * @param {HTMLElement} root The variant root.
     */
    function wireReview (root) {
        root.querySelectorAll('[data-atl-card]').forEach((card) => {
            const flip = card.querySelector('[data-atl-flip]');
            const mark = card.querySelector('.atl-card-mark');
            flip.addEventListener('click', () => {
                card.classList.toggle('is-flipped');
                mark.hidden = false;
            });
            mark.querySelectorAll('[data-atl-mark]').forEach((button) => {
                button.addEventListener('click', () => {
                    card.dataset.marked = button.dataset.atlMark;
                    mark.textContent = button.dataset.atlMark === 'Knew it' ? 'Knew it ✓' : 'Not yet: it comes back later';
                });
            });
        });
        root.querySelectorAll('[data-atl-skip]').forEach((skip) => {
            skip.addEventListener('click', () => {
                skip.closest('.atl-review').classList.add('is-skipped');
                skip.textContent = 'Review skipped';
            });
        });
    }

    /**
     * Shows the explanation for each answer; answering every question finishes the Lesson.
     * @param {HTMLElement} root The variant root.
     */
    function wireCheck (root) {
        const questions = root.querySelectorAll('[data-atl-q]');
        questions.forEach((q) => {
            q.querySelectorAll('[data-atl-option]').forEach((option) => {
                option.addEventListener('click', () => {
                    const right = option.dataset.atlOption === q.dataset.correct;
                    q.querySelectorAll('[data-atl-option]').forEach((o) => o.classList.remove('is-right', 'is-wrong'));
                    option.classList.add(right ? 'is-right' : 'is-wrong');
                    q.querySelector('.atl-q-explain').hidden = false;
                    q.dataset.answered = 'true';
                    if ([...questions].every((x) => x.dataset.answered === 'true')) {
                        finishLesson(root);
                    }
                });
            });
        });
    }

    /**
     * Places today's keepsake and marks the next Lesson as opening tomorrow.
     * @param {HTMLElement} root The variant root.
     */
    function finishLesson (root) {
        root.querySelectorAll('[data-atl-keepsake="2"]').forEach((spot) => {
            spot.classList.add('is-placed', 'is-new');
            spot.querySelector('.atl-keepsake-icon').textContent = '📒';
            spot.querySelector('.visually-hidden').textContent = 'Ledger';
            spot.title = 'Ledger';
        });
        root.querySelectorAll('[data-atl-today-status]').forEach((s) => {
            s.textContent = 'Finished';
            s.className = 'atl-status is-done';
        });
        root.querySelectorAll('[data-atl-next-status]').forEach((s) => {
            s.textContent = 'Opens tomorrow';
        });
        root.querySelectorAll('[data-atl-finished]').forEach((f) => {
            f.hidden = false;
        });
        root.querySelectorAll('[data-atl-dot="2"]').forEach((d) => d.classList.add('is-done'));
        const stop = root.querySelector('[data-atl-stop="2"]');
        if (stop) {
            stop.classList.remove('is-today');
            stop.classList.add('is-done');
            stop.querySelector('.atl-c-marker').textContent = '📒';
        }
    }

    /**
     * Variant A: Topic tabs filter the card catalogue.
     * @param {HTMLElement} root The variant root.
     */
    function wireTabs (root) {
        root.querySelectorAll('[data-atl-tab]').forEach((tab) => {
            tab.addEventListener('click', () => {
                root.querySelectorAll('[data-atl-tab]').forEach((t) => t.setAttribute('aria-selected', String(t === tab)));
                root.querySelectorAll('[data-atl-topic]').forEach((card) => {
                    card.hidden = card.dataset.atlTopic !== tab.dataset.atlTab;
                });
            });
        });
    }

    /**
     * Variant B: one step of the Lesson at a time.
     * @param {HTMLElement} root The variant root.
     */
    function wireSteps (root) {
        const stages = root.querySelectorAll('[data-atl-step]');
        if (!stages.length) {
            return;
        }
        let current = 0;
        const show = (index) => {
            current = Math.max(0, Math.min(stages.length - 1, index));
            stages.forEach((s, i) => {
                s.hidden = i !== current;
            });
            root.querySelectorAll('[data-atl-step-tab]').forEach((t, i) => t.classList.toggle('is-current', i === current));
            root.querySelector('[data-atl-prev]').disabled = current === 0;
            root.querySelector('[data-atl-next]').disabled = current === stages.length - 1;
        };
        root.querySelectorAll('[data-atl-step-tab]').forEach((t, i) => t.addEventListener('click', () => show(i)));
        root.querySelector('[data-atl-prev]').addEventListener('click', () => show(current - 1));
        root.querySelector('[data-atl-next]').addEventListener('click', () => show(current + 1));
        show(0);
    }

    /**
     * Floating bar and arrow keys cycle ?variant=.
     */
    function wireSwitcher () {
        const params = new URLSearchParams(window.location.search);
        const index = Math.max(0, VARIANTS.indexOf((params.get('variant') || 'A').toUpperCase()));
        const go = (step) => {
            params.set('variant', VARIANTS[(index + step + VARIANTS.length) % VARIANTS.length]);
            window.location.search = params.toString();
        };
        document.querySelectorAll('[data-atl-variant-step]').forEach((button) => {
            button.addEventListener('click', () => go(Number(button.dataset.atlVariantStep)));
        });
        document.addEventListener('keydown', (event) => {
            const target = event.target;
            if (target instanceof HTMLElement && (target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName))) {
                return;
            }
            if (event.key === 'ArrowLeft') {
                go(-1);
            } else if (event.key === 'ArrowRight') {
                go(1);
            }
        });
    }

    document.addEventListener('DOMContentLoaded', () => {
        const root = document.querySelector('[data-atl]');
        if (!root) {
            return;
        }
        wireReview(root);
        wireCheck(root);
        wireTabs(root);
        wireSteps(root);
        wireSwitcher();
    });
})();
