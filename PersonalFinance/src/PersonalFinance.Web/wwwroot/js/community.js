/**
 * Community page — poll voting, comment toggling, case study expand/collapse, likes, and real-time updates.
 */
(function () {
    'use strict';

    // --- Toast Notification ---
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

    // --- Poll Voting ---
    document.addEventListener('click', function (e) {
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
        allBtns.forEach(function (b) {
            b.classList.add('voted');
            b.setAttribute('disabled', 'true');
        });
        btn.classList.add('voted');

        // Send vote to server
        fetch('/Community/Vote', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': window.PFUtils.getAntiForgeryToken()
            },
            body: JSON.stringify({ pollCode: pollCode, optionId: optionId })
        })
            .then(function (res) {
                return res.json();
            })
            .then(function (data) {
                if (data.success) {
                    showToast('Thanks for voting!');
                    // Update poll results from server response
                    if (data.poll) {
                        updatePollResults(pollCard, data.poll);
                    }
                } else {
                    // Revert optimistic update on failure
                    allBtns.forEach(function (b) {
                        b.classList.remove('voted');
                        b.removeAttribute('disabled');
                    });
                    showToast(data.error || 'Vote failed.');
                }
            })
            .catch(function () {
                // Revert optimistic update on error
                allBtns.forEach(function (b) {
                    b.classList.remove('voted');
                    b.removeAttribute('disabled');
                });
                showToast('Vote failed. Please try again.');
            });
    });

    /**
   * Update poll card with new results from the server.
   */
    function updatePollResults (pollCard, pollData) {
        const buttons = pollCard.querySelectorAll('.community-poll-option-btn');
        const voteSpans = pollCard.querySelectorAll('.community-poll-option-votes');
        const totalVotesEl = pollCard.querySelector('.community-poll-votes');

        buttons.forEach(function (btn, index) {
            const optionText = btn.dataset.optionText;
            const option = pollData.options.find(function (o) {
                return o.text === optionText;
            });
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
                bar.style.width = option.percentage + '%';
            }
            if (pct) {
                pct.textContent = option.percentage + '%';
            }

            // Mark the voted option
            if (pollData.votedOptionId && option.id === pollData.votedOptionId) {
                const checked = btn.querySelector('.community-poll-option-checked');
                if (!checked) {
                    const span = document.createElement('span');
                    span.className = 'community-poll-option-checked';
                    span.setAttribute('aria-hidden', 'true');
                    span.textContent = '✓';
                    btn.appendChild(span);
                }
            }

            // Update vote count
            if (voteSpans[index]) {
                voteSpans[index].style.display = 'inline';
                voteSpans[index].textContent = option.votes + ' votes';
            }
        });

        if (totalVotesEl) {
            totalVotesEl.textContent = pollData.totalVotes + ' votes';
        }
    }

    // --- Poll Comment Toggles ---
    document.addEventListener('click', function (e) {
        const toggle = e.target.closest('.community-poll-comment-toggle');
        if (!toggle) {
            return;
        }

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
    });

    // --- Case Study Comment Toggles ---
    document.addEventListener('click', function (e) {
        const toggle = e.target.closest('.community-casestudy-comment-toggle');
        if (!toggle) {
            return;
        }

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
    });

    // --- Story Expand/Collapse ---
    document.addEventListener('click', function (e) {
        const btn = e.target.closest('.community-story-toggle-btn');
        if (!btn) {
            return;
        }

        const card = btn.closest('.community-story-card');
        if (!card) {
            return;
        }

        const body = card.querySelector('.community-story-body');
        if (!body) {
            return;
        }

        const isExpanded = btn.getAttribute('aria-expanded') === 'true';
        btn.setAttribute('aria-expanded', String(!isExpanded));
        btn.textContent = isExpanded ? 'Read more' : 'Show less';
        body.classList.toggle('expanded', !isExpanded);
    });

    // --- Comment Like Buttons ---
    document.addEventListener('click', function (e) {
        const likeBtn = e.target.closest('.community-comment-like');
        if (!likeBtn) {
            return;
        }

        const commentId = parseInt(likeBtn.dataset.commentId, 10);
        const countEl = likeBtn.querySelector('.community-comment-like-count');
        if (!countEl || !commentId) {
            return;
        }

        const isLiked = likeBtn.classList.contains('liked');

        // Optimistic update
        const count = parseInt(countEl.textContent, 10) || 0;
        if (isLiked) {
            likeBtn.classList.remove('liked');
            countEl.textContent = count - 1;
        } else {
            likeBtn.classList.add('liked');
            countEl.textContent = count + 1;
        }

        // Send to server
        fetch('/Community/ToggleLike', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': window.PFUtils.getAntiForgeryToken()
            },
            body: JSON.stringify({ commentId: commentId })
        })
            .then(function (res) {
                return res.json();
            })
            .then(function (data) {
                if (data.success) {
                    countEl.textContent = data.likes;
                    if (data.isLiked) {
                        likeBtn.classList.add('liked');
                    } else {
                        likeBtn.classList.remove('liked');
                    }
                } else {
                    // Revert optimistic update
                    if (isLiked) {
                        likeBtn.classList.add('liked');
                    } else {
                        likeBtn.classList.remove('liked');
                    }
                    countEl.textContent = count;
                    showToast(data.error || 'Failed to update like.');
                }
            })
            .catch(function () {
                // Revert optimistic update
                if (isLiked) {
                    likeBtn.classList.add('liked');
                } else {
                    likeBtn.classList.remove('liked');
                }
                countEl.textContent = count;
                showToast('Failed to update like. Please try again.');
            });
    });

    // --- Poll Comment Submit ---
    document.addEventListener('click', function (e) {
        const submitBtn = e.target.closest('.community-poll-comment-submit');
        if (!submitBtn) {
            return;
        }

        const form = submitBtn.closest('.community-poll-comment-form');
        if (!form) {
            return;
        }

        const input = form.querySelector('textarea');
        if (!input || !input.value.trim()) {
            return;
        }

        const pollCard = submitBtn.closest('.community-poll-card');
        const pollCode = pollCard ? pollCard.dataset.pollId : '';

        const text = input.value.trim();
        input.value = ''; // Clear immediately

        fetch('/Community/AddComment', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': window.PFUtils.getAntiForgeryToken()
            },
            body: JSON.stringify({ pollCode: pollCode, text: text })
        })
            .then(function (res) {
                return res.json();
            })
            .then(function (data) {
                if (data.success) {
                    showToast('Comment posted!');
                    form.style.display = 'none';
                    const toggle = form.previousElementSibling;
                    if (toggle) {
                        toggle.setAttribute('aria-expanded', 'false');
                    }
                    // Prepend to comments list
                    prependComment(data.comment);
                } else {
                    input.value = text; // Restore on failure
                    showToast(data.error || 'Failed to post comment.');
                }
            })
            .catch(function () {
                input.value = text; // Restore on error
                showToast('Failed to post comment. Please try again.');
            });
    });

    // --- Story Comment Submit ---
    document.addEventListener('click', function (e) {
        const submitBtn = e.target.closest('.community-story-comment-submit');
        if (!submitBtn) {
            return;
        }

        const form = submitBtn.closest('.community-story-comment-form');
        if (!form) {
            return;
        }

        const input = form.querySelector('textarea');
        if (!input || !input.value.trim()) {
            return;
        }

        const card = submitBtn.closest('.community-story-card');
        const storyId = card ? parseInt(card.dataset.storyId, 10) : 0;

        const text = input.value.trim();
        input.value = ''; // Clear immediately

        fetch('/Community/AddComment', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': window.PFUtils.getAntiForgeryToken()
            },
            body: JSON.stringify({ storyId: storyId, text: text })
        })
            .then(function (res) {
                return res.json();
            })
            .then(function (data) {
                if (data.success) {
                    showToast('Comment posted!');
                    form.style.display = 'none';
                    const toggle = form.previousElementSibling;
                    if (toggle) {
                        toggle.setAttribute('aria-expanded', 'false');
                    }
                    // Update comment count
                    const countEl = card.querySelector('.community-story-comments-count');
                    if (countEl) {
                        const currentCount = parseInt(countEl.textContent.trim(), 10) || 0;
                        countEl.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg> ' + (currentCount + 1);
                    }
                    // Prepend to comments list
                    prependComment(data.comment);
                } else {
                    input.value = text; // Restore on failure
                    showToast(data.error || 'Failed to post comment.');
                }
            })
            .catch(function () {
                input.value = text; // Restore on error
                showToast('Failed to post comment. Please try again.');
            });
    });

    // --- Story Like Buttons ---
    document.addEventListener('click', function (e) {
        const likeBtn = e.target.closest('.community-story-like');
        if (!likeBtn) {
            return;
        }

        const storyId = parseInt(likeBtn.dataset.storyId, 10);
        const countEl = likeBtn.querySelector('.community-story-like-count');
        if (!countEl || !storyId) {
            return;
        }

        const isLiked = likeBtn.classList.contains('liked');

        // Optimistic update
        const count = parseInt(countEl.textContent, 10) || 0;
        if (isLiked) {
            likeBtn.classList.remove('liked');
            countEl.textContent = count - 1;
        } else {
            likeBtn.classList.add('liked');
            countEl.textContent = count + 1;
        }

        // Send to server
        fetch('/Community/ToggleStoryLike', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': window.PFUtils.getAntiForgeryToken()
            },
            body: JSON.stringify({ storyId: storyId })
        })
            .then(function (res) {
                return res.json();
            })
            .then(function (data) {
                if (data.success) {
                    countEl.textContent = data.likes;
                    if (data.isLiked) {
                        likeBtn.classList.add('liked');
                    } else {
                        likeBtn.classList.remove('liked');
                    }
                } else {
                    // Revert optimistic update
                    if (isLiked) {
                        likeBtn.classList.add('liked');
                    } else {
                        likeBtn.classList.remove('liked');
                    }
                    countEl.textContent = count;
                    showToast('Failed to update like.');
                }
            })
            .catch(function () {
                // Revert optimistic update
                if (isLiked) {
                    likeBtn.classList.add('liked');
                } else {
                    likeBtn.classList.remove('liked');
                }
                countEl.textContent = count;
                showToast('Failed to update like. Please try again.');
            });
    });

    // --- Story Comment Toggle ---
    document.addEventListener('click', function (e) {
        const toggle = e.target.closest('.community-story-comment-toggle');
        if (!toggle) {
            return;
        }

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
    });

    /**
   * Prepend a new comment to the recent comments list.
   */
    function prependComment (comment) {
        const list = document.getElementById('communityCommentsList');
        if (!list) {
            return;
        }

        // Remove "no comments" message if present
        const noComments = list.querySelector('.community-no-comments');
        if (noComments) {
            noComments.remove();
        }

        const timeStr = 'just now';

        const card = document.createElement('div');
        card.className = 'community-comment-card';
        card.dataset.commentId = comment.id;

        const initial = (comment.author || '?').charAt(0).toUpperCase();

        card.innerHTML =
      '<div class="community-comment-header">' +
        '<div class="community-comment-avatar" aria-hidden="true">' + escapeHtml(initial) + '</div>' +
        '<div class="community-comment-info">' +
          '<strong class="community-comment-author">' + escapeHtml(comment.author) + '</strong>' +
          ' <span class="community-comment-topic">' + escapeHtml(comment.topic || '') + '</span>' +
          ' <time class="community-comment-time">' + timeStr + '</time>' +
        '</div>' +
        '<button type="button" class="community-comment-like" data-comment-id="' + comment.id + '" aria-label="Like this comment">' +
          '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' +
            '<path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>' +
          '</svg>' +
          '<span class="community-comment-like-count">' + (comment.likes || 0) + '</span>' +
        '</button>' +
      '</div>' +
      '<p class="community-comment-text">' + escapeHtml(comment.text) + '</p>';

        // Insert at the beginning
        if (list.firstChild) {
            list.insertBefore(card, list.firstChild);
        } else {
            list.appendChild(card);
        }
    }

    /**
   * Escape HTML to prevent XSS.
   */
    function escapeHtml (str) {
        if (!str) {
            return '';
        }
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    // --- Story Submission Form ---
    const submitStoryBtn = document.getElementById('submitStoryBtn');
    if (submitStoryBtn) {
        submitStoryBtn.addEventListener('click', function () {
            const form = document.getElementById('submitStoryForm');
            if (!form) {
                return;
            }

            const title = document.getElementById('storyTitle');
            const category = document.getElementById('storyCategory');
            const summary = document.getElementById('storySummary');
            const body = document.getElementById('storyBody');

            // Validate
            let isValid = true;

            [title, category, summary, body].forEach(function (field) {
                if (field) {
                    if (!field.value.trim() || (field.id === 'storyBody' && field.value.trim().length < 100)) {
                        field.classList.add('is-invalid');
                        isValid = false;
                    } else {
                        field.classList.remove('is-invalid');
                    }
                }
            });

            if (!isValid) {
                return;
            }

            // Disable button during submission
            submitStoryBtn.disabled = true;
            submitStoryBtn.textContent = 'Submitting...';

            fetch('/Community/SubmitStory', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': window.PFUtils.getAntiForgeryToken()
                },
                body: JSON.stringify({
                    title: title.value.trim(),
                    category: category.value.trim(),
                    summary: summary.value.trim(),
                    body: body.value.trim()
                })
            })
                .then(function (res) {
                    return res.json();
                })
                .then(function (data) {
                    if (data.success) {
                        showToast('Your story has been submitted successfully!');
                        // Reset form
                        form.reset();
                        [title, category, summary, body].forEach(function (field) {
                            if (field) {
                                field.classList.remove('is-invalid');
                            }
                        });
                        // Close modal
                        const modalEl = document.getElementById('submitStoryModal');
                        if (modalEl) {
                            const modal = bootstrap.Modal.getInstance(modalEl);
                            if (modal) {
                                modal.hide();
                            }
                        }
                    } else {
                        showToast(data.error || 'Failed to submit story.');
                    }
                })
                .catch(function () {
                    showToast('Failed to submit story. Please try again.');
                })
                .finally(function () {
                    submitStoryBtn.disabled = false;
                    submitStoryBtn.innerHTML = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align: middle; margin-right: 0.25rem;"><line x1="22" y1="2" x2="11" y2="13"/><polygon points="22 2 15 22 11 13 2 9 22 2"/></svg> Submit Story';
                });
        });
    }

    // --- Real-time stats update after vote ---
    // (Stats are refreshed on page reload; no AJAX needed for simple counters)

})();
