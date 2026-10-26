/* global mermaid, PFI18n */
/**
 * Money Priorities: draws the Priority Tree with Mermaid (ADR 0014).
 *
 * The page provides an empty frame and calls `window.MoneyPrioritiesDiagram.show(frame, texts)` with the two Mermaid
 * texts the ApiService generated. This module renders each text offscreen with a fresh id, then swaps the SVG in once.
 * It shows the whole tree when its labels stay readable (at least 80% of the body font size at the frame's width), and
 * otherwise the path view, with a "Show the whole tree" button that opens the whole tree at natural size in a
 * horizontally scrolling frame. No fixed pixel width decides the view: the choice is made again after every render
 * and on every resize of the frame.
 *
 * The diagram texts never carry user-entered values, and Mermaid runs with `securityLevel: 'strict'`. A render failure
 * shows a short message and logs nothing; the page's "Your path" list stays the text equivalent.
 */
(function () {
    'use strict';

    /** Smallest readable label size, as a share of the body font size (spec: 80%). */
    const MIN_LABEL_SCALE = 0.8;

    let initialised = false;
    let idCount = 0;
    /** @type {WeakMap<HTMLElement, object>} */
    const states = new WeakMap();

    /**
     * Returns a fresh id for a render or an element.
     * @param {string} prefix
     * @returns {string}
     */
    function freshId (prefix) {
        idCount += 1;
        return `${prefix}-${idCount}`;
    }

    /**
     * Reads the body's computed font size in CSS pixels.
     * @returns {number}
     */
    function bodyFontSize () {
        const size = parseFloat(window.getComputedStyle(document.body).fontSize);
        return Number.isFinite(size) && size > 0 ? size : 16;
    }

    /**
     * Initialises Mermaid once, with the one configuration ADR 0014 allows.
     */
    function ensureInitialised () {
        if (initialised) {
            return;
        }
        mermaid.initialize({
            startOnLoad: false,
            securityLevel: 'strict',
            theme: 'base',
            look: 'classic',
            layout: 'dagre',
            logLevel: 'fatal',
            flowchart: { useMaxWidth: true, htmlLabels: true, curve: 'basis' },
            themeVariables: {
                fontFamily: window.getComputedStyle(document.body).fontFamily,
                fontSize: `${bodyFontSize()}px`,
                // Mermaid needs a colour value here, not a CSS variable: the light palette's white (ADR 0014).
                edgeLabelBackground: '#FFFFFF'
            }
        });
        initialised = true;
    }

    /**
     * Removes anything Mermaid left in the document for a render id. Mermaid cleans up after a successful render;
     * this also covers a failed one.
     * @param {string} id
     */
    function removeRenderLeftovers (id) {
        [id, `d${id}`, `i${id}`].forEach((leftoverId) => {
            const leftover = document.getElementById(leftoverId);
            if (leftover) {
                leftover.remove();
            }
        });
    }

    /**
     * Reads the width from the root element's viewBox in SVG markup.
     * @param {string} svg
     * @returns {number} The width, or 0 when it cannot be read.
     */
    function viewBoxWidth (svg) {
        const match = /<svg\b[^>]*\bviewBox="\s*[-\d.e]+[\s,]+[-\d.e]+[\s,]+([\d.e]+)/i.exec(svg);
        const width = match ? parseFloat(match[1]) : 0;
        return Number.isFinite(width) ? width : 0;
    }

    /**
     * Renders one Mermaid text offscreen with a fresh id and no container.
     * @param {string} text
     * @returns {Promise<{svg: string, width: number}>} The SVG markup and its viewBox width.
     */
    async function renderOffscreen (text) {
        const id = freshId('mp-diagram-svg');
        try {
            const result = await mermaid.render(id, text);
            return { svg: result.svg, width: viewBoxWidth(result.svg) };
        } finally {
            removeRenderLeftovers(id);
        }
    }

    /**
     * The width the frame offers its content, in CSS pixels.
     * @param {HTMLElement} frame
     * @returns {number}
     */
    function contentWidth (frame) {
        const style = window.getComputedStyle(frame);
        return frame.clientWidth - (parseFloat(style.paddingLeft) || 0) - (parseFloat(style.paddingRight) || 0);
    }

    /**
     * Whether the whole tree's labels stay readable at the frame's width: the effective label size (body font size x
     * frame width / viewBox width) is at least 80% of the body font size.
     * @param {HTMLElement} frame
     * @param {object} state
     * @returns {boolean}
     */
    function wholeTreeFits (frame, state) {
        const fontSize = bodyFontSize();
        const width = contentWidth(frame);
        if (!state.whole.width || width <= 0) {
            return true;
        }
        const effectiveLabelSize = fontSize * width / state.whole.width;
        return effectiveLabelSize >= fontSize * MIN_LABEL_SCALE;
    }

    /**
     * Picks the view for the frame's current width, sets the toggle, and swaps the SVG in when the view changed.
     * @param {HTMLElement} frame
     * @param {object} state
     */
    function applyMode (frame, state) {
        if (!state.whole) {
            return;
        }
        const fits = wholeTreeFits(frame, state);
        if (fits || !state.path) {
            state.expanded = false;
        }
        let mode = 'path';
        if (fits) {
            mode = 'fit';
        } else if (!state.path || state.expanded) {
            mode = 'scroll';
        }

        const opened = mode === 'scroll' && Boolean(state.path);
        state.toggle.hidden = fits || !state.path;
        state.toggle.setAttribute('aria-expanded', String(opened));
        state.toggle.textContent = PFI18n.t(opened ? 'MoneyPriorities.Js.Diagram.Hide' : 'MoneyPriorities.Js.Diagram.Show');

        if (mode === state.mode) {
            return;
        }
        state.mode = mode;

        const target = mode === 'scroll' ? state.scroll : state.view;
        const other = mode === 'scroll' ? state.view : state.scroll;
        other.replaceChildren();
        other.hidden = true;
        // eslint-disable-next-line no-restricted-syntax -- the one swap of Mermaid's SVG (securityLevel 'strict'); diagram texts never hold user input
        target.innerHTML = mode === 'path' ? state.path.svg : state.whole.svg;
        target.hidden = false;
        target.classList.toggle('mp-diagram__view--path', mode === 'path');

        const svg = target.querySelector('svg');
        if (svg && mode === 'scroll') {
            // Natural size: the frame scrolls sideways instead of shrinking the labels.
            svg.style.maxWidth = 'none';
            svg.style.width = `${state.whole.width}px`;
            scrollToStart(target);
        }
    }

    /**
     * Scrolls the whole-tree frame so the tree's start node is in view: the tree begins at its top, wherever that
     * falls across the width.
     * @param {HTMLElement} scroll
     */
    function scrollToStart (scroll) {
        const start = scroll.querySelector('g.node[id*="-flowchart-S-"]');
        if (!start) {
            return;
        }
        const nodeBox = start.getBoundingClientRect();
        const frameBox = scroll.getBoundingClientRect();
        const centre = nodeBox.left - frameBox.left + scroll.scrollLeft + nodeBox.width / 2;
        scroll.scrollLeft = Math.max(0, centre - scroll.clientWidth / 2);
    }

    /**
     * Builds the frame's children once: the view, the toggle, the scrolling whole-tree frame and the failure message.
     * @param {HTMLElement} frame
     * @returns {object} The frame's state.
     */
    function createState (frame) {
        const scrollId = freshId('mp-diagram-scroll');

        const view = document.createElement('div');
        view.className = 'mp-diagram__view';

        const toggle = document.createElement('button');
        toggle.type = 'button';
        toggle.className = 'btn btn-sm btn-outline-secondary mp-diagram__toggle';
        toggle.textContent = PFI18n.t('MoneyPriorities.Js.Diagram.Show');
        toggle.setAttribute('aria-expanded', 'false');
        toggle.setAttribute('aria-controls', scrollId);
        toggle.hidden = true;

        const scroll = document.createElement('div');
        scroll.id = scrollId;
        scroll.className = 'mp-diagram__scroll';
        scroll.setAttribute('role', 'region');
        scroll.setAttribute('aria-label', PFI18n.t('MoneyPriorities.Js.Diagram.ScrollAria'));
        scroll.tabIndex = 0;
        scroll.hidden = true;

        const message = document.createElement('p');
        message.className = 'mp-diagram__message';
        message.hidden = true;

        frame.replaceChildren(view, toggle, scroll, message);

        const state = {
            view: view,
            toggle: toggle,
            scroll: scroll,
            message: message,
            whole: null,
            path: null,
            expanded: false,
            mode: null,
            latest: 0,
            pending: false
        };

        toggle.addEventListener('click', () => {
            state.expanded = !state.expanded;
            applyMode(frame, state);
        });

        if (typeof ResizeObserver !== 'undefined') {
            const observer = new ResizeObserver(() => {
                // Decide on the next frame, so a swap never runs inside the observer callback.
                if (state.pending) {
                    return;
                }
                state.pending = true;
                window.requestAnimationFrame(() => {
                    state.pending = false;
                    applyMode(frame, state);
                });
            });
            observer.observe(frame);
        }

        states.set(frame, state);
        return state;
    }

    /**
     * Shows the short failure message in place of the diagram.
     * @param {object} state
     */
    function showFailure (state) {
        state.whole = null;
        state.path = null;
        state.mode = null;
        state.expanded = false;
        state.view.replaceChildren();
        state.view.hidden = true;
        state.scroll.replaceChildren();
        state.scroll.hidden = true;
        state.toggle.hidden = true;
        state.message.textContent = PFI18n.t('MoneyPriorities.Js.Diagram.Failure');
        state.message.hidden = false;
    }

    /**
     * Renders the Priority Tree into `frame`. `texts.wholeTree` is always given; `texts.pathView` is null before the
     * first submit, and then the whole tree is always shown. Resolves when the SVG is in the page.
     * @param {HTMLElement} frame The empty element the page provides.
     * @param {{ wholeTree: string, pathView: (string|null) }} texts The Mermaid texts from the ApiService.
     * @returns {Promise<void>}
     */
    async function show (frame, texts) {
        if (!frame) {
            return;
        }
        const state = states.get(frame) || createState(frame);
        state.latest += 1;
        const call = state.latest;

        let whole = null;
        let path = null;
        let failed = false;
        try {
            if (typeof mermaid === 'undefined' || !texts || !texts.wholeTree) {
                throw new Error('Nothing to draw');
            }
            ensureInitialised();
            whole = await renderOffscreen(texts.wholeTree);
            if (texts.pathView) {
                path = await renderOffscreen(texts.pathView);
            }
        } catch {
            failed = true;
        }

        // A later call has started: its result replaces this one, so skip the swap.
        if (call !== state.latest) {
            return;
        }
        if (failed) {
            showFailure(state);
            return;
        }
        state.message.hidden = true;
        state.whole = whole;
        state.path = path;
        state.expanded = false;
        state.mode = null;
        applyMode(frame, state);
    }

    window.MoneyPrioritiesDiagram = {
        show: show
    };
})();
