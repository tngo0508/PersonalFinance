/* global ChartPalette */
/**
 * PROTOTYPE (#587) - mounts the variant named by ?variant= and drives the floating switcher (arrows and ← → keys).
 */
(function () {
    'use strict';

    const KEYS = ['A', 'B', 'C'];

    /**
     * Current variant key from the URL.
     * @returns {string} A, B or C.
     */
    function current () {
        const v = (new URLSearchParams(window.location.search).get('variant') || 'A').toUpperCase();
        return KEYS.includes(v) ? v : 'A';
    }

    /**
     * Render the current variant and update the switcher label.
     */
    function mount () {
        const key = current();
        const variant = window.ProtoVariants[key];
        const root = document.getElementById('protoRoot');
        root.className = `proto proto--${key.toLowerCase()}`;
        variant.render(root);
        document.getElementById('protoSwitcherLabel').textContent = `${key} · ${variant.name}`;
    }

    /**
     * Move to another variant.
     * @param {number} step - +1 or -1.
     */
    function go (step) {
        const i = (KEYS.indexOf(current()) + step + KEYS.length) % KEYS.length;
        const url = new URL(window.location.href);
        url.searchParams.set('variant', KEYS[i]);
        window.history.replaceState(null, '', url);
        mount();
        window.scrollTo(0, 0);
    }

    document.addEventListener('DOMContentLoaded', () => {
        mount();
        document.querySelectorAll('.proto-switcher__btn').forEach(b => b.addEventListener('click', () => go(Number(b.dataset.step))));
        document.addEventListener('keydown', (e) => {
            const t = e.target;
            if (t.closest && t.closest('input, textarea, select, [contenteditable]')) {
                return;
            }
            if (e.key === 'ArrowLeft') {
                go(-1);
            } else if (e.key === 'ArrowRight') {
                go(1);
            }
        });
        let timer = null;
        let lastWidth = window.innerWidth;
        window.addEventListener('resize', () => {
            if (window.innerWidth === lastWidth) {
                return;
            }
            lastWidth = window.innerWidth;
            clearTimeout(timer);
            timer = setTimeout(mount, 150);
        });
        ChartPalette.subscribe(() => mount());
    });
})();
