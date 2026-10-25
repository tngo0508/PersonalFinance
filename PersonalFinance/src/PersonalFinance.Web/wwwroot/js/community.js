/* global PFI18n */
/**
 * Community page — poll voting, comment toggling, story expand/collapse, likes, and story submission.
 * Chrome strings come from the page's <js-strings prefix="Community.Js."> island via PFI18n; user content
 * (comments, author names, topics) is set as text and never translated.
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
     * @param {object} target - { pollCode } or { storyId }.
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

    // --- Comment form toggles (polls and stories) ---
    document.addEventListener('click', (e) => {
        const toggle = e.target.closest('.community-poll-comment-toggle, .community-story-comment-toggle');
        if (toggle) {
            toggleCommentForm(toggle);
        }
    });

    // --- Story Expand/Collapse ---
    document.addEventListener('click', (e) => {
        const btn = e.target.closest('.community-story-toggle-btn');
        if (!btn) {
            return;
        }

        const card = btn.closest('.community-story-card');
        const body = card ? card.querySelector('.community-story-body') : null;
        if (!body) {
            return;
        }

        const isExpanded = btn.getAttribute('aria-expanded') === 'true';
        btn.setAttribute('aria-expanded', String(!isExpanded));
        btn.textContent = PFI18n.t(isExpanded ? 'Community.Js.ReadMore' : 'Community.Js.ShowLess');
        body.classList.toggle('expanded', !isExpanded);
    });

    // --- Comment Like Buttons ---
    document.addEventListener('click', (e) => {
        const likeBtn = e.target.closest('.community-comment-like');
        const commentId = likeBtn ? parseInt(likeBtn.dataset.commentId, 10) : 0;
        if (commentId) {
            toggleLike(likeBtn, '.community-comment-like-count', '/Community/ToggleLike', { commentId: commentId });
        }
    });

    // --- Story Like Buttons ---
    document.addEventListener('click', (e) => {
        const likeBtn = e.target.closest('.community-story-like');
        const storyId = likeBtn ? parseInt(likeBtn.dataset.storyId, 10) : 0;
        if (storyId) {
            toggleLike(likeBtn, '.community-story-like-count', '/Community/ToggleStoryLike', { storyId: storyId });
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

    // --- Story Comment Submit ---
    document.addEventListener('click', async (e) => {
        const submitBtn = e.target.closest('.community-story-comment-submit');
        const form = submitBtn ? submitBtn.closest('.community-story-comment-form') : null;
        if (!form) {
            return;
        }

        const card = submitBtn.closest('.community-story-card');
        const storyId = card ? parseInt(card.dataset.storyId, 10) : 0;
        const posted = await postComment(form, { storyId: storyId });

        const countEl = posted && card ? card.querySelector('.community-story-comments-number') : null;
        if (countEl) {
            countEl.textContent = (parseInt(countEl.textContent.trim(), 10) || 0) + 1;
        }
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

        const header = el('div', 'community-comment-header');
        const avatar = el('div', 'community-comment-avatar', (comment.author || '?').charAt(0).toUpperCase());
        avatar.setAttribute('aria-hidden', 'true');

        const info = el('div', 'community-comment-info');
        info.append(
            el('strong', 'community-comment-author', comment.author || ''), ' ',
            el('span', 'community-comment-topic', comment.topic || ''), ' ',
            el('time', 'community-comment-time', PFI18n.t('Community.Js.JustNow')));

        const like = el('button', 'community-comment-like');
        like.type = 'button';
        like.dataset.commentId = comment.id;
        like.setAttribute('aria-label', PFI18n.t('Community.Js.LikeComment'));
        like.append(heartIcon(), el('span', 'community-comment-like-count', String(comment.likes || 0)));

        header.append(avatar, info, like);
        card.append(header, el('p', 'community-comment-text', comment.text || ''));
        list.prepend(card);
    }

    // --- Story Submission Form ---
    const submitStoryBtn = document.getElementById('submitStoryBtn');

    /**
     * Disables the submit button and shows "Submitting..." while a story is sent, then restores it.
     * @param {boolean} submitting - Whether a submission is in flight.
     */
    function setSubmitting (submitting) {
        submitStoryBtn.disabled = submitting;
        const label = submitStoryBtn.querySelector('.community-submit-story-label');
        if (label) {
            label.textContent = PFI18n.t(submitting ? 'Community.Js.Submitting' : 'Community.Js.SubmitStory');
        }
    }

    if (submitStoryBtn) {
        submitStoryBtn.addEventListener('click', async () => {
            const form = document.getElementById('submitStoryForm');
            const title = document.getElementById('storyTitle');
            const category = document.getElementById('storyCategory');
            const summary = document.getElementById('storySummary');
            const body = document.getElementById('storyBody');
            if (!form || !title || !category || !summary || !body) {
                return;
            }

            const fields = [title, category, summary, body];
            let isValid = true;
            fields.forEach((field) => {
                const value = field.value.trim();
                const invalid = !value || (field === body && value.length < 100);
                field.classList.toggle('is-invalid', invalid);
                isValid = isValid && !invalid;
            });

            if (!isValid) {
                return;
            }

            setSubmitting(true);

            try {
                const data = await postJson('/Community/SubmitStory', {
                    title: title.value.trim(),
                    category: category.value.trim(),
                    summary: summary.value.trim(),
                    body: body.value.trim()
                });
                if (data.success) {
                    showToast(PFI18n.t('Community.Js.StorySubmitted'));
                    form.reset();
                    fields.forEach((field) => field.classList.remove('is-invalid'));
                    const modalEl = document.getElementById('submitStoryModal');
                    const modal = modalEl ? bootstrap.Modal.getInstance(modalEl) : null;
                    if (modal) {
                        modal.hide();
                    }
                } else {
                    showToast(data.error || PFI18n.t('Community.Js.StoryFailed'));
                }
            } catch {
                showToast(PFI18n.t('Community.Js.StoryFailedRetry'));
            } finally {
                setSubmitting(false);
            }
        });
    }
})();
