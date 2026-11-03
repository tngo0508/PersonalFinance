/* global cytoscape, cytoscapeDagre, ChartPalette */
/**
 * Financial Universe: draws the month's graph with Cytoscape.js and the dagre layout (ADR 0020, spec #617 slices 2
 * and 3).
 *
 * The page provides the `#fuPageData` JSON island (every label, amount, link and detail line already composed by the
 * Web controller), a month picker, a toolbar of real buttons, a graph panel with the canvas container, the list panel
 * that is the accessible equivalent, and the node detail panel (a Bootstrap offcanvas). This module draws the island
 * into the container, keeps the drawing in the theme's colours, and picks the default view by legibility: after the
 * fit it measures the node labels against the body font size and opens on the list when they would be too small to
 * read. No pixel breakpoint decides the view; the choice is made again on every resize until the user picks a view
 * with the toggle.
 *
 * The wheel scrolls the page; ctrl+wheel zooms the graph at the pointer, pinch zooms on touch, and the toolbar's
 * buttons zoom in, zoom out and fit. Nodes drag (positions are not kept: a reload runs the layout again), pointing at
 * a node fades everything outside its neighbourhood, and tapping a node, or a list row's Details button, opens the
 * detail panel with the island's lines for that node; Escape and the close button dismiss it and focus returns to
 * what opened it. Changing the month submits the picker's form, so the URL carries the month. When the library is
 * missing or the island cannot be read, the list stays and a short message says so.
 */
