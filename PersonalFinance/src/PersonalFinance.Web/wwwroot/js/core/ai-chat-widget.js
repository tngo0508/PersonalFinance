/**
 * Floating AI Assistant Chat Widget.
 * Provides a chat bubble button that opens a chat panel for interacting with the AI assistant.
 */
(function () {
    'use strict';

    const chatState = {
        isOpen: false,
        isLoading: false,
        conversationHistory: [],
        messageCount: 0
    };

    /**
     * Create the chat widget HTML elements and append them to the body.
     * @returns {void}
     */
    function createWidget() {
        // Create the floating button
        const chatButton = document.createElement('button');
        chatButton.id = 'ai-chat-button';
        chatButton.className = 'ai-chat-button';
        chatButton.setAttribute('aria-label', 'Open AI Assistant');
        chatButton.setAttribute('title', 'AI Assistant');
        chatButton.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" fill="currentColor" viewBox="0 0 16 16"><path d="M14 1a1 1 0 0 1 1 1v8a1 1 0 0 1-1 1H4.414a1 1 0 0 0-.707.293L1.586 13.293A1 1 0 0 1 .293 13H0V2a1 1 0 0 1 1-1h13zm-1-1H1a2 2 0 0 0-2 2v10a2 2 0 0 0 .586 1.414l2.707 2.707A2 2 0 0 0 5.293 16H14a2 2 0 0 0 2-2V2a2 2 0 0 0-2-2H13z"/><path d="M3 4.5a.5.5 0 0 1 .5-.5h9a.5.5 0 0 1 0 1h-9a.5.5 0 0 1-.5-.5zm0 3a.5.5 0 0 1 .5-.5h9a.5.5 0 0 1 0 1h-9a.5.5 0 0 1-.5-.5zm0 3a.5.5 0 0 1 .5-.5h6a.5.5 0 0 1 0 1h-6a.5.5 0 0 1-.5-.5z"/></svg>';

        // Create the chat panel
        const chatPanel = document.createElement('div');
        chatPanel.id = 'ai-chat-panel';
        chatPanel.className = 'ai-chat-panel';
        chatPanel.setAttribute('role', 'dialog');
        chatPanel.setAttribute('aria-label', 'AI Assistant Chat');

        chatPanel.innerHTML =
            '<div class="ai-chat-header">' +
                '<h5 class="mb-0">AI Assistant</h5>' +
                '<button type="button" class="btn-close" id="ai-chat-close" aria-label="Close"></button>' +
            '</div>' +
            '<div class="ai-chat-messages" id="ai-chat-messages"></div>' +
            '<div class="ai-chat-quick-actions" id="ai-chat-quick-actions">' +
                '<button type="button" class="btn btn-sm btn-outline-primary quick-action" data-message="Analyze my budget allocations">Analyze my budget</button>' +
                '<button type="button" class="btn btn-sm btn-outline-primary quick-action" data-message="Review my net worth progress">Review net worth</button>' +
                '<button type="button" class="btn btn-sm btn-outline-primary quick-action" data-message="Give me savings tips">Savings tips</button>' +
            '</div>' +
            '<div class="ai-chat-input">' +
                '<form id="ai-chat-form">' +
                    '<div class="input-group">' +
                        '<input type="text" class="form-control" id="ai-chat-input" placeholder="Ask me anything about your finances..." aria-label="Chat message" />' +
                        '<button type="submit" class="btn btn-primary" id="ai-chat-send" disabled>' +
                            '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16"><path d="M15.964.686a.5.5 0 0 0-.65-.65L.767 5.855H.766l-.452.18a.5.5 0 0 0-.082.887l.41.26.001.002 4.995 3.178 3.178 4.995.002.002.26.41a.5.5 0 0 0 .886-.083l6-15zm-1.833 1.89L6.637 10.07l-.295-.06a.76.76 0 0 0-.155 0H5.29L.42.924l13.68.752z"/></svg>' +
                        '</button>' +
                    '</div>' +
                '</form>' +
            '</div>';

        document.body.appendChild(chatButton);
        document.body.appendChild(chatPanel);

        // Add welcome message
        addMessage('assistant', 'Hello! I\'m your personal finance assistant. How can I help you today?');
    }

    /**
     * Add a message to the chat panel.
     * @param {string} role - The role of the message ("user" or "assistant").
     * @param {string} content - The message content.
     * @returns {void}
     */
    function addMessage(role, content) {
        const messagesContainer = document.getElementById('ai-chat-messages');
        if (!messagesContainer) return;

        const messageDiv = document.createElement('div');
        messageDiv.className = 'ai-chat-message ai-chat-message-' + role;

        const bubbleDiv = document.createElement('div');
        bubbleDiv.className = 'ai-chat-bubble';
        bubbleDiv.textContent = content;

        messageDiv.appendChild(bubbleDiv);
        messagesContainer.appendChild(messageDiv);

        // Scroll to bottom
        messagesContainer.scrollTop = messagesContainer.scrollHeight;

        // Add to conversation history (limit to last 20 messages)
        chatState.conversationHistory.push({ role: role, content: content });
        if (chatState.conversationHistory.length > 20) {
            chatState.conversationHistory = chatState.conversationHistory.slice(-20);
        }

        chatState.messageCount++;
    }

    /**
     * Show a typing indicator in the chat panel.
     * @returns {void}
     */
    function showTypingIndicator() {
        const messagesContainer = document.getElementById('ai-chat-messages');
        if (!messagesContainer) return;

        const typingDiv = document.createElement('div');
        typingDiv.className = 'ai-chat-message ai-chat-message-assistant';
        typingDiv.id = 'ai-chat-typing';

        const bubbleDiv = document.createElement('div');
        bubbleDiv.className = 'ai-chat-bubble ai-chat-typing-bubble';
        bubbleDiv.innerHTML = '<span></span><span></span><span></span>';

        typingDiv.appendChild(bubbleDiv);
        messagesContainer.appendChild(typingDiv);
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    /**
     * Remove the typing indicator from the chat panel.
     * @returns {void}
     */
    function removeTypingIndicator() {
        const typingIndicator = document.getElementById('ai-chat-typing');
        if (typingIndicator) {
            typingIndicator.remove();
        }
    }

    /**
     * Send a message to the AI assistant.
     * @param {string} message - The user's message.
     * @returns {void}
     */
    async function sendMessage(message) {
        if (chatState.isLoading || !message.trim()) return;

        chatState.isLoading = true;

        // Disable input and button
        const input = document.getElementById('ai-chat-input');
        const sendButton = document.getElementById('ai-chat-send');
        if (input) input.disabled = true;
        if (sendButton) sendButton.disabled = true;

        // Add user message to chat
        addMessage('user', message);

        // Clear input
        if (input) input.value = '';

        // Show typing indicator
        showTypingIndicator();

        try {
            const response = await fetch('/api/AiAssistant/chat', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    message: message,
                    conversationHistory: chatState.conversationHistory
                })
            });

            if (!response.ok) {
                throw new Error('Failed to get response from AI assistant');
            }

            const data = await response.json();
            removeTypingIndicator();
            addMessage('assistant', data.response);
        } catch (error) {
            removeTypingIndicator();
            addMessage('assistant', 'I\'m sorry, I encountered an error. Please try again later.');
        } finally {
            chatState.isLoading = false;
            if (input) input.disabled = false;
            if (sendButton) sendButton.disabled = !input.value.trim();
        }
    }

    /**
     * Toggle the chat panel open/closed.
     * @returns {void}
     */
    function toggleChat() {
        const panel = document.getElementById('ai-chat-panel');
        const button = document.getElementById('ai-chat-button');
        if (!panel || !button) return;

        chatState.isOpen = !chatState.isOpen;

        if (chatState.isOpen) {
            panel.classList.add('ai-chat-panel-open');
            button.classList.add('ai-chat-button-active');
            const input = document.getElementById('ai-chat-input');
            if (input) input.focus();
        } else {
            panel.classList.remove('ai-chat-panel-open');
            button.classList.remove('ai-chat-button-active');
        }
    }

    /**
     * Initialize event listeners for the chat widget.
     * @returns {void}
     */
    function initEventListeners() {
        // Chat button click
        const chatButton = document.getElementById('ai-chat-button');
        if (chatButton) {
            chatButton.addEventListener('click', toggleChat);
        }

        // Close button click
        const closeButton = document.getElementById('ai-chat-close');
        if (closeButton) {
            closeButton.addEventListener('click', toggleChat);
        }

        // Form submission
        const form = document.getElementById('ai-chat-form');
        if (form) {
            form.addEventListener('submit', function (e) {
                e.preventDefault();
                const input = document.getElementById('ai-chat-input');
                if (input && input.value.trim()) {
                    sendMessage(input.value.trim());
                }
            });
        }

        // Input field - enable/disable send button
        const input = document.getElementById('ai-chat-input');
        if (input) {
            input.addEventListener('input', function () {
                const sendButton = document.getElementById('ai-chat-send');
                if (sendButton) {
                    sendButton.disabled = !this.value.trim();
                }
            });
        }

        // Quick action buttons
        const quickActionsContainer = document.getElementById('ai-chat-quick-actions');
        if (quickActionsContainer) {
            const quickButtons = quickActionsContainer.querySelectorAll('.quick-action');
            quickButtons.forEach(function (btn) {
                btn.addEventListener('click', function () {
                    const message = this.getAttribute('data-message');
                    if (message) {
                        sendMessage(message);
                    }
                });
            });
        }

        // Close on Escape key
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && chatState.isOpen) {
                toggleChat();
            }
        });
    }

    /**
     * Initialize the AI chat widget.
     * @returns {void}
     */
    function init() {
        createWidget();
        initEventListeners();
    }

    // Wait for DOM readiness
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
