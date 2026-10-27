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
     * The widget's text in the page language, from the Shell.Js.Ai.* strings.
     * @param {string} key - The key after Shell.Js.Ai.
     * @returns {string}
     */
    function t (key) {
        return window.PFI18n.t(`Shell.Js.Ai.${ key}`);
    }

    /**
     * Quick action suggestions: the chip label and the message it sends, both Shell.Js.Ai.QuickAction.<key>.*.
     * @type {Array<{icon: string, key: string}>}
     */
    const QUICK_ACTIONS = [
        { icon: '\u2197', key: 'Save' },
        { icon: '\u25ce', key: 'Budget' },
        { icon: '\u2301', key: 'NetWorth' },
        { icon: '\u2192', key: 'NextStep' }
    ];

    /**
     * The greeting key (Shell.Js.Ai.Greeting.<key>) per page context.
     */
    const PAGE_GREETINGS = {
        GoogleDrive: 'Default',
        Networth: 'NetWorth',
        News: 'Default',
        Community: 'Default',
        Home: 'Home'
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
        return t(`Greeting.${ PAGE_GREETINGS[detectPageContext()] || PAGE_GREETINGS.Home}`);
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
            source.childNodes.forEach((node) => {
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
     * Create an element with attributes and children. All text goes in through text nodes, never as HTML.
     * @param {string} tag - The tag name.
     * @param {Object<string, string|boolean>} [attrs] - Attributes; true sets an empty attribute, false skips it.
     * @param {Array<Node|string>} [children] - Child nodes; strings become text nodes.
     * @returns {HTMLElement}
     */
    function el (tag, attrs, children) {
        const element = document.createElement(tag);
        Object.entries(attrs || {}).forEach(([name, value]) => {
            if (value === true) {
                element.setAttribute(name, '');
            } else if (value !== false) {
                element.setAttribute(name, value);
            }
        });
        element.append(...(children || []));
        return element;
    }

    /**
     * Build the complete widget DOM and append it to the body.
     */
    function createWidget () {
        // Floating action button
        const button = el('button', {
            id: 'ai-chat-button',
            class: 'ai-chat-button',
            'aria-label': t('Open'),
            title: t('Title'),
            type: 'button',
            'aria-controls': 'ai-chat-panel',
            'aria-expanded': 'false'
        }, [
            el('span', { class: 'ai-chat-spark', 'aria-hidden': 'true' }, ['✦']),
            el('span', { class: 'ai-chat-launcher-label' }, [t('Launcher')])
        ]);

        const header = el('div', { class: 'ai-chat-header' }, [
            el('div', { class: 'ai-chat-header-info' }, [
                el('span', { class: 'ai-chat-mark', 'aria-hidden': 'true' }, ['✦']),
                el('div', {}, [el('h2', { id: 'ai-chat-title' }, [t('Title')]), el('p', {}, [t('Subtitle')])])
            ]),
            el('div', { class: 'ai-chat-header-actions' }, [
                el('button', { type: 'button', class: 'ai-chat-btn-icon', id: 'ai-chat-reset', title: t('NewChat'), 'aria-label': t('NewChatLabel') }, ['+']),
                el('button', { type: 'button', class: 'ai-chat-btn-icon', id: 'ai-chat-close', title: t('Close'), 'aria-label': t('CloseLabel') }, ['×'])
            ])
        ]);

        const footer = el('div', { class: 'ai-chat-footer' }, [
            el('form', { id: 'ai-chat-form', class: 'ai-chat-form' }, [
                el('textarea', {
                    class: 'ai-chat-input',
                    id: 'ai-chat-input',
                    rows: '2',
                    maxlength: '2000',
                    placeholder: t('Placeholder'),
                    'aria-label': t('InputLabel'),
                    'aria-describedby': 'ai-chat-input-hint'
                }),
                el('div', { class: 'ai-chat-composer-actions' }, [
                    el('span', { id: 'ai-chat-input-hint' }, [t('InputHint')]),
                    el('button', { type: 'submit', class: 'ai-chat-send-btn', id: 'ai-chat-send', disabled: true, 'aria-label': t('Send') }, ['↑'])
                ])
            ]),
            el('p', { class: 'ai-chat-disclaimer' }, [t('Disclaimer')])
        ]);

        const panel = el('section', {
            id: 'ai-chat-panel',
            class: 'ai-chat-panel',
            hidden: true,
            role: 'dialog',
            'aria-labelledby': 'ai-chat-title'
        }, [
            header,
            el('div', {
                class: 'ai-chat-body',
                id: 'ai-chat-messages',
                role: 'log',
                'aria-label': t('Conversation'),
                'aria-live': 'polite',
                'aria-relevant': 'additions'
            }),
            el('p', { class: 'ai-chat-status', id: 'ai-chat-status', role: 'status' }),
            footer
        ]);

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

        const welcome = el('div', { class: 'ai-chat-welcome' }, [
            el('div', { class: 'ai-chat-welcome-icon', 'aria-hidden': 'true' }, ['✦']),
            el('p', { class: 'ai-chat-eyebrow' }, [t('Welcome.Eyebrow')]),
            el('h3', {}, [t('Welcome.Heading')]),
            el('p', { class: 'ai-chat-welcome-text' }, [getGreeting()]),
            el('p', { class: 'ai-chat-welcome-hint' }, [t('Welcome.Hint')])
        ]);

        const chips = el('div', { class: 'ai-chat-quick-actions' }, QUICK_ACTIONS.map((action, index) =>
            el('button', { type: 'button', class: 'ai-chat-chip', 'data-index': String(index) }, [
                el('span', { class: 'ai-chat-chip-icon', 'aria-hidden': 'true' }, [action.icon]),
                el('span', {}, [t(`QuickAction.${ action.key }.Label`)])
            ])));

        container.replaceChildren(welcome, chips);
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
        wrapper.className = `ai-chat-message ai-chat-message-${ role}`;

        const bubble = document.createElement('div');
        bubble.className = 'ai-chat-bubble';

        if (role === 'assistant') {
            bubble.appendChild(renderMarkdown(content));
        } else {
            bubble.textContent = content;
        }

        const label = document.createElement('span');
        label.className = 'ai-chat-message-label';
        label.textContent = role === 'user' ? t('Role.User') : t('Role.Assistant');
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

        const typing = el('div', {
            class: 'ai-chat-message ai-chat-message-assistant',
            id: 'ai-chat-typing',
            'aria-label': t('Thinking')
        }, [el('div', { class: 'ai-chat-bubble ai-chat-typing' }, [el('span'), el('span'), el('span')])]);
        container.appendChild(typing);
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
        document.getElementById('ai-chat-status').textContent = t('Status.Thinking');

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
            container.replaceChildren();
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
                    'RequestVerificationToken': window.PFUtils.getAntiForgeryToken()
                },
                body: JSON.stringify({
                    message: message,
                    conversationHistory: historyToSend
                })
            });

            if (response.status === 429) {
                const error = await response.json();
                const limitError = new Error(error.error || t('Status.UsageLimit'));
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
                : t('Status.Failed');
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
            form.addEventListener('submit', (e) => {
                e.preventDefault();
                if (input && input.value.trim()) {
                    sendMessage(input.value.trim());
                }
            });
        }

        if (input) {
            input.addEventListener('keydown', (e) => {
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
                    this.style.height = `${Math.min(this.scrollHeight, 150) }px`;
                }
            });
        }

        // Delegate chip clicks
        const messagesContainer = document.getElementById('ai-chat-messages');
        if (messagesContainer) {
            messagesContainer.addEventListener('click', (e) => {
                const chip = e.target.closest('.ai-chat-chip');
                if (!chip) {
                    return;
                }
                const index = parseInt(chip.getAttribute('data-index'), 10);
                if (!isNaN(index) && QUICK_ACTIONS[index]) {
                    sendMessage(t(`QuickAction.${ QUICK_ACTIONS[index].key }.Message`));
                }
            });
        }

        // Escape key closes panel
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && state.isOpen) {
                closeChat();
            }
        });

        // Close on outside click (use capture phase so it runs before inner handlers that may mutate the DOM)
        document.addEventListener('click', (e) => {
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
