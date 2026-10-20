/**
 * AI Assistant Chat Widget.
 * A floating chat panel that provides AI-powered personal finance insights
 * with contextual quick actions and a clean, modern interface.
 */
(function () {
    'use strict';

    const API_ENDPOINT = '/api/AiAssistant/chat';
    const MAX_HISTORY = 20;
    const MAX_HISTORY_SENT = 10;

    const state = {
        isOpen: false,
        isLoading: false,
        history: [],
        hasInteracted: false
    };

    /**
     * Quick action suggestions grouped by context.
     * @type {Array<{icon: string, label: string, message: string}>}
     */
    const QUICK_ACTIONS = [
        { icon: '\u2197', label: 'Find room to save', message: 'Review my budget and suggest practical ways to save more.' },
        { icon: '\u25ce', label: 'Review my budget', message: 'Analyze my budget allocations and show me spending patterns.' },
        { icon: '\u2301', label: 'Explore my net worth', message: 'Help me understand my net worth and progress toward financial independence.' },
        { icon: '\u2192', label: 'Plan my next step', message: 'Help me prioritize my savings goals and choose a practical next step.' }
    ];

    /**
     * Context-aware greeting messages per page.
     */
    const PAGE_GREETINGS = {
        GoogleDrive: 'I can help analyze your budget, review transactions, and plan your finances.',
        Networth: 'I can help project your future net worth, break down assets, and track progress.',
        News: 'I can help analyze your budget, review transactions, and plan your finances.',
        Community: 'I can help analyze your budget, review transactions, and plan your finances.',
        Home: 'I can help you analyze your budget, review transactions, create a budget plan, and project your net worth.'
    };

    /**
     * Detect the current page context from the URL path.
     * @returns {string}
     */
    function detectPageContext () {
        const path = window.location.pathname;
        if (path.indexOf('/GoogleDrive') >= 0) {
            return 'GoogleDrive';
        }
        if (path.indexOf('/Networth') >= 0) {
            return 'Networth';
        }
        if (path.indexOf('/News') >= 0) {
            return 'News';
        }
        if (path.indexOf('/Community') >= 0) {
            return 'Community';
        }
        return 'Home';
    }

    /**
     * Get a contextual greeting based on the current page.
     * @returns {string}
     */
    function getGreeting () {
        return PAGE_GREETINGS[detectPageContext()] || PAGE_GREETINGS.Home;
    }

    /**
     * Escape HTML special characters for safe insertion.
     * @param {string} text
     * @returns {string}
     */
    function escapeHtml (text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Convert markdown text to safe HTML for assistant messages.
     * @param {string} markdown
     * @returns {DocumentFragment}
     */
    function renderMarkdown (markdown) {
        const fragment = document.createDocumentFragment();
        if (typeof marked === 'undefined') {
            fragment.appendChild(document.createTextNode(markdown));
            return fragment;
        }
        // Rebuild only formatting elements; discard raw HTML attributes and unsafe links.
        const parsed = new DOMParser().parseFromString(marked.parse(markdown, { async: false }), 'text/html');
        const allowed = new Set(['P', 'BR', 'STRONG', 'EM', 'UL', 'OL', 'LI', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'BLOCKQUOTE', 'CODE', 'PRE', 'HR', 'TABLE', 'THEAD', 'TBODY', 'TR', 'TH', 'TD', 'A']);
        function appendSafe (source, target) {
            source.childNodes.forEach(function (node) {
                if (node.nodeType === Node.TEXT_NODE) {
                    target.appendChild(document.createTextNode(node.textContent));
                } else if (allowed.has(node.nodeName)) {
                    const element = document.createElement(node.nodeName.toLowerCase());
                    if (node.nodeName === 'A') {
                        const href = node.getAttribute('href') || '';
                        if (/^https?:\/\//i.test(href)) {
                            element.setAttribute('href', href);
                            element.setAttribute('target', '_blank');
                            element.setAttribute('rel', 'noopener noreferrer');
                        }
                    }
                    appendSafe(node, element);
                    target.appendChild(element);
                }
            });
        }
        appendSafe(parsed.body, fragment);
        return fragment;
    }

    /**
     * Build the complete widget DOM and append it to the body.
     */
    function createWidget () {
        // Floating action button
        const button = document.createElement('button');
        button.id = 'ai-chat-button';
        button.className = 'ai-chat-button';
        button.setAttribute('aria-label', 'Open AI Assistant');
        button.setAttribute('title', 'AI Assistant');
        button.type = 'button';
        button.setAttribute('aria-controls', 'ai-chat-panel');
        button.setAttribute('aria-expanded', 'false');
        button.innerHTML = '<span class="ai-chat-spark" aria-hidden="true">&#10022;</span><span class="ai-chat-launcher-label">Ask assistant</span>';

        const panel = document.createElement('section');
        panel.id = 'ai-chat-panel';
        panel.className = 'ai-chat-panel';
        panel.hidden = true;
        panel.setAttribute('role', 'dialog');
        panel.setAttribute('aria-labelledby', 'ai-chat-title');
        panel.innerHTML = `
            <div class="ai-chat-header">
                <div class="ai-chat-header-info">
                    <span class="ai-chat-mark" aria-hidden="true">&#10022;</span>
                    <div><h2 id="ai-chat-title">AI Assistant</h2><p>Your finances, a little clearer</p></div>
                </div>
                <div class="ai-chat-header-actions">
                    <button type="button" class="ai-chat-btn-icon" id="ai-chat-reset" title="New chat" aria-label="Start new chat">+</button>
                    <button type="button" class="ai-chat-btn-icon" id="ai-chat-close" title="Close" aria-label="Close assistant">&times;</button>
                </div>
            </div>
            <div class="ai-chat-body" id="ai-chat-messages" role="log" aria-label="Conversation" aria-live="polite" aria-relevant="additions"></div>
            <p class="ai-chat-status" id="ai-chat-status" role="status"></p>
            <div class="ai-chat-footer">
                <form id="ai-chat-form" class="ai-chat-form">
                    <textarea class="ai-chat-input" id="ai-chat-input" rows="2" maxlength="2000" placeholder="What would you like to understand?" aria-label="Message the assistant" aria-describedby="ai-chat-input-hint"></textarea>
                    <div class="ai-chat-composer-actions">
                        <span id="ai-chat-input-hint">Enter to send &middot; Shift + Enter for a new line</span>
                        <button type="submit" class="ai-chat-send-btn" id="ai-chat-send" disabled aria-label="Send message">&uarr;</button>
                    </div>
                </form>
                <p class="ai-chat-disclaimer">AI can make mistakes. For guidance, not financial advice.</p>
            </div>`;

        document.body.appendChild(button);
        document.body.appendChild(panel);
    }

    /**
     * Render the welcome screen with quick action chips.
     */
    function renderWelcome () {
        const container = document.getElementById('ai-chat-messages');
        if (!container) {
            return;
        }

        const greeting = getGreeting();
        let html =
            '<div class="ai-chat-welcome">'
            + '  <div class="ai-chat-welcome-icon" aria-hidden="true">&#10022;</div>'
            + '  <p class="ai-chat-eyebrow">YOUR FINANCE COMPANION</p>'
            + '  <h3>Make sense of your money.</h3>'
            + '  <p class="ai-chat-welcome-text">' + escapeHtml(greeting) + '</p>'
            + '  <p class="ai-chat-welcome-hint">Pick a topic or type your own question</p>'
            + '</div>';

        html += '<div class="ai-chat-quick-actions">';
        QUICK_ACTIONS.forEach(function (action, index) {
            html += '<button type="button" class="ai-chat-chip" data-index="' + index + '">'
                + '<span class="ai-chat-chip-icon" aria-hidden="true">' + action.icon + '</span>'
                + '<span>' + escapeHtml(action.label) + '</span>'
                + '</button>';
        });
        html += '</div>';

        container.innerHTML = html;
        container.scrollTop = 0;
    }

    /**
     * Add a chat message bubble to the conversation.
     * @param {string} role - "user" or "assistant"
     * @param {string} content
     */
    function addMessage (role, content) {
        const container = document.getElementById('ai-chat-messages');
        if (!container) {
            return;
        }

        const wrapper = document.createElement('div');
        wrapper.className = 'ai-chat-message ai-chat-message-' + role;

        const bubble = document.createElement('div');
        bubble.className = 'ai-chat-bubble';

        if (role === 'assistant') {
            bubble.appendChild(renderMarkdown(content));
        } else {
            bubble.textContent = content;
        }

        const label = document.createElement('span');
        label.className = 'ai-chat-message-label';
        label.textContent = role === 'user' ? 'You' : 'Assistant';
        wrapper.appendChild(label);
        wrapper.appendChild(bubble);
        container.appendChild(wrapper);
        container.scrollTop = container.scrollHeight;

        // Keep conversation history bounded
        state.history.push({ role: role, content: content });
        if (state.history.length > MAX_HISTORY) {
            state.history = state.history.slice(-MAX_HISTORY);
        }
    }

    /**
     * Show the animated typing indicator.
     */
    function showTyping () {
        const container = document.getElementById('ai-chat-messages');
        if (!container) {
            return;
        }

        const el = document.createElement('div');
        el.className = 'ai-chat-message ai-chat-message-assistant';
        el.id = 'ai-chat-typing';
        el.setAttribute('aria-label', 'Assistant is thinking');
        el.innerHTML = '<div class="ai-chat-bubble ai-chat-typing"><span></span><span></span><span></span></div>';
        container.appendChild(el);
        container.scrollTop = container.scrollHeight;
    }

    /**
     * Remove the typing indicator.
     */
    function hideTyping () {
        const el = document.getElementById('ai-chat-typing');
        if (el) {
            el.remove();
        }
    }

    /**
     * Send a message to the AI backend.
     * @param {string} message
     */
    async function sendMessage (message) {
        if (state.isLoading || !message.trim()) {
            return;
        }
        const historyToSend = state.history.slice(-MAX_HISTORY_SENT);
        state.isLoading = true;
        document.getElementById('ai-chat-reset').disabled = true;
        document.getElementById('ai-chat-status').textContent = 'Thinking through your question...';

        const input = document.getElementById('ai-chat-input');
        const sendBtn = document.getElementById('ai-chat-send');
        if (input) {
            input.disabled = true;
        }
        if (sendBtn) {
            sendBtn.disabled = true;
        }

        // Clear welcome and show conversation
        const container = document.getElementById('ai-chat-messages');
        if (container && !state.hasInteracted) {
            container.innerHTML = '';
        }
        state.hasInteracted = true;

        addMessage('user', message);
        if (input) {
            input.value = '';
            input.style.height = '';
        }
        showTyping();

        try {
            const response = await fetch(API_ENDPOINT, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('meta[name="__RequestVerificationToken"]')?.content || ''
                },
                body: JSON.stringify({
                    message: message,
                    conversationHistory: historyToSend
                })
            });

            if (response.status === 429) {
                const error = await response.json();
                const limitError = new Error(error.error || 'AI usage limit reached. Please try again later.');
                limitError.isUsageLimit = true;
                throw limitError;
            }
            if (!response.ok) {
                throw new Error('API request failed');
            }

            const data = await response.json();
            hideTyping();
            if (typeof data.response !== 'string' || !data.response.trim()) {
                throw new Error('Empty response');
            }
            addMessage('assistant', data.response);
            document.getElementById('ai-chat-status').textContent = data.budgetWarning || '';
            const disclaimer = document.querySelector('.ai-chat-disclaimer');
            if (disclaimer && data.disclaimer) {
                disclaimer.textContent = data.disclaimer;
            }
        } catch (error) {
            hideTyping();
            state.history.pop();
            document.getElementById('ai-chat-status').textContent = error.isUsageLimit
                ? error.message
                : 'Couldn\u2019t get a reply. Your question is ready to try again.';
            if (input) {
                input.value = message;
            }
        } finally {
            state.isLoading = false; // eslint-disable-line require-atomic-updates
            document.getElementById('ai-chat-reset').disabled = false;
            if (input) {
                input.disabled = false;
                if (state.isOpen) {
                    input.focus();
                }
            }
            if (sendBtn) {
                sendBtn.disabled = !input || !input.value.trim();
            }
        }
    }

    /**
     * Open the chat panel.
     */
    function openChat () {
        const panel = document.getElementById('ai-chat-panel');
        const button = document.getElementById('ai-chat-button');
        if (!panel || !button) {
            return;
        }

        state.isOpen = true;
        panel.hidden = false;
        panel.classList.add('ai-chat-panel-open');
        button.setAttribute('aria-expanded', 'true');
        panel.setAttribute('aria-hidden', 'false');
        button.classList.add('ai-chat-button-active');

        if (!state.hasInteracted) {
            requestAnimationFrame(renderWelcome);
        }

        const input = document.getElementById('ai-chat-input');
        if (input) {
            input.focus();
        }
    }

    /**
     * Close the chat panel.
     */
    function closeChat () {
        const panel = document.getElementById('ai-chat-panel');
        const button = document.getElementById('ai-chat-button');
        if (!panel || !button) {
            return;
        }

        state.isOpen = false;
        panel.hidden = true;
        panel.classList.remove('ai-chat-panel-open');
        button.setAttribute('aria-expanded', 'false');
        if (panel.contains(document.activeElement)) {
            button.focus();
        }
        panel.setAttribute('aria-hidden', 'true');
        button.classList.remove('ai-chat-button-active');
    }

    /**
     * Toggle the chat panel.
     */
    function toggleChat () {
        if (state.isOpen) {
            closeChat();
        } else {
            openChat();
        }
    }

    /**
     * Reset the chat to the welcome state.
     */
    function resetChat () {
        if (state.isLoading) {
            return;
        }
        document.getElementById('ai-chat-status').textContent = '';
        state.history = [];
        state.hasInteracted = false;
        renderWelcome();
        const input = document.getElementById('ai-chat-input');
        input.value = '';
        input.style.height = '';
        document.getElementById('ai-chat-send').disabled = true;
        input.focus();
    }

    /**
     * Wire up all event listeners.
     */
    function initEvents () {
        const button = document.getElementById('ai-chat-button');
        const closeBtn = document.getElementById('ai-chat-close');
        const resetBtn = document.getElementById('ai-chat-reset');
        const form = document.getElementById('ai-chat-form');
        const input = document.getElementById('ai-chat-input');

        if (button) {
            button.addEventListener('click', toggleChat);
        }

        if (closeBtn) {
            closeBtn.addEventListener('click', closeChat);
        }

        if (resetBtn) {
            resetBtn.addEventListener('click', resetChat);
        }

        if (form) {
            form.addEventListener('submit', function (e) {
                e.preventDefault();
                if (input && input.value.trim()) {
                    sendMessage(input.value.trim());
                }
            });
        }

        if (input) {
            input.addEventListener('keydown', function (e) {
                if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) {
                    e.preventDefault();
                    form.requestSubmit();
                }
            });
            input.addEventListener('input', function () {
                const sendBtn = document.getElementById('ai-chat-send');
                if (sendBtn) {
                    sendBtn.disabled = state.isLoading || !this.value.trim();
                    this.style.height = 'auto';
                    this.style.height = Math.min(this.scrollHeight, 150) + 'px';
                }
            });
        }

        // Delegate chip clicks
        const messagesContainer = document.getElementById('ai-chat-messages');
        if (messagesContainer) {
            messagesContainer.addEventListener('click', function (e) {
                const chip = e.target.closest('.ai-chat-chip');
                if (!chip) {
                    return;
                }
                const index = parseInt(chip.getAttribute('data-index'), 10);
                if (!isNaN(index) && QUICK_ACTIONS[index]) {
                    sendMessage(QUICK_ACTIONS[index].message);
                }
            });
        }

        // Escape key closes panel
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && state.isOpen) {
                closeChat();
            }
        });

        // Close on outside click (use capture phase so it runs before inner handlers that may mutate the DOM)
        document.addEventListener('click', function (e) {
            if (!state.isOpen) {
                return;
            }
            const panel = document.getElementById('ai-chat-panel');
            const button = document.getElementById('ai-chat-button');
            if (panel && button && !panel.contains(e.target) && !button.contains(e.target)) {
                closeChat();
            }
        }, true);
    }

    /**
     * Initialize the widget.
     */
    function init () {
        createWidget();
        initEvents();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
