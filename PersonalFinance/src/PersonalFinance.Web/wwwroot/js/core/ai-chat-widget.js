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
        {
            icon: '📊',
            label: 'Analyze my budget',
            message: 'Analyze my budget allocations and show me spending patterns.'
        },
        {icon: '💰', label: 'Savings tips', message: 'Suggest areas where I can reduce spending to save more money.'},
        {
            icon: '📈',
            label: 'Net worth outlook',
            message: 'Based on my current finances, project my net worth over the next 5 years.'
        },
        {icon: '🎯', label: 'Budget planning', message: 'Help me apply the 50/30/20 budgeting rule to my finances.'},
        {icon: '🔍', label: 'Spending patterns', message: 'Review my recent transactions and identify spending trends.'},
        {icon: '🏦', label: 'Retirement check', message: 'Help me understand if I\'m on track for retirement.'}
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
    function detectPageContext() {
        const path = window.location.pathname;
        if (path.indexOf('/GoogleDrive') >= 0) return 'GoogleDrive';
        if (path.indexOf('/Networth') >= 0) return 'Networth';
        if (path.indexOf('/News') >= 0) return 'News';
        if (path.indexOf('/Community') >= 0) return 'Community';
        return 'Home';
    }

    /**
     * Get a contextual greeting based on the current page.
     * @returns {string}
     */
    function getGreeting() {
        return PAGE_GREETINGS[detectPageContext()] || PAGE_GREETINGS.Home;
    }

    /**
     * Escape HTML special characters for safe insertion.
     * @param {string} text
     * @returns {string}
     */
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Escape special characters for HTML attribute values.
     * @param {string} text
     * @returns {string}
     */
    function escapeAttr(text) {
        return text.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/'/g, '&#39;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    /**
     * Convert markdown text to safe HTML for assistant messages.
     * @param {string} markdown
     * @returns {string}
     */
    function renderMarkdown(markdown) {
        if (typeof marked !== 'undefined') {
            try {
                return marked.parse(markdown, { async: false });
            } catch (_e) {
                // Fall through to plain-text fallback
            }
        }
        return markdown.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\n/g, '<br>');
    }

    /**
     * Build the complete widget DOM and append it to the body.
     */
    function createWidget() {
        // Floating action button
        const button = document.createElement('button');
        button.id = 'ai-chat-button';
        button.className = 'ai-chat-button';
        button.setAttribute('aria-label', 'Open AI Assistant');
        button.setAttribute('title', 'AI Assistant');
        button.innerHTML =
            '<svg class="ai-chat-button-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor">'
            + '<path d="M8 16A8 8 0 1 0 8 0a8 8 0 0 0 0 16zm.93-9.472a.235.235 0 0 0-.42.022A4.357 4.357 0 0 1 6 8.529v.061a.236.236 0 0 0 .18.228 3.52 3.52 0 0 1 .952.24c.26.138.483.3.592.516.233.457.271.982.096 1.438A.236.236 0 0 0 7.64 11h.088a.236.236 0 0 0 .225-.168.236.236 0 0 0 .236-.202c.039-.315.014-.626-.059-.919a.236.236 0 0 0-.164-.184.236.236 0 0 0-.184.157c-.053.173-.086.353-.11.527a.236.236 0 0 0 .134.936.236.236 0 0 0 .225.168h.088z"/>'
            + '<path d="M8 1.5c-.253 0-.497.1-.68.28L7.5 2v1.5l-.12.12A1.5 1.5 0 0 0 6 5v1a1.5 1.5 0 0 0 1.5 1.5h1A1.5 1.5 0 0 0 10 6V5a1.5 1.5 0 0 0-.38-1.38L9.5 3.5V2l-.12-.12A1.5 1.5 0 0 0 8 1.5z"/>'
            + '</svg>'
            + '<span class="ai-chat-button-close" aria-hidden="true">&times;</span>';

        // Chat panel
        const panel = document.createElement('div');
        panel.id = 'ai-chat-panel';
        panel.className = 'ai-chat-panel';
        panel.setAttribute('role', 'dialog');
        panel.setAttribute('aria-label', 'AI Assistant Chat');
        panel.setAttribute('aria-hidden', 'true');

        panel.innerHTML =
            '<div class="ai-chat-header">'
            + '  <div class="ai-chat-header-info">'
            + '    <span class="ai-chat-header-dot" aria-hidden="true"></span>'
            + '    <span class="ai-chat-header-title">AI Assistant</span>'
            + '  </div>'
            + '  <div class="ai-chat-header-actions">'
            + '    <button type="button" class="ai-chat-btn-icon" id="ai-chat-reset" title="New chat" aria-label="Start new chat">'
            + '      <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16">'
            + '        <path d="M11.534 7h3.932a.235.235 0 0 1 .193.383l-1.932 2.222A1.162 1.162 0 0 1 12.866 10H11.5a2.5 2.5 0 0 1-2.5-2.5V4.5a.5.5 0 0 1 1 0v3A1.5 1.5 0 0 0 11.5 9z"/>'
            + '        <path d="M3.717 11H2.5a.5.5 0 0 1 0-1h1.217A11.974 11.974 0 0 1 1 8c0-2.507.956-4.794 2.517-6.432a.473.473 0 0 1 .692.647A10.974 10.974 0 0 0 2 8a10.974 10.974 0 0 0 2.217 6.783z"/>'
            + '      </svg>'
            + '    </button>'
            + '    <button type="button" class="ai-chat-btn-icon" id="ai-chat-close" title="Close" aria-label="Close">'
            + '      <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16">'
            + '        <path d="M4.646 4.646a.5.5 0 0 1 .708 0L8 7.293l2.646-2.647a.5.5 0 0 1 .708.708L8.707 8l2.647 2.646a.5.5 0 0 1-.708.708L8 8.707l-2.646 2.647a.5.5 0 0 1-.708-.708L7.293 8 4.646 5.354a.5.5 0 0 1 0-.708z"/>'
            + '      </svg>'
            + '    </button>'
            + '  </div>'
            + '</div>'
            + '<div class="ai-chat-body" id="ai-chat-messages"></div>'
            + '<div class="ai-chat-footer">'
            + '  <form id="ai-chat-form" class="ai-chat-form">'
            + '    <input type="text" class="ai-chat-input" id="ai-chat-input" placeholder="Ask about your finances..." aria-label="Chat message" autocomplete="off" />'
            + '    <button type="submit" class="ai-chat-send-btn" id="ai-chat-send" disabled aria-label="Send message">'
            + '      <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16">'
            + '        <path d="M15.964.686a.5.5 0 0 0-.65-.65L.767 5.855H.766l-.452.18a.5.5 0 0 0-.082.887l.41.26.001.002 4.995 3.178 3.178 4.995.002.002.26.41a.5.5 0 0 0 .886-.083l6-15zm-1.833 1.89L6.637 10.07l-.295-.06a.76.76 0 0 0-.155 0H5.29L.42.924l13.68.752z"/>'
            + '      </svg>'
            + '    </button>'
            + '  </form>'
            + '</div>';

        document.body.appendChild(button);
        document.body.appendChild(panel);
    }

    /**
     * Render the welcome screen with quick action chips.
     */
    function renderWelcome() {
        const container = document.getElementById('ai-chat-messages');
        if (!container) return;

        const greeting = getGreeting();
        let html =
            '<div class="ai-chat-welcome">'
            + '  <div class="ai-chat-welcome-icon" aria-hidden="true">💡</div>'
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
    function addMessage(role, content) {
        const container = document.getElementById('ai-chat-messages');
        if (!container) return;

        const wrapper = document.createElement('div');
        wrapper.className = 'ai-chat-message ai-chat-message-' + role;

        const bubble = document.createElement('div');
        bubble.className = 'ai-chat-bubble';

        if (role === 'assistant') {
            bubble.innerHTML = renderMarkdown(content);
        } else {
            bubble.textContent = content;
        }

        wrapper.appendChild(bubble);
        container.appendChild(wrapper);
        container.scrollTop = container.scrollHeight;

        // Keep conversation history bounded
        state.history.push({role: role, content: content});
        if (state.history.length > MAX_HISTORY) {
            state.history = state.history.slice(-MAX_HISTORY);
        }
    }

    /**
     * Show the animated typing indicator.
     */
    function showTyping() {
        const container = document.getElementById('ai-chat-messages');
        if (!container) return;

        const el = document.createElement('div');
        el.className = 'ai-chat-message ai-chat-message-assistant';
        el.id = 'ai-chat-typing';
        el.innerHTML = '<div class="ai-chat-bubble ai-chat-typing"><span></span><span></span><span></span></div>';
        container.appendChild(el);
        container.scrollTop = container.scrollHeight;
    }

    /**
     * Remove the typing indicator.
     */
    function hideTyping() {
        const el = document.getElementById('ai-chat-typing');
        if (el) el.remove();
    }

    /**
     * Send a message to the AI backend.
     * @param {string} message
     */
    async function sendMessage(message) {
        if (state.isLoading || !message.trim()) return;
        state.isLoading = true;

        const input = document.getElementById('ai-chat-input');
        const sendBtn = document.getElementById('ai-chat-send');
        if (input) input.disabled = true;
        if (sendBtn) sendBtn.disabled = true;

        // Clear welcome and show conversation
        const container = document.getElementById('ai-chat-messages');
        if (container) container.innerHTML = '';
        state.hasInteracted = true;

        addMessage('user', message);
        if (input) input.value = '';
        showTyping();

        try {
            const historyToSend = state.history.slice(-MAX_HISTORY_SENT);
            const response = await fetch(API_ENDPOINT, {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({
                    message: message,
                    conversationHistory: historyToSend
                })
            });

            if (!response.ok) throw new Error('API request failed');

            const data = await response.json();
            hideTyping();
            addMessage('assistant', data.response);
        } catch (_error) {
            hideTyping();
            addMessage('assistant', 'I\'m sorry, I encountered an error. Please try again later.');
        } finally {
            state.isLoading = false;
            if (input) input.disabled = false;
            if (sendBtn) sendBtn.disabled = !input || !input.value.trim();
        }
    }

    /**
     * Open the chat panel.
     */
    function openChat() {
        const panel = document.getElementById('ai-chat-panel');
        const button = document.getElementById('ai-chat-button');
        if (!panel || !button) return;

        state.isOpen = true;
        panel.classList.add('ai-chat-panel-open');
        panel.setAttribute('aria-hidden', 'false');
        button.classList.add('ai-chat-button-active');

        if (!state.hasInteracted) {
            requestAnimationFrame(renderWelcome);
        }

        const input = document.getElementById('ai-chat-input');
        if (input) input.focus();
    }

    /**
     * Close the chat panel.
     */
    function closeChat() {
        const panel = document.getElementById('ai-chat-panel');
        const button = document.getElementById('ai-chat-button');
        if (!panel || !button) return;

        state.isOpen = false;
        panel.classList.remove('ai-chat-panel-open');
        panel.setAttribute('aria-hidden', 'true');
        button.classList.remove('ai-chat-button-active');
    }

    /**
     * Toggle the chat panel.
     */
    function toggleChat() {
        state.isOpen ? closeChat() : openChat();
    }

    /**
     * Reset the chat to the welcome state.
     */
    function resetChat() {
        state.history = [];
        state.hasInteracted = false;
        renderWelcome();
    }

    /**
     * Wire up all event listeners.
     */
    function initEvents() {
        const button = document.getElementById('ai-chat-button');
        const closeBtn = document.getElementById('ai-chat-close');
        const resetBtn = document.getElementById('ai-chat-reset');
        const form = document.getElementById('ai-chat-form');
        const input = document.getElementById('ai-chat-input');

        if (button) button.addEventListener('click', toggleChat);

        if (closeBtn) closeBtn.addEventListener('click', closeChat);

        if (resetBtn) resetBtn.addEventListener('click', resetChat);

        if (form) {
            form.addEventListener('submit', function (e) {
                e.preventDefault();
                if (input && input.value.trim()) {
                    sendMessage(input.value.trim());
                }
            });
        }

        if (input) {
            input.addEventListener('input', function () {
                const sendBtn = document.getElementById('ai-chat-send');
                if (sendBtn) sendBtn.disabled = !this.value.trim();
            });
        }

        // Delegate chip clicks
        const messagesContainer = document.getElementById('ai-chat-messages');
        if (messagesContainer) {
            messagesContainer.addEventListener('click', function (e) {
                const chip = e.target.closest('.ai-chat-chip');
                if (!chip) return;
                const index = parseInt(chip.getAttribute('data-index'), 10);
                if (!isNaN(index) && QUICK_ACTIONS[index]) {
                    sendMessage(QUICK_ACTIONS[index].message);
                }
            });
        }

        // Escape key closes panel
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && state.isOpen) closeChat();
        });

        // Close on outside click (use capture phase so it runs before inner handlers that may mutate the DOM)
        document.addEventListener('click', function (e) {
            if (!state.isOpen) return;
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
    function init() {
        createWidget();
        initEvents();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