(function () {
    'use strict';

    /**
     * Smallest readable node label, as a share of the body font size. Measured on this graph and the prototype's
     * (docs/research/financial-universe-prototype.md): the prototype's heavy month (24 nodes) reads at 72% in a
     * 1280 x 800 window, where the window's height sets the fit zoom, and is unreadable at 31% on a 375px phone, where
     * the width does; a fresh Account's sparse month fits a 375px phone at 61%, which is 9.7px text and still too small
     * to read. The switch sits between the laptop and phone readings, so a laptop keeps the graph and a phone opens
     * on the list whatever the month holds. Money Priorities' 80% does not transfer: this graph reads below it.
     */
    const MIN_LABEL_SCALE = 0.65;
    /** Zoom factor of the toolbar buttons. */
    const ZOOM_STEP = 1.2;
    /** Zoom factor of one ctrl+wheel tick. */
    const WHEEL_STEP = 1.1;
    const MIN_ZOOM = 0.1;
    const MAX_ZOOM = 3;
    /** A fit never enlarges a sparse month past this zoom, so a few nodes do not become a few huge nodes. */
    const FIT_MAX_ZOOM = 1.5;
    const FIT_PADDING = 16;
    /** The widest weighted edge, in pixels, on top of the 1px base. */
    const EXTRA_EDGE_WIDTH = 7;
    const MEASURING_CLASS = 'fu-panel--measuring';
    const ISLAND_ID = 'fuPageData';
    /** The Cytoscape class of an element outside the highlighted neighbourhood. */
    const DIM_CLASS = 'fu-dim';
    /** The stylesheet's token for how far a dimmed element fades (financial-universe.css). */
    const DIM_TOKEN = '--fu-dim-opacity';
    const DEFAULT_DIM_OPACITY = 0.2;

    /**
     * Reads the body's computed font size in CSS pixels.
     * @returns {number}
     */
    function bodyFontSize () {
        const size = parseFloat(window.getComputedStyle(document.body).fontSize);
        return Number.isFinite(size) && size > 0 ? size : 16;
    }

    /**
     * Reads the dim opacity token from the page's root element.
     * @param {Element|null} root - The `.financial-universe` element that carries the token.
     * @returns {number}
     */
    function readDimOpacity (root) {
        if (!root) {
            return DEFAULT_DIM_OPACITY;
        }
        const value = parseFloat(window.getComputedStyle(root).getPropertyValue(DIM_TOKEN));
        return Number.isFinite(value) && value >= 0 && value <= 1 ? value : DEFAULT_DIM_OPACITY;
    }

    /**
     * Reads the JSON island the view rendered.
     * @returns {{nodes: object[], edges: object[]}|null} The page data, or null when it is missing or unreadable.
     */
    function readPageData () {
        const island = document.getElementById(ISLAND_ID);
        if (!island) {
            return null;
        }
        try {
            const data = JSON.parse(island.textContent || '');
            return data && Array.isArray(data.nodes) && Array.isArray(data.edges) ? data : null;
        } catch (error) {
            return null;
        }
    }

    /**
     * The colours of the theme in effect, from the chart palette (core/chart-palette.js): the Money Flow slot and kind
     * tokens for the budget nodes, so both pictures colour a category the same, and the chart series for the rest.
     * @returns {{slots: string[], income: string, other: string, saved: string, shortfall: string, goal: string,
     *     netWorth: string, scenario: string, placeholder: string, text: string, textMuted: string, surface: string,
     *     edge: string}}
     */
    function readColours () {
        const palette = ChartPalette.read();
        const tokens = ChartPalette.readTokens([
            '--app-money-flow-slot-0', '--app-money-flow-slot-1', '--app-money-flow-slot-2', '--app-money-flow-slot-3',
            '--app-money-flow-slot-4', '--app-money-flow-income', '--app-money-flow-other', '--app-accent-negative', '--app-text',
            '--app-text-muted'
        ]);
        return {
            slots: tokens.slice(0, 5),
            income: tokens[5],
            other: tokens[6],
            saved: palette.positive,
            shortfall: tokens[7],
            goal: palette.series[4],
            netWorth: palette.series[2],
            scenario: palette.series[5],
            placeholder: palette.grid,
            text: tokens[8],
            textMuted: tokens[9],
            surface: palette.background,
            edge: palette.axis
        };
    }

    /**
     * The border colour of a node, by its kind; an expense category takes its colour slot, as in Money Flow.
     * @param {object} node - A Cytoscape node.
     * @param {object} colours - readColours()'s palette.
     * @returns {string}
     */
    function borderColour (node, colours) {
        switch (node.data('kind')) {
        case 'IncomeSource':
        case 'Income':
            return colours.income;
        case 'ExpenseCategory': {
            const slot = node.data('colorSlot');
            return Number.isInteger(slot) && colours.slots[slot] ? colours.slots[slot] : colours.other;
        }
        case 'Other':
            return colours.other;
        case 'Savings':
            return colours.saved;
        case 'Shortfall':
            return colours.shortfall;
        case 'SavingsGoal':
            return colours.goal;
        case 'NetWorth':
            return colours.netWorth;
        case 'InvestmentScenario':
            return colours.scenario;
        default:
            return colours.placeholder;
        }
    }

    /**
     * The Cytoscape stylesheet for a palette. Node labels use the body font size, so the label share after a fit is
     * the zoom level itself.
     * @param {object} colours - readColours()'s palette.
     * @param {number} maxWeight - The heaviest edge weight, for scaling widths.
     * @param {number} dimOpacity - The opacity of an element outside the highlighted neighbourhood.
     * @returns {object[]}
     */
    function buildStyle (colours, maxWeight, dimOpacity) {
        const fontSize = bodyFontSize();
        return [
            {
                selector: 'node',
                style: {
                    shape: 'round-rectangle',
                    width: 'label',
                    height: 'label',
                    padding: `${Math.round(fontSize * 0.6)}px`,
                    'background-color': colours.surface,
                    'border-width': 2,
                    'border-color': (node) => borderColour(node, colours),
                    label: 'data(label)',
                    'text-wrap': 'wrap',
                    'text-max-width': `${fontSize * 12}px`,
                    'text-valign': 'center',
                    'text-halign': 'center',
                    'font-size': fontSize,
                    'font-family': window.getComputedStyle(document.body).fontFamily,
                    'line-height': 1.3,
                    color: colours.text
                }
            },
            {
                selector: 'node[kind = "Income"], node[kind = "Savings"], node[kind = "Shortfall"], node[kind = "NetWorth"]',
                style: { 'font-weight': 'bold' }
            },
            {
                selector: 'node[placeholder = "yes"]',
                style: { 'border-style': 'dashed', color: colours.textMuted }
            },
            {
                selector: 'edge',
                style: {
                    'curve-style': 'bezier',
                    'target-arrow-shape': 'triangle',
                    'arrow-scale': 0.8,
                    'line-color': colours.edge,
                    'target-arrow-color': colours.edge,
                    width: (edge) => {
                        if (edge.data('weighted') !== 'yes') {
                            return 1.5;
                        }
                        const weight = edge.data('weight');
                        return maxWeight > 0 ? 1 + EXTRA_EDGE_WIDTH * (weight / maxWeight) : 1;
                    }
                }
            },
            {
                selector: 'edge[weighted = "no"]',
                style: { 'line-style': 'dashed', 'line-dash-pattern': [6, 4] }
            },
            {
                selector: `.${DIM_CLASS}`,
                style: { opacity: dimOpacity }
            }
        ];
    }

    /**
     * The island's nodes and edges as Cytoscape elements. A node's label is its name over its amount, both as the page
     * composed them; nothing else is composed here.
     * @param {{nodes: object[], edges: object[]}} data - The page data.
     * @returns {{nodes: object[], edges: object[], maxWeight: number}}
     */
    function toElements (data) {
        const nodes = data.nodes.map((node) => ({
            data: {
                id: node.id,
                kind: node.kind,
                colorSlot: node.colorSlot,
                href: node.href || '',
                placeholder: node.isPlaceholder ? 'yes' : 'no',
                label: node.amountText ? `${node.label}\n${node.amountText}` : node.label
            }
        }));
        let maxWeight = 0;
        const edges = data.edges.map((edge) => {
            const weighted = edge.weight !== null && edge.weight !== undefined;
            const weight = weighted ? Number(edge.weight) : 0;
            maxWeight = Math.max(maxWeight, weight);
            return {
                data: {
                    id: `${edge.source}->${edge.target}`,
                    source: edge.source,
                    target: edge.target,
                    weight: weight,
                    weighted: weighted ? 'yes' : 'no'
                }
            };
        });
        return { nodes: nodes, edges: edges, maxWeight: maxWeight };
    }

    /**
     * Whether the pointer is coarse (touch), where Cytoscape's own pinch zoom is wanted and hover means nothing.
     * @returns {boolean}
     */
    function hasCoarsePointer () {
        return typeof window.matchMedia === 'function' && window.matchMedia('(pointer: coarse)').matches;
    }

    /**
     * Fits the whole graph into the container.
     * @param {object} cy - The Cytoscape instance.
     */
    function fitGraph (cy) {
        cy.fit(undefined, FIT_PADDING);
        if (cy.zoom() > FIT_MAX_ZOOM) {
            cy.zoom(FIT_MAX_ZOOM);
            cy.center();
        }
    }

    /**
     * Zooms around the container's centre.
     * @param {object} cy - The Cytoscape instance.
     * @param {number} factor - The multiplier.
     */
    function zoomBy (cy, factor) {
        cy.zoom({
            level: cy.zoom() * factor,
            renderedPosition: { x: cy.width() / 2, y: cy.height() / 2 }
        });
    }

    /**
     * The rendered node label size as a share of the body font size: the zoom level, since labels use that size.
     * @param {object} cy - The Cytoscape instance.
     * @returns {number}
     */
    function labelShare (cy) {
        return cy.zoom();
    }

    /**
     * Makes the month picker submit on change; its button is then only for readers without script.
     */
    function setupMonthPicker () {
        const form = document.getElementById('fuMonthForm');
        const select = form ? form.querySelector('select[name="period"]') : null;
        if (!form || !select) {
            return;
        }
        const submit = form.querySelector('[data-fu-month-submit]');
        if (submit) {
            submit.hidden = true;
        }
        select.addEventListener('change', () => {
            form.submit();
        });
    }

    /**
     * Wires the node detail panel (a Bootstrap offcanvas). Every line comes from the island's node; a fact whose line
     * is empty is hidden, nothing is composed. Focus moves to the close button when the panel opens and back to the
     * element that opened it when the panel closes; Escape closes it from anywhere on the page.
     * @param {Object<string, object>} nodesById - The island's nodes by id.
     * @param {{opened: function(string): void, closed: function(): void}} hooks - Called with the node's id when the
     *     panel opens, and when it has closed.
     * @returns {{open: function(string, Element|null): void, hide: function(): void, current: function(): string|null}|null}
     *     The panel, or null when the page or Bootstrap has none.
     */
    function setupPanel (nodesById, hooks) {
        const element = document.getElementById('fuNodePanel');
        if (!element || typeof bootstrap === 'undefined' || !bootstrap.Offcanvas) {
            return null;
        }
        const fields = {};
        element.querySelectorAll('[data-fu-field]').forEach((field) => {
            fields[field.getAttribute('data-fu-field')] = field;
        });
        const rows = Array.from(element.querySelectorAll('[data-fu-row]'));
        const closeButton = element.querySelector('[data-bs-dismiss="offcanvas"]');
        const offcanvas = bootstrap.Offcanvas.getOrCreateInstance(element);
        const state = { current: null, opener: null };

        /**
         * Writes one line into its field.
         * @param {string} name - The field's data-fu-field value.
         * @param {string|undefined} text - The island's line.
         */
        function setText (name, text) {
            if (fields[name]) {
                fields[name].textContent = text || '';
            }
        }

        /**
         * Shows the panel for a node.
         * @param {string} nodeId - The node's id in the island.
         * @param {Element|null} opener - The element focus returns to when the panel closes.
         */
        function open (nodeId, opener) {
            const node = nodesById[nodeId];
            if (!node) {
                return;
            }
            state.current = nodeId;
            state.opener = opener || null;
            setText('kind', node.kindText);
            setText('label', node.label);
            setText('amount', node.amountText);
            setText('detail', node.detailText);
            setText('native', node.nativeAmountText);
            setText('period', node.periodText);
            setText('feature', node.featureText);
            if (fields.link) {
                fields.link.textContent = node.linkText || '';
                fields.link.setAttribute('href', node.href || '#');
                fields.link.hidden = !node.href || !node.linkText;
            }
            rows.forEach((row) => {
                const lines = Array.from(row.querySelectorAll('[data-fu-field]')).map((field) => field.textContent.trim());
                row.hidden = lines.every((line) => line === '');
            });
            element.setAttribute('data-fu-node', nodeId);
            offcanvas.show();
            hooks.opened(nodeId);
        }

        element.addEventListener('shown.bs.offcanvas', () => {
            if (closeButton) {
                closeButton.focus();
            }
        });
        element.addEventListener('hidden.bs.offcanvas', () => {
            state.current = null;
            element.removeAttribute('data-fu-node');
            hooks.closed();
            if (state.opener && typeof state.opener.focus === 'function') {
                state.opener.focus();
            }
            state.opener = null;
        });
        document.addEventListener('keydown', (event) => {
            if (event.key === 'Escape' && state.current) {
                offcanvas.hide();
            }
        });

        return {
            open: open,
            hide: () => offcanvas.hide(),
            current: () => state.current
        };
    }

    /**
     * Wires the page: the month picker, the detail panel from the list, then the drawing, its toolbar and the view.
     */
    function init () {
        setupMonthPicker();

        const root = document.querySelector('.financial-universe');
        const graphPanel = document.getElementById('fuGraphPanel');
        const listPanel = document.getElementById('fuListPanel');
        const container = document.getElementById('fuGraph');
        const toolbar = document.querySelector('[data-test-id="fu-toolbar"]');
        const failure = document.getElementById('fuGraphFailure');
        if (!graphPanel || !listPanel || !container || !toolbar) {
            return;
        }

        const viewButtons = Array.from(toolbar.querySelectorAll('[data-fu-view]'));
        const zoomTools = toolbar.querySelector('[data-fu-zoom-tools]');
        const data = readPageData();
        const state = {
            cy: null,
            /** The user's choice from the toggle; null until they press it. @type {'graph'|'list'|null} */
            choice: null,
            /** @type {'graph'|'list'|null} */
            mode: null,
            lastWidth: 0,
            pending: false,
            /** The node whose neighbourhood is highlighted for the panel; null when the panel is closed. */
            panelNode: null
        };

        /**
         * Fades everything outside a node's closed neighbourhood.
         * @param {object} node - A Cytoscape node.
         */
        function highlight (node) {
            if (!state.cy) {
                return;
            }
            state.cy.elements().addClass(DIM_CLASS);
            node.closedNeighborhood().removeClass(DIM_CLASS);
        }

        /**
         * Restores the drawing: the panel's node stays highlighted while the panel is open, else nothing is dimmed.
         */
        function restoreHighlight () {
            if (!state.cy) {
                return;
            }
            const panelNode = state.panelNode ? state.cy.getElementById(state.panelNode) : null;
            if (panelNode && panelNode.nonempty()) {
                highlight(panelNode);
            } else {
                state.cy.elements().removeClass(DIM_CLASS);
            }
        }

        // The detail panel works from the list even when the graph cannot draw, as long as the island reads.
        const nodesById = {};
        (data ? data.nodes : []).forEach((node) => {
            nodesById[node.id] = node;
        });
        const panel = data ? setupPanel(nodesById, {
            opened: (nodeId) => {
                state.panelNode = nodeId;
                restoreHighlight();
            },
            closed: () => {
                state.panelNode = null;
                restoreHighlight();
            }
        }) : null;
        if (panel) {
            listPanel.querySelectorAll('[data-fu-details]').forEach((button) => {
                button.hidden = false;
            });
            listPanel.addEventListener('click', (event) => {
                const button = event.target.closest('[data-fu-details]');
                if (button) {
                    panel.open(button.getAttribute('data-fu-details'), button);
                }
            });
        }

        if (typeof cytoscape !== 'function' || !data || !window.ChartPalette) {
            toolbar.hidden = true;
            if (failure) {
                failure.hidden = false;
            }
            return;
        }
        if (typeof cytoscapeDagre === 'function') {
            cytoscape.use(cytoscapeDagre);
        }

        const elements = toElements(data);
        const dimOpacity = readDimOpacity(root);
        const graphViewButton = viewButtons.find((button) => button.getAttribute('data-fu-view') === 'graph') || null;

        /**
         * Shows one panel and hides the other, and sets the toggle and the zoom tools to match.
         * @param {'graph'|'list'} mode
         */
        function showMode (mode) {
            const changed = mode !== state.mode;
            state.mode = mode;
            graphPanel.hidden = mode !== 'graph';
            listPanel.hidden = mode !== 'list';
            viewButtons.forEach((button) => {
                button.setAttribute('aria-pressed', String(button.getAttribute('data-fu-view') === mode));
            });
            if (zoomTools) {
                zoomTools.hidden = mode !== 'graph';
            }
            if (changed && mode === 'graph' && state.cy) {
                state.cy.resize();
                fitGraph(state.cy);
            }
        }

        /**
         * Fits the graph at the panel's current width, measuring with the panel unpainted when it is hidden, and
         * applies the user's choice or the legibility reading.
         */
        function reevaluate () {
            const wasHidden = graphPanel.hidden;
            if (wasHidden) {
                graphPanel.hidden = false;
                graphPanel.classList.add(MEASURING_CLASS);
            }
            state.cy.resize();
            fitGraph(state.cy);
            const fits = labelShare(state.cy) >= MIN_LABEL_SCALE;
            if (wasHidden) {
                graphPanel.classList.remove(MEASURING_CLASS);
                graphPanel.hidden = true;
            }
            showMode(state.choice || (fits ? 'graph' : 'list'));
        }

        // Draw with the panel measurable but unpainted, so a phone never flashes the graph before the list.
        graphPanel.hidden = false;
        graphPanel.classList.add(MEASURING_CLASS);
        state.cy = cytoscape({
            container: container,
            elements: { nodes: elements.nodes, edges: elements.edges },
            style: buildStyle(readColours(), elements.maxWeight, dimOpacity),
            layout: {
                name: 'dagre',
                rankDir: 'LR',
                nodeSep: 14,
                rankSep: 70,
                edgeSep: 10,
                ranker: 'network-simplex',
                fit: true,
                padding: FIT_PADDING
            },
            minZoom: MIN_ZOOM,
            maxZoom: MAX_ZOOM,
            // Plain wheel scrolls the page; the ctrl+wheel listener below zooms. On touch, Cytoscape's pinch zooms.
            userZoomingEnabled: hasCoarsePointer(),
            boxSelectionEnabled: false,
            // Nodes drag; a reload runs the layout again, nothing is kept.
            autoungrabify: false,
            autounselectify: true
        });
        const fits = labelShare(state.cy) >= MIN_LABEL_SCALE;
        graphPanel.classList.remove(MEASURING_CLASS);
        state.lastWidth = graphPanel.parentElement ? graphPanel.parentElement.clientWidth : 0;
        showMode(fits ? 'graph' : 'list');

        // ctrl+wheel zooms at the pointer; a plain wheel keeps scrolling the page over the canvas.
        container.addEventListener('wheel', (event) => {
            if (!event.ctrlKey) {
                return;
            }
            event.preventDefault();
            const rect = container.getBoundingClientRect();
            state.cy.zoom({
                level: state.cy.zoom() * (event.deltaY < 0 ? WHEEL_STEP : 1 / WHEEL_STEP),
                renderedPosition: { x: event.clientX - rect.left, y: event.clientY - rect.top }
            });
        }, { passive: false });

        // Tapping a node opens its details; without a panel, the feature that owns it.
        state.cy.on('tap', 'node', (event) => {
            if (panel) {
                panel.open(event.target.id(), graphViewButton);
                return;
            }
            const href = event.target.data('href');
            if (href) {
                window.location.assign(href);
            }
        });

        // Pointing at a node shows what it connects to; touch has no hover, so it skips this.
        if (!hasCoarsePointer()) {
            state.cy.on('mouseover', 'node', (event) => {
                highlight(event.target);
            });
            state.cy.on('mouseout', 'node', () => {
                restoreHighlight();
            });
        }

        toolbar.addEventListener('click', (event) => {
            const button = event.target.closest('button');
            if (!button) {
                return;
            }
            const view = button.getAttribute('data-fu-view');
            if (view === 'graph' || view === 'list') {
                state.choice = view;
                showMode(view);
                return;
            }
            switch (button.getAttribute('data-fu-action')) {
            case 'zoom-in':
                zoomBy(state.cy, ZOOM_STEP);
                break;
            case 'zoom-out':
                zoomBy(state.cy, 1 / ZOOM_STEP);
                break;
            case 'fit':
                fitGraph(state.cy);
                break;
            default:
                break;
            }
        });

        // Redraw in the other palette when the theme changes (core/theme.js dispatches "themechange").
        ChartPalette.subscribe(() => {
            state.cy.style().fromJson(buildStyle(readColours(), elements.maxWeight, dimOpacity)).update();
        });

        // Fit again, and re-read legibility, when the page's width changes (a resize, the sidebar collapsing).
        if (typeof ResizeObserver !== 'undefined' && graphPanel.parentElement) {
            const observer = new ResizeObserver((entries) => {
                const width = entries[0] && entries[0].contentRect ? Math.round(entries[0].contentRect.width) : 0;
                if (width === state.lastWidth || state.pending) {
                    return;
                }
                state.lastWidth = width;
                state.pending = true;
                // Decide on the next frame, so a panel swap never runs inside the observer callback.
                window.requestAnimationFrame(() => {
                    state.pending = false;
                    reevaluate();
                });
            });
            observer.observe(graphPanel.parentElement);
        }

        /** Read-only handles for the E2E suite. */
        window.FinancialUniverseGraph = {
            instance: () => state.cy,
            mode: () => state.mode,
            labelShare: () => labelShare(state.cy),
            minLabelScale: MIN_LABEL_SCALE,
            panelNode: () => (panel ? panel.current() : null),
            dimClass: DIM_CLASS
        };
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
