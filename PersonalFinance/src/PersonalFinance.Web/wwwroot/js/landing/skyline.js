/**
 * Landing Page Skyline: a 3D view of Headquarters (Stanton, California) behind the hero (ADR 0013).
 *
 * The container `[data-skyline]` shows a poster image (set in landing.css, at 992px and wider only). This module
 * upgrades it to a live MapLibre scene: the buildings rise outward from the city centre like a growth chart, then the
 * camera slowly orbits. It never slows the page:
 *
 * - Below 992px wide it does nothing, so no script, data or poster is downloaded.
 * - MapLibre and the data load only after the page's `load` event, once the browser is idle.
 * - Under reduced motion, reduced data, a 2G/3G connection or without WebGL the poster stays as the final view.
 * - Rendering pauses while the hero is off screen or the tab is hidden.
 *
 * The container's `data-skyline-state` reports what happened: `poster`, `loading`, `live` or `skipped`, with the
 * reason in `data-skyline-reason`. `data-skyline-capture` renders the finished scene at once, with no orbit, for
 * `scripts/capture-skyline-poster.mjs`.
 */
(function () {
    'use strict';

    const WIDE_QUERY = '(min-width: 992px)';
    const SLOW_CONNECTIONS = ['slow-2g', '2g', '3g'];

    // Stanton City Hall, the point the Skyline rises from.
    const CENTRE = [-117.9932, 33.8025];
    const VIEW = { zoom: 15.2, pitch: 60, bearing: -28 };
    const COPY_SHARE = 0.42; // the left share of the hero the copy covers; the city centres in the rest

    const HEIGHT_EXAGGERATION = 2.2;
    const RISE_MS = 2800;
    const RIPPLE = 0.55; // share of the rise spent travelling outward from the centre
    const ORBIT_DEGREES_PER_SECOND = 0.9;

    const colours = {
        ground: '#F3F6F5',
        park: '#DCEBE3',
        water: '#A9CAD6',
        road: '#FFFFFF',
        boundary: '#8A6A1E',
        low: '#D8E9E0',
        mid: '#8DBEA6',
        high: '#3F8A6C',
        tallest: '#1D6A50'
    };

    /**
     * Why the live scene should not run here, if any reason applies.
     * @returns {string|null} The reason to keep the poster, or null to go live.
     */
    function reasonToKeepPoster () {
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            return 'reduced-motion';
        }
        const connection = navigator.connection;
        if (connection && connection.saveData) {
            return 'save-data';
        }
        if (connection && SLOW_CONNECTIONS.includes(connection.effectiveType)) {
            return 'slow-connection';
        }
        if (!supportsWebGl()) {
            return 'no-webgl';
        }
        return null;
    }

    /**
     * Whether the browser can create a WebGL context.
     * @returns {boolean} True when WebGL is available.
     */
    function supportsWebGl () {
        try {
            const canvas = document.createElement('canvas');
            return Boolean(canvas.getContext('webgl2') || canvas.getContext('webgl'));
        } catch {
            return false;
        }
    }

    /**
     * Run a callback after the `load` event, once the browser is idle.
     * @param {Function} callback - The work to defer.
     */
    function afterLoadWhenIdle (callback) {
        const schedule = () => {
            if ('requestIdleCallback' in window) {
                window.requestIdleCallback(callback, { timeout: 2000 });
            } else {
                window.setTimeout(callback, 200);
            }
        };
        if (document.readyState === 'complete') {
            schedule();
        } else {
            window.addEventListener('load', schedule, { once: true });
        }
    }

    /**
     * Add a stylesheet and a script to the page, resolving once the script has run.
     * @param {string} cssUrl - The MapLibre stylesheet.
     * @param {string} scriptUrl - The MapLibre script.
     * @returns {Promise<void>} Resolves when the script has loaded.
     */
    function loadMapLibre (cssUrl, scriptUrl) {
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = cssUrl;
        document.head.appendChild(link);

        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = scriptUrl;
            script.async = true;
            script.onload = () => resolve();
            script.onerror = () => reject(new Error('MapLibre failed to load'));
            document.head.appendChild(script);
        });
    }

    /**
     * Fetch the Skyline GeoJSON.
     * @param {string} url - The data file.
     * @returns {Promise<object>} The parsed GeoJSON.
     */
    async function fetchData (url) {
        const response = await fetch(url);
        if (!response.ok) {
            throw new Error(`Skyline data returned ${response.status}`);
        }
        return response.json();
    }

    /**
     * Give each building `d`, its distance from the centre scaled to 0..1, so the rise can travel outward.
     * @param {object} data - The Skyline GeoJSON.
     */
    function addDistances (data) {
        const cosLat = Math.cos(CENTRE[1] * Math.PI / 180);
        const buildings = data.features.filter((f) => f.properties.k === 'b');
        let furthest = 0;

        buildings.forEach((building) => {
            const [lon, lat] = building.geometry.coordinates[0][0];
            const dx = (lon - CENTRE[0]) * cosLat;
            const dy = lat - CENTRE[1];
            building.properties.d = Math.sqrt(dx * dx + dy * dy);
            furthest = Math.max(furthest, building.properties.d);
        });
        buildings.forEach((building) => {
            building.properties.d = furthest > 0 ? building.properties.d / furthest : 0;
        });
    }

    /**
     * The extrusion height expression at a point in the rise.
     * @param {number} progress - 0 (flat) to 1 (fully risen).
     * @returns {Array} A MapLibre expression.
     */
    function heightAt (progress) {
        // Each building starts once the rise has travelled out to it, then grows over the remaining share.
        const grown = ['/', ['-', progress, ['*', ['get', 'd'], RIPPLE]], 1 - RIPPLE];
        return ['*', ['get', 'h'], HEIGHT_EXAGGERATION, ['max', 0, ['min', 1, grown]]];
    }

    /**
     * The MapLibre style: a pale paper ground, white roads and buildings shaded green by height.
     * @param {object} data - The Skyline GeoJSON.
     * @param {number} progress - The starting point of the rise.
     * @returns {object} A MapLibre style.
     */
    function buildStyle (data, progress) {
        const kind = (k) => ['==', ['get', 'k'], k];
        return {
            version: 8,
            sources: { stanton: { type: 'geojson', data } },
            light: { anchor: 'map', position: [1.3, 210, 35], color: '#FFFFFF', intensity: 0.32 },
            sky: {
                'sky-color': colours.ground, 'horizon-color': '#FFFFFF', 'fog-color': colours.ground,
                'sky-horizon-blend': 0.6, 'horizon-fog-blend': 0.7, 'fog-ground-blend': 0.55
            },
            layers: [
                { id: 'ground', type: 'background', paint: { 'background-color': colours.ground } },
                { id: 'parks', type: 'fill', source: 'stanton', filter: kind('p'), paint: { 'fill-color': colours.park } },
                {
                    id: 'water', type: 'line', source: 'stanton', filter: kind('w'),
                    paint: { 'line-color': colours.water, 'line-width': 2.5 }
                },
                {
                    id: 'roads', type: 'line', source: 'stanton', filter: kind('r'),
                    layout: { 'line-cap': 'round', 'line-join': 'round' },
                    paint: {
                        'line-color': colours.road,
                        'line-width': ['interpolate', ['linear'], ['zoom'], 13, 1.5, 16, 6]
                    }
                },
                {
                    id: 'boundary', type: 'line', source: 'stanton', filter: kind('c'),
                    paint: {
                        'line-color': colours.boundary,
                        'line-width': 1.5,
                        'line-opacity': 0.55,
                        'line-dasharray': [3, 2]
                    }
                },
                {
                    id: 'buildings', type: 'fill-extrusion', source: 'stanton', filter: kind('b'),
                    paint: {
                        'fill-extrusion-color': [
                            'interpolate', ['linear'], ['get', 'h'],
                            3, colours.low, 7, colours.mid, 12, colours.high, 20, colours.tallest
                        ],
                        'fill-extrusion-height': heightAt(progress),
                        'fill-extrusion-base': 0,
                        'fill-extrusion-opacity': 0.94,
                        'fill-extrusion-vertical-gradient': true
                    }
                }
            ]
        };
    }

    /**
     * Ease out: fast at first, settling gently.
     * @param {number} t - 0..1.
     * @returns {number} The eased value.
     */
    function easeOutCubic (t) {
        return 1 - Math.pow(1 - t, 3);
    }

    /**
     * Draw the live Skyline in the container and start the rise and orbit.
     * @param {HTMLElement} container - The `[data-skyline]` element.
     * @param {object} data - The Skyline GeoJSON.
     * @param {boolean} capture - Render the finished scene at once, with no orbit.
     */
    function startScene (container, data, capture) {
        const mount = container.querySelector('[data-skyline-map]');
        addDistances(data);

        const map = new window.maplibregl.Map({
            container: mount,
            style: buildStyle(data, capture ? 1 : 0),
            center: CENTRE,
            zoom: VIEW.zoom,
            pitch: VIEW.pitch,
            bearing: VIEW.bearing,
            maxPitch: 70,
            padding: { left: Math.round(mount.clientWidth * COPY_SHARE) },
            interactive: false,
            attributionControl: false,
            fadeDuration: 0,
            canvasContextAttributes: { antialias: true, preserveDrawingBuffer: capture }
        });

        let visible = true;
        let frame = 0;
        let lastTime = 0;
        let riseStart = 0;
        let rising = !capture;

        const running = () => visible && !document.hidden;

        const tick = (time) => {
            frame = 0;
            if (!running()) {
                lastTime = 0;
                return;
            }
            if (rising) {
                riseStart = riseStart || time;
                const t = Math.min(1, (time - riseStart) / RISE_MS);
                map.setPaintProperty('buildings', 'fill-extrusion-height', heightAt(easeOutCubic(t)));
                rising = t < 1;
            }
            if (lastTime) {
                map.setBearing(map.getBearing() + ORBIT_DEGREES_PER_SECOND * (time - lastTime) / 1000);
            }
            lastTime = time;
            frame = window.requestAnimationFrame(tick);
        };

        const resume = () => {
            if (!frame && running()) {
                frame = window.requestAnimationFrame(tick);
            }
        };

        map.once('load', () => {
            container.classList.add('is-live');
            container.dataset.skylineState = 'live';
            if (capture) {
                map.once('idle', () => {
                    container.dataset.skylineReady = 'true';
                });
                return;
            }

            if ('IntersectionObserver' in window) {
                new IntersectionObserver((entries) => {
                    visible = entries[0].isIntersecting;
                    resume();
                }).observe(container);
            }
            document.addEventListener('visibilitychange', resume);
            resume();
        });
    }

    /**
     * Upgrade the poster to the live Skyline when the device and settings allow it.
     */
    function init () {
        const container = document.querySelector('[data-skyline]');
        if (!container || !window.matchMedia(WIDE_QUERY).matches) {
            return;
        }

        const capture = container.hasAttribute('data-skyline-capture');
        container.dataset.skylineState = 'poster';

        const reason = capture ? null : reasonToKeepPoster();
        if (reason) {
            container.dataset.skylineState = 'skipped';
            container.dataset.skylineReason = reason;
            return;
        }

        afterLoadWhenIdle(async () => {
            container.dataset.skylineState = 'loading';
            try {
                const [, data] = await Promise.all([
                    loadMapLibre(container.dataset.skylineCss, container.dataset.skylineScript),
                    fetchData(container.dataset.skylineData)
                ]);
                startScene(container, data, capture);
            } catch {
                // The poster stays: the Skyline is decoration and must never break the page.
                container.dataset.skylineState = 'skipped';
                container.dataset.skylineReason = 'load-failed';
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
