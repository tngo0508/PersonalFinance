/* global PFI18n */
/**
 * Guild Hall page — the Pseudonym re-roll, poll voting, Reactions, reply threads on Strategies and polls, and pinning
 * a Strategy with the no-amounts nudge. Chrome strings come from the page's <js-strings prefix="Community.Js."> island via PFI18n;
 * user content (Strategies, comments, Pseudonyms) is set as text and never translated.
 */
(function () {
    'use strict';

    /**
     * Shows a short message in the page's toast.
     * @param {string} message - The text to show.
     */
    function showToast (message) {
        let toast = document.querySelector('.community-toast');
        if (!toast) {
            toast = document.createElement('div');
            toast.className = 'community-toast';
            document.body.appendChild(toast);
        }
        toast.textContent = message;
        toast.classList.add('show');
        setTimeout(() => toast.classList.remove('show'), 3000);
    }

    /**
     * POSTs a JSON body to a Community endpoint and returns the parsed JSON answer.
     * @param {string} url - The endpoint.
     * @param {object} payload - The request body.
     * @returns {Promise<object>} The response body; error answers carry { success: false, error }.
     */
    async function postJson (url, payload) {
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': window.PFUtils.getAntiForgeryToken()
            },
            body: JSON.stringify(payload)
        });
        return response.json();
    }

    /**
     * Shows a re-rolled Pseudonym everywhere the viewer's own name is on the page: the badge, and the author of the
     * viewer's own Strategies and comments, so nothing on the page still shows the old one.
     * @param {string} name - The new Pseudonym.
     */
    function applyPseudonym (name) {
        document.querySelectorAll('[data-pseudonym-self]').forEach((el) => {
            el.textContent = name;
        });
    }

    // --- Pseudonym re-roll ---
    const rerollBtn = document.getElementById('rerollPseudonymBtn');
    if (rerollBtn) {
        rerollBtn.addEventListener('click', async () => {
            rerollBtn.disabled = true;
            try {
                const data = await postJson('/Community/RerollPseudonym', {});
                if (data.success && data.pseudonym) {
                    applyPseudonym(data.pseudonym);
                    showToast(PFI18n.t('Community.Js.PseudonymRerolled', { name: data.pseudonym }));
                } else {
                    showToast(data.error || PFI18n.t('Community.Js.RerollFailed'));
                }
            } catch {
                showToast(PFI18n.t('Community.Js.RerollFailedRetry'));
            } finally {
                rerollBtn.disabled = false;
            }
        });
    }

    /**
     * Sets a Reaction button's pressed state and, on a Strategy, its count.
     * @param {HTMLElement} btn - The Reaction button.
     * @param {boolean} pressed - Whether the viewer holds the Reaction.
     * @param {number|null} count - The kind's count, or null when the button shows none (a Comment).
     */
    function setReaction (btn, pressed, count) {
        btn.setAttribute('aria-pressed', String(pressed));
        const countEl = btn.querySelector('[data-react-count]');
        if (countEl && typeof count === 'number') {
            countEl.textContent = String(count);
        }
    }

    /**
     * Toggles a Reaction optimistically, then confirms it with the server, reverting on failure.
     * @param {HTMLElement} btn - The Reaction button, carrying its kind and its Strategy or Comment id.
     */
    async function toggleReaction (btn) {
        const strategyId = parseInt(btn.dataset.strategyId, 10) || null;
        const commentId = parseInt(btn.dataset.commentId, 10) || null;
        if (!strategyId && !commentId) {
            return;
        }

        const countEl = btn.querySelector('[data-react-count]');
        const wasPressed = btn.getAttribute('aria-pressed') === 'true';
        const count = countEl ? parseInt(countEl.textContent, 10) || 0 : null;
        const optimistic = count === null ? null : Math.max(0, count + (wasPressed ? -1 : 1));
        setReaction(btn, !wasPressed, optimistic);
        btn.disabled = true;

        try {
            const data = await postJson('/Community/React', {
                kind: btn.dataset.reactKind,
                strategyId: strategyId,
                commentId: commentId
            });
            if (data.success) {
                setReaction(btn, data.byViewer, data.count);
            } else {
                setReaction(btn, wasPressed, count);
                showToast(data.error || PFI18n.t('Community.Js.ReactFailed'));
            }
        } catch {
            setReaction(btn, wasPressed, count);
            showToast(PFI18n.t('Community.Js.ReactFailedRetry'));
        } finally {
            btn.disabled = false;
        }
    }

    document.addEventListener('click', (e) => {
        const btn = e.target.closest('.community-react');
        if (btn && !btn.disabled) {
            toggleReaction(btn);
        }
    });

    /**
     * Adds a posted Comment to the end of its thread, from the page's template, and updates the thread's summary.
     * @param {HTMLElement} thread - The thread (a details element).
     * @param {object} comment - The posted Comment, with its author label.
     */
    function appendComment (thread, comment) {
        const list = thread.querySelector('[data-thread-list]');
        const template = document.getElementById('commentTemplate');
        if (!list || !template || !comment) {
            return;
        }

        const item = template.content.firstElementChild.cloneNode(true);
        item.dataset.commentId = comment.id;
        item.querySelectorAll('[data-comment]').forEach((slot) => {
            slot.textContent = comment[slot.dataset.comment] || '';
        });
        item.querySelectorAll('.community-react').forEach((btn) => {
            btn.dataset.commentId = comment.id;
        });
        list.appendChild(item);

        const summary = thread.querySelector('[data-thread-summary]');
        if (summary) {
            summary.textContent = PFI18n.plural('Community.Js.Replies', list.children.length);
        }
    }

    // --- Replies on Strategies and polls ---
    document.addEventListener('submit', async (e) => {
        const form = e.target.closest('[data-reply-form]');
        if (!form) {
            return;
        }

        e.preventDefault();
        const input = form.querySelector('textarea');
        const button = form.querySelector('button[type="submit"]');
        if (!input) {
            return;
        }

        const text = input.value.trim();
        input.classList.toggle('is-invalid', !text);
        if (!text) {
            input.focus();
            return;
        }

        const target = form.dataset.strategyId
            ? { strategyId: parseInt(form.dataset.strategyId, 10) }
            : { pollCode: form.dataset.pollCode };
        if (button) {
            button.disabled = true;
        }

        try {
            const data = await postJson('/Community/AddComment', { ...target, text: text });
            if (data.success) {
                input.value = '';
                appendComment(form.closest('[data-thread]'), data.comment);
                showToast(PFI18n.t('Community.Js.CommentPosted'));
            } else {
                showToast(data.error || PFI18n.t('Community.Js.CommentFailed'));
            }
        } catch {
            showToast(PFI18n.t('Community.Js.CommentFailedRetry'));
        } finally {
            if (button) {
                button.disabled = false;
            }
        }
    });

    // --- Poll Voting ---
    document.addEventListener('click', async (e) => {
        const btn = e.target.closest('.community-poll-option-btn');
        if (!btn || btn.classList.contains('voted') || btn.hasAttribute('disabled')) {
            return;
        }

        const pollCode = btn.dataset.pollCode;
        const optionId = parseInt(btn.dataset.optionId, 10);
        const pollCard = btn.closest('.community-poll-card');
        if (!pollCard || !pollCode || !optionId) {
            return;
        }

        // Optimistically mark as voted
        const allBtns = pollCard.querySelectorAll('.community-poll-option-btn');
        const setVoted = (voted) => allBtns.forEach((b) => {
            b.classList.toggle('voted', voted);
            b.toggleAttribute('disabled', voted);
        });
        setVoted(true);

        try {
            const data = await postJson('/Community/Vote', { pollCode: pollCode, optionId: optionId });
            if (data.success) {
                showToast(PFI18n.t('Community.Js.Voted'));
                if (data.poll) {
                    updatePollResults(pollCard, data.poll);
                }
            } else {
                setVoted(false);
                showToast(data.error || PFI18n.t('Community.Js.VoteFailed'));
            }
        } catch {
            setVoted(false);
            showToast(PFI18n.t('Community.Js.VoteFailedRetry'));
        }
    });

    /**
     * Updates a poll card with new results from the server.
     * @param {HTMLElement} pollCard - The poll card.
     * @param {object} pollData - The updated poll.
     */
    function updatePollResults (pollCard, pollData) {
        const buttons = pollCard.querySelectorAll('.community-poll-option-btn');
        const voteSpans = pollCard.querySelectorAll('.community-poll-option-votes');
        const totalVotesEl = pollCard.querySelector('.community-poll-votes');

        buttons.forEach((btn, index) => {
            const optionText = btn.dataset.optionText;
            const option = pollData.options.find((o) => o.text === optionText);
            if (!option) {
                return;
            }

            const result = btn.querySelector('.community-poll-option-result');
            const bar = btn.querySelector('.community-poll-option-bar');
            const pct = btn.querySelector('.community-poll-option-percentage');

            if (result) {
                result.style.display = 'flex';
            }
            if (bar) {
                bar.style.width = `${option.percentage}%`;
            }
            if (pct) {
                pct.textContent = `${option.percentage}%`;
            }

            // Mark the voted option
            if (pollData.votedOptionId && option.id === pollData.votedOptionId &&
                !btn.querySelector('.community-poll-option-checked')) {
                const span = document.createElement('span');
                span.className = 'community-poll-option-checked';
                span.setAttribute('aria-hidden', 'true');
                span.textContent = '✓';
                btn.appendChild(span);
            }

            if (voteSpans[index]) {
                voteSpans[index].style.display = 'inline';
                voteSpans[index].textContent = PFI18n.plural('Community.Js.Votes', option.votes);
            }
        });

        if (totalVotesEl) {
            totalVotesEl.textContent = PFI18n.plural('Community.Js.Votes', pollData.totalVotes);
        }
    }

    // --- Pin a Strategy ---
    const pinForm = document.getElementById('pinStrategyForm');
    const pinBtn = document.getElementById('pinStrategyBtn');
    const nudge = document.getElementById('strategyAmountNudge');

    /**
     * The no-amounts rule the server flags Strategies with (MoneyAmountRule), read from the form so both agree.
     * @param {HTMLElement} form - The pin form.
     * @returns {RegExp|null} The rule, or null when the page carries none.
     */
    function amountRule (form) {
        try {
            return form.dataset.amountPattern ? new RegExp(form.dataset.amountPattern, 'iu') : null;
        } catch {
            return null;
        }
    }

    /**
     * Shows the nudge while any watched field names a money amount. It never blocks sending: an Admin reviews the
     * Strategy either way.
     * @param {HTMLElement} form - The pin form.
     * @param {RegExp|null} rule - The no-amounts rule.
     */
    function updateNudge (form, rule) {
        if (!nudge || !rule) {
            return;
        }

        const fields = form.querySelectorAll('[data-amount-watch]');
        nudge.hidden = !Array.prototype.some.call(fields, (field) => rule.test(field.value));
    }

    /**
     * Disables the send button and shows "Sending..." while a Strategy is sent, then restores it.
     * @param {boolean} sending - Whether a submission is in flight.
     */
    function setSending (sending) {
        pinBtn.disabled = sending;
        const label = pinBtn.querySelector('.community-pin-label');
        if (label) {
            label.textContent = PFI18n.t(sending ? 'Community.Js.Sending' : 'Community.Js.SendForReview');
        }
    }

    /**
     * Pins the viewer's waiting note at the top of the board, from the page's template.
     * @param {object} strategy - The sent Strategy, with its category label and author.
     */
    function pinPendingNote (strategy) {
        const board = document.getElementById('communityBoard');
        const template = document.getElementById('pendingNoteTemplate');
        if (!board || !template || !strategy) {
            return;
        }

        const note = template.content.firstElementChild.cloneNode(true);
        note.dataset.strategyId = strategy.id;
        note.querySelectorAll('[data-note]').forEach((slot) => {
            slot.textContent = strategy[slot.dataset.note] || '';
        });
        const empty = board.querySelector('[data-board-empty]');
        if (empty) {
            empty.remove();
        }
        board.prepend(note);
    }

    if (pinForm && pinBtn) {
        const rule = amountRule(pinForm);
        pinForm.addEventListener('input', () => updateNudge(pinForm, rule));

        pinForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const fields = Array.prototype.slice.call(pinForm.querySelectorAll('[name]'));
            let isValid = true;
            fields.forEach((field) => {
                const invalid = !field.value.trim();
                field.classList.toggle('is-invalid', invalid);
                isValid = isValid && !invalid;
            });

            if (!isValid) {
                const firstInvalid = pinForm.querySelector('.is-invalid');
                if (firstInvalid) {
                    firstInvalid.focus();
                }
                return;
            }

            const payload = {};
            fields.forEach((field) => {
                payload[field.name] = field.value.trim();
            });
            setSending(true);

            try {
                const data = await postJson('/Community/SubmitStrategy', payload);
                if (data.success) {
                    showToast(PFI18n.t('Community.Js.StrategySent'));
                    pinPendingNote(data.strategy);
                    pinForm.reset();
                    fields.forEach((field) => field.classList.remove('is-invalid'));
                    updateNudge(pinForm, rule);
                } else {
                    showToast(data.error || PFI18n.t('Community.Js.StrategyFailed'));
                }
            } catch {
                showToast(PFI18n.t('Community.Js.StrategyFailedRetry'));
            } finally {
                setSending(false);
            }
        });
    }
})();
