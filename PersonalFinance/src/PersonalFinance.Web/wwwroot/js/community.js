/* global PFI18n */
/**
 * Guild Hall page — the Pseudonym re-roll, poll voting, comment toggling, comment likes, and pinning a Strategy with
 * the no-amounts nudge. Chrome strings come from the page's <js-strings prefix="Community.Js."> island via PFI18n;
 * user content (Strategies, comments, Pseudonyms) is set as text and never translated.
 */
(function () {
    'use strict';

    const SVG_NS = 'http://www.w3.org/2000/svg';
    const HEART_PATH = 'M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z';

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
     * The avatar letters of an author label: the first letter of up to count words, upper-cased.
     * @param {string} name - The author label.
     * @param {number} count - How many words to take.
     * @returns {string} The letters.
     */
    function initials (name, count) {
        return name.split(' ').filter(Boolean).slice(0, count).map((word) => word.charAt(0).toUpperCase()).join('');
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
        document.querySelectorAll('[data-pseudonym-self-initials]').forEach((el) => {
            el.textContent = initials(name, parseInt(el.dataset.pseudonymSelfInitials, 10) || 1);
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
     * Opens or closes the comment form that follows a toggle button, focusing its textarea when opened.
     * @param {HTMLElement} toggle - The toggle button.
     */
    function toggleCommentForm (toggle) {
        const form = toggle.nextElementSibling;
        if (!form) {
            return;
        }

        const isExpanded = toggle.getAttribute('aria-expanded') === 'true';
        toggle.setAttribute('aria-expanded', String(!isExpanded));
        form.style.display = isExpanded ? 'none' : 'block';
        if (!isExpanded) {
            const input = form.querySelector('textarea');
            if (input) {
                input.focus();
            }
        }
    }

    /**
     * Closes a comment form and marks its toggle collapsed.
     * @param {HTMLElement} form - The comment form.
     */
    function closeCommentForm (form) {
        form.style.display = 'none';
        const toggle = form.previousElementSibling;
        if (toggle) {
            toggle.setAttribute('aria-expanded', 'false');
        }
    }

    /**
     * Sets a like button's state and count.
     * @param {HTMLElement} likeBtn - The like button.
     * @param {HTMLElement} countEl - Its count element.
     * @param {boolean} liked - Whether it is liked.
     * @param {number} count - The like count.
     */
    function setLike (likeBtn, countEl, liked, count) {
        likeBtn.classList.toggle('liked', liked);
        countEl.textContent = count;
    }

    /**
     * Toggles a like optimistically, then confirms it with the server, reverting on failure.
     * @param {HTMLElement} likeBtn - The like button.
     * @param {string} countSelector - Selector of its count element.
     * @param {string} url - The toggle endpoint.
     * @param {object} payload - The request body.
     */
    async function toggleLike (likeBtn, countSelector, url, payload) {
        const countEl = likeBtn.querySelector(countSelector);
        if (!countEl) {
            return;
        }

        const wasLiked = likeBtn.classList.contains('liked');
        const count = parseInt(countEl.textContent, 10) || 0;
        setLike(likeBtn, countEl, !wasLiked, wasLiked ? count - 1 : count + 1);

        try {
            const data = await postJson(url, payload);
            if (data.success) {
                setLike(likeBtn, countEl, data.isLiked, data.likes);
            } else {
                setLike(likeBtn, countEl, wasLiked, count);
                showToast(data.error || PFI18n.t('Community.Js.LikeFailed'));
            }
        } catch {
            setLike(likeBtn, countEl, wasLiked, count);
            showToast(PFI18n.t('Community.Js.LikeFailedRetry'));
        }
    }

    /**
     * Posts a comment from a form, prepends it to Recent Community Activity, and closes the form.
     * @param {HTMLElement} form - The comment form.
     * @param {object} target - { pollCode } or { strategyId }.
     * @returns {Promise<boolean>} Whether the comment was posted.
     */
    async function postComment (form, target) {
        const input = form.querySelector('textarea');
        if (!input || !input.value.trim()) {
            return false;
        }

        const text = input.value.trim();
        input.value = '';

        try {
            const data = await postJson('/Community/AddComment', { ...target, text: text });
            if (!data.success) {
                input.value = text;
                showToast(data.error || PFI18n.t('Community.Js.CommentFailed'));
                return false;
            }

            showToast(PFI18n.t('Community.Js.CommentPosted'));
            closeCommentForm(form);
            prependComment(data.comment);
            return true;
        } catch {
            input.value = text;
            showToast(PFI18n.t('Community.Js.CommentFailedRetry'));
            return false;
        }
    }

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

    // --- Comment form toggles (polls) ---
    document.addEventListener('click', (e) => {
        const toggle = e.target.closest('.community-poll-comment-toggle');
        if (toggle) {
            toggleCommentForm(toggle);
        }
    });

    // --- Comment Like Buttons ---
    document.addEventListener('click', (e) => {
        const likeBtn = e.target.closest('.community-comment-like');
        const commentId = likeBtn ? parseInt(likeBtn.dataset.commentId, 10) : 0;
        if (commentId) {
            toggleLike(likeBtn, '.community-comment-like-count', '/Community/ToggleLike', { commentId: commentId });
        }
    });

    // --- Poll Comment Submit ---
    document.addEventListener('click', (e) => {
        const submitBtn = e.target.closest('.community-poll-comment-submit');
        const form = submitBtn ? submitBtn.closest('.community-poll-comment-form') : null;
        if (!form) {
            return;
        }

        const pollCard = submitBtn.closest('.community-poll-card');
        postComment(form, { pollCode: pollCard ? pollCard.dataset.pollId : '' });
    });

    /**
     * Builds an element with a class and, optionally, text.
     * @param {string} tag - The tag name.
     * @param {string} className - The class.
     * @param {string} [text] - Its text, set with textContent.
     * @returns {HTMLElement} The element.
     */
    function el (tag, className, text) {
        const node = document.createElement(tag);
        node.className = className;
        if (text !== undefined) {
            node.textContent = text;
        }
        return node;
    }

    /**
     * Builds the heart icon of a like button.
     * @returns {SVGElement} The icon.
     */
    function heartIcon () {
        const svg = document.createElementNS(SVG_NS, 'svg');
        const attributes = {
            width: '16', height: '16', viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor',
            'stroke-width': '2', 'stroke-linecap': 'round', 'stroke-linejoin': 'round'
        };
        Object.keys(attributes).forEach((name) => svg.setAttribute(name, attributes[name]));
        const path = document.createElementNS(SVG_NS, 'path');
        path.setAttribute('d', HEART_PATH);
        svg.appendChild(path);
        return svg;
    }

    /**
     * Prepends a new comment to the recent comments list.
     * @param {object} comment - The posted comment.
     */
    function prependComment (comment) {
        const list = document.getElementById('communityCommentsList');
        if (!list || !comment) {
            return;
        }

        const noComments = list.querySelector('.community-no-comments');
        if (noComments) {
            noComments.remove();
        }

        const card = el('div', 'community-comment-card');
        card.dataset.commentId = comment.id;

        const author = comment.author || '';
        const header = el('div', 'community-comment-header');
        const avatar = el('div', 'community-comment-avatar', initials(author, 1) || '?');
        avatar.setAttribute('aria-hidden', 'true');
        const authorEl = el('strong', 'community-comment-author', author);
        if (comment.isOwn) {
            avatar.dataset.pseudonymSelfInitials = '1';
            authorEl.dataset.pseudonymSelf = '';
        }

        const info = el('div', 'community-comment-info');
        info.append(authorEl, ' ', el('time', 'community-comment-time', PFI18n.t('Community.Js.JustNow')));

        const like = el('button', 'community-comment-like');
        like.type = 'button';
        like.dataset.commentId = comment.id;
        like.setAttribute('aria-label', PFI18n.t('Community.Js.LikeComment'));
        like.append(heartIcon(), el('span', 'community-comment-like-count', String(comment.likes || 0)));

        header.append(avatar, info, like);
        card.append(header, el('p', 'community-comment-text', comment.text || ''));
        list.prepend(card);
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
