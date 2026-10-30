/* global PFI18n */
/**
 * The Review on a Lesson page (#520): up to three flash cards from earlier Lessons. A card is a button that turns on
 * click or Enter to show its one-liner; a turned card can be marked "Knew it" or "Not yet", which posts the mark. Focus
 * then moves to the next card, or to a "Review done" note after the last one. The Skip link jumps to today's Lesson.
 *
 * PFReviewCards.createDeck holds the turn and mark rules apart from the DOM, so tools/review-cards.test.js can run them
 * in Node.
 */
(function (root) {
    'use strict';

    /**
     * Tracks which cards are turned and marked. A card is marked once, and only after it is turned; a mark whose post
     * failed can be released and made again.
     * @param {number} total - How many cards the Review shows.
     * @returns {{turn: function(number): boolean, isTurned: function(number): boolean,
     *     mark: function(number): boolean, release: function(number): void, isMarked: function(number): boolean,
     *     nextUnmarked: function(number): number, allMarked: function(): boolean}} turn(index) flips a card and returns
     *     whether it now shows its back; mark(index) returns true when the mark may be posted; nextUnmarked(index)
     *     returns the first unmarked card after index (wrapping), or -1.
     */
    function createDeck (total) {
        const turned = new Set();
        const marked = new Set();
        const inDeck = (index) => Number.isInteger(index) && index >= 0 && index < total;

        return {
            turn: function (index) {
                if (!inDeck(index)) {
                    return false;
                }
                if (turned.has(index)) {
                    turned.delete(index);
                    return false;
                }
                turned.add(index);
                return true;
            },
            isTurned: function (index) {
                return turned.has(index);
            },
            mark: function (index) {
                if (!inDeck(index) || !turned.has(index) || marked.has(index)) {
                    return false;
                }
                marked.add(index);
                return true;
            },
            release: function (index) {
                marked.delete(index);
            },
            isMarked: function (index) {
                return marked.has(index);
            },
            nextUnmarked: function (index) {
                for (let step = 1; step <= total; step++) {
                    const candidate = (index + step) % total;
                    if (!marked.has(candidate)) {
                        return candidate;
                    }
                }
                return -1;
            },
            allMarked: function () {
                return total > 0 && marked.size === total;
            }
        };
    }

    root.PFReviewCards = { createDeck: createDeck };

    if (typeof document === 'undefined') {
        return;
    }

    /**
     * Wires the cards, their marks and the Skip link.
     */
    function init () {
        const review = document.querySelector('[data-review]');
        if (!review) {
            return;
        }
        const cards = Array.from(review.querySelectorAll('[data-card]'));
        const done = review.querySelector('[data-review-done]');
        const skip = review.querySelector('[data-review-skip]');
        const lessonStart = document.getElementById('lessonStart');
        const deck = createDeck(cards.length);

        if (skip && lessonStart) {
            skip.addEventListener('click', (event) => {
                event.preventDefault();
                review.hidden = true;
                lessonStart.focus();
            });
        }

        /**
         * Shows or hides a card's back to match the deck.
         * @param {number} index - The card.
         */
        function showSide (index) {
            const card = cards[index];
            const face = card.querySelector('[data-card-turn]');
            const back = card.querySelector('[data-card-back]');
            const isTurned = deck.isTurned(index);
            card.classList.toggle('is-turned', isTurned);
            if (face) {
                face.setAttribute('aria-expanded', isTurned ? 'true' : 'false');
            }
            if (back) {
                back.hidden = !isTurned;
            }
        }

        /**
         * Moves focus on once a card is marked: to the next unmarked card, or to the done note.
         * @param {number} index - The card just marked.
         */
        function moveOn (index) {
            if (deck.allMarked()) {
                if (done) {
                    done.hidden = false;
                    done.focus();
                }
                return;
            }
            const next = deck.nextUnmarked(index);
            const face = next >= 0 ? cards[next].querySelector('[data-card-turn]') : null;
            if (face) {
                face.focus();
            }
        }

        /**
         * Posts a mark; on failure releases it so the reader can try again.
         * @param {number} index - The card.
         * @param {boolean} knewIt - "Knew it" (true) or "Not yet" (false).
         */
        async function postMark (index, knewIt) {
            const card = cards[index];
            const buttons = Array.from(card.querySelectorAll('[data-mark]'));
            const status = card.querySelector('[data-card-status]');
            buttons.forEach((button) => {
                button.disabled = true;
                button.setAttribute('aria-pressed', String(button.getAttribute('data-mark') === String(knewIt)));
            });
            try {
                const response = await fetch(card.getAttribute('data-mark-url'), {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': window.PFUtils.getAntiForgeryToken()
                    },
                    body: JSON.stringify({ knewIt: knewIt })
                });
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }
                card.classList.add('is-marked');
                if (status) {
                    status.textContent = PFI18n.t(knewIt ? 'Learning.Js.Review.MarkedKnewIt' : 'Learning.Js.Review.MarkedNotYet');
                }
                moveOn(index);
            } catch (e) {
                deck.release(index);
                buttons.forEach((button) => {
                    button.disabled = false;
                    button.removeAttribute('aria-pressed');
                });
                if (status) {
                    status.textContent = PFI18n.t('Learning.Js.Review.MarkFailed');
                }
            }
        }

        cards.forEach((card, index) => {
            const face = card.querySelector('[data-card-turn]');
            if (face) {
                face.addEventListener('click', () => {
                    deck.turn(index);
                    showSide(index);
                });
            }
            card.querySelectorAll('[data-mark]').forEach((button) => {
                button.addEventListener('click', () => {
                    if (deck.mark(index)) {
                        postMark(index, button.getAttribute('data-mark') === 'true');
                    }
                });
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(typeof window !== 'undefined' ? window : globalThis);
