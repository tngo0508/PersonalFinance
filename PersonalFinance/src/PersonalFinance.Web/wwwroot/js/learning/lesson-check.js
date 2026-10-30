/* global PFI18n */
/**
 * The Check on a Lesson page (#519): each question takes one answer, then shows whether it was right and why. There
 * is no score. Once every question is answered the page posts "finish" once, with the browser's IANA time zone, and
 * the finished sheet with the keepsake appears. A failed post shows an error with a Try again button.
 *
 * PFLessonCheck.createProgress holds the finishing rule apart from the DOM, so tools/lesson-check.test.js can run it
 * in Node.
 */
(function (root) {
    'use strict';

    /**
     * Tracks which questions are answered and calls onComplete once, when the last one is.
     * @param {number} total - How many questions the Check has.
     * @param {Function} onComplete - Called once every question is answered.
     * @returns {{answer: function(number): boolean, answeredCount: function(): number}} answer(index) records an
     *     answer and returns true when that answer completed the Check.
     */
    function createProgress (total, onComplete) {
        const answered = new Set();
        let completed = false;

        return {
            answer: function (index) {
                if (completed || !Number.isInteger(index) || index < 0 || index >= total) {
                    return false;
                }
                answered.add(index);
                if (answered.size < total) {
                    return false;
                }
                completed = true;
                onComplete();
                return true;
            },
            answeredCount: function () {
                return answered.size;
            }
        };
    }

    root.PFLessonCheck = { createProgress: createProgress };

    if (typeof document === 'undefined') {
        return;
    }

    /**
     * The browser's IANA time zone, or undefined when it cannot tell (the server then uses UTC).
     * @returns {string|undefined} The zone id.
     */
    function browserTimeZone () {
        try {
            return Intl.DateTimeFormat().resolvedOptions().timeZone;
        } catch (e) {
            return undefined;
        }
    }

    /**
     * Wires the Check's questions and the finish post.
     */
    function init () {
        const check = document.querySelector('[data-check]');
        if (!check) {
            return;
        }
        const questions = Array.from(check.querySelectorAll('[data-question]'));
        const finished = document.querySelector('[data-finished]');
        const error = check.querySelector('[data-finish-error]');
        const errorText = check.querySelector('[data-finish-error-text]');
        const retry = check.querySelector('[data-finish-retry]');
        const finishUrl = check.getAttribute('data-finish-url');

        /**
         * Posts "finish"; on success shows the finished sheet, otherwise the error.
         */
        async function postFinish () {
            if (error) {
                error.hidden = true;
            }
            try {
                const response = await fetch(finishUrl, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': window.PFUtils.getAntiForgeryToken()
                    },
                    body: JSON.stringify({ timeZone: browserTimeZone() })
                });
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }
                if (finished) {
                    finished.hidden = false;
                    finished.focus();
                }
            } catch (e) {
                if (error) {
                    if (errorText) {
                        errorText.textContent = PFI18n.t('Learning.Js.Check.FinishFailed');
                    }
                    error.hidden = false;
                }
            }
        }

        const progress = createProgress(questions.length, postFinish);
        if (retry) {
            retry.addEventListener('click', postFinish);
        }

        questions.forEach((question, index) => {
            const correct = parseInt(question.getAttribute('data-correct'), 10);
            const options = Array.from(question.querySelectorAll('[data-option]'));
            const explanation = question.querySelector('[data-explanation]');
            const verdict = question.querySelector('[data-verdict]');

            options.forEach((option) => {
                option.addEventListener('click', () => {
                    const chosen = parseInt(option.getAttribute('data-option'), 10);
                    options.forEach((other) => {
                        other.disabled = true;
                        const value = parseInt(other.getAttribute('data-option'), 10);
                        other.classList.toggle('is-correct', value === correct);
                        other.classList.toggle('is-wrong', value === chosen && chosen !== correct);
                    });
                    option.setAttribute('aria-pressed', 'true');
                    if (verdict) {
                        verdict.textContent = PFI18n.t(chosen === correct ? 'Learning.Js.Check.Right' : 'Learning.Js.Check.NotQuite');
                    }
                    if (explanation) {
                        explanation.hidden = false;
                    }
                    progress.answer(index);
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
