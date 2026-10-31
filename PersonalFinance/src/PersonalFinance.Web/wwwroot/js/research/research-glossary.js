/**
 * Research glossary: a click on a term (`button.research-term`, rendered by `<research-term key="...">` with its
 * explanation in `data-term-text`) opens a small popover under it with that text. The popover is a `role="tooltip"`
 * the term points at through `aria-describedby` while it is open. A second click on the term, Escape or a click
 * anywhere else closes it. Exposed as `window.ResearchGlossary = { open, close }`.
 */
(function () {
    'use strict';

    const POPOVER_ID = 'research-glossary-popover';
    const TERM_SELECTOR = '.research-term';
    const GAP_PX = 8;
    const EDGE_PX = 8;

    let popover = null;
    let openTerm = null;

    /**
     * The popover, created on first use.
     * @returns {HTMLElement} The popover element.
     */
    function ensurePopover () {
        if (!popover) {
            popover = document.createElement('div');
            popover.id = POPOVER_ID;
            popover.className = 'research-glossary-popover';
            popover.setAttribute('role', 'tooltip');
            popover.hidden = true;
            document.body.append(popover);
        }
        return popover;
    }

    /**
     * Close the popover, if open.
     * @returns {void}
     */
    function close () {
        if (!popover || popover.hidden) {
            return;
        }
        popover.hidden = true;
        if (openTerm) {
            openTerm.removeAttribute('aria-describedby');
        }
        openTerm = null;
    }

    /**
     * Open the popover under a term with its explanation.
     * @param {HTMLElement} term - The term's button.
     * @returns {void}
     */
    function open (term) {
        const text = term ? term.dataset.termText : '';
        if (!text) {
            return;
        }
        close();
        const pop = ensurePopover();
        pop.textContent = text;
        pop.hidden = false;
        const rect = term.getBoundingClientRect();
        const width = pop.offsetWidth;
        const left = Math.max(EDGE_PX, Math.min(rect.left, window.innerWidth - width - EDGE_PX));
        pop.style.left = `${left}px`;
        pop.style.top = `${rect.bottom + GAP_PX}px`;
        term.setAttribute('aria-describedby', POPOVER_ID);
        openTerm = term;
    }

    /**
     * Wire the clicks and keys that open and close the popover.
     * @returns {void}
     */
    function init () {
        document.addEventListener('click', (event) => {
            const target = event.target instanceof Element ? event.target : null;
            const term = target ? target.closest(TERM_SELECTOR) : null;
            if (term) {
                if (term === openTerm) {
                    close();
                } else {
                    open(term);
                }
                return;
            }
            if (!target || !target.closest(`#${POPOVER_ID}`)) {
                close();
            }
        });
        document.addEventListener('keydown', (event) => {
            if (event.key === 'Escape' && openTerm) {
                close();
            }
        });
        // The popover is placed for the page as it was; a scroll or resize moves the term away from it.
        window.addEventListener('resize', close);
        window.addEventListener('scroll', close, { passive: true });
    }

    window.ResearchGlossary = { open, close };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
