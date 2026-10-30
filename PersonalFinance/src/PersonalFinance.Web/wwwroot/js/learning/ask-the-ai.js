/**
 * Ask the AI on a Term Entry (#521): the button opens the AI Assistant widget about this entry, with its "About" chip
 * and two starter questions (PFAiChat.openWithTerm in core/ai-chat-widget.js). Only signed-in pages load it, as the
 * widget is only there for a signed-in Account.
 */
(function () {
    'use strict';

    /**
     * Wires each Ask the AI button to the widget.
     */
    function init () {
        document.querySelectorAll('[data-ask-ai]').forEach((button) => {
            button.addEventListener('click', () => {
                if (!window.PFAiChat) {
                    return;
                }
                window.PFAiChat.openWithTerm({
                    slug: button.getAttribute('data-term-slug'),
                    term: button.getAttribute('data-term'),
                    related: button.getAttribute('data-related') || undefined
                });
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
