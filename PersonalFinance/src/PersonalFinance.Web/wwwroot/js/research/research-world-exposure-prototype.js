/* global d3, topojson */
/**
 * PROTOTYPE, throwaway (wayfinder ticket #237, "World tab: how to show basket exposure by country and currency").
 * Never merge. Mounted on the Research page's World tab, switched by ?variant=A|B|C and the floating bar. Only loaded
 * in Development when ?variant= is present. Data: /prototype/world-exposure/exposure.json (latest N-PORT filing per
 * fund, every holding except securities-lending collateral) and the vendored countries-110m borders.
 *
 *   A  Map first: basket choropleth with dot markers for territories the 110m map has no shape for, zoom and pan,
 *      linked region bars beside it, currency callouts.
 *   B  Bars first: ranked country bars grouped by region (expand a region), a small inset map, currency as a 100% bar.
 *   C  Toggle + meters: one big map with a Country | Currency switch, "US vs rest" and "USD vs rest" meters, and a
 *      "Not on the map" list for small territories.
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    if (!page || typeof d3 === 'undefined' || typeof topojson === 'undefined') {
        return;
    }
    const VARIANTS = [
        { key: 'A', name: 'Map first + region bars' },
        { key: 'B', name: 'Bars first + inset map' },
        { key: 'C', name: 'Country/Currency toggle + meters' }
    ];
    const SLOT = { VT: '#2a78d6', VXUS: '#eb6834', VEA: '#1baf7a', VWO: '#eda100', VTI: '#e87ba4', AIQ: '#008300', BNDX: '#6250d6', IWM: '#e34948' };
    const BLUES = ['#cde2fb', '#9ec5f4', '#6da7ec', '#3987e5', '#256abf', '#184f95', '#0d366b'];
    // Where to draw a marker for a territory the 110m map has no shape for: [longitude, latitude].
    const MARKERS = { HK: [114.17, 22.32], SG: [103.82, 1.35], BM: [-64.75, 32.3], KY: [-81.25, 19.3], JE: [-2.13, 49.21], GG: [-2.58, 49.45], IM: [-4.55, 54.23], VG: [-64.62, 18.42], MH: [171.18, 7.13], GI: [-5.35, 36.14], LI: [9.55, 47.16] };
    const MAX_FUNDS = 5;
    const params = new URLSearchParams(window.location.search);
    const variant = (VARIANTS.find((v) => v.key === (params.get('variant') || 'A').toUpperCase()) || VARIANTS[0]).key;
    const fmt = (v) => (v >= 10 ? `${v.toFixed(0)}%` : (v < 0.05 ? '<0.1%' : `${v.toFixed(1)}%`));
    const el = (tag, cls, text) => {
        const e = document.createElement(tag);
        if (cls) {
            e.className = cls;
        }
        if (text !== undefined) {
            e.textContent = text;
        }
        return e;
    };
    const regionName = new Intl.DisplayNames(['en'], { type: 'region' });
    const currencyName = new Intl.DisplayNames(['en'], { type: 'currency' });
    const countryLabel = (c) => (c === 'N/A' ? 'Unallocated' : (regionName.of(c) || c));
    const currencyLabel = (c) => (c === 'N/A' ? 'Unallocated' : `${c} · ${currencyName.of(c) || c}`);

    // ---------- model ----------

    function exposure (data, basket) {
        const total = d3.sum(basket, (b) => b.w) || 1;
        const country = new Map();
        const currency = new Map();
        basket.forEach(({ t, w }) => {
            const f = data.funds[t];
            const share = w / total;
            Object.entries(f.country).forEach(([c, p]) => country.set(c, (country.get(c) || 0) + share * p));
            Object.entries(f.currency).forEach(([c, p]) => currency.set(c, (currency.get(c) || 0) + share * p));
            country.set('N/A', (country.get('N/A') || 0) + share * f.unallocated);
            currency.set('N/A', (currency.get('N/A') || 0) + share * f.unallocated);
        });
        const rows = (m) => Array.from(m, ([code, pct]) => ({ code, pct })).filter((r) => r.pct >= 0.005).sort((a, b) => b.pct - a.pct);
        const countries = rows(country);
        const currencies = rows(currency);
        const regions = d3.rollups(countries.filter((r) => r.code !== 'N/A'), (v) => ({ pct: d3.sum(v, (r) => r.pct), countries: v }), (r) => data.region[r.code] || 'Other')
            .map(([name, v]) => ({ name, ...v })).sort((a, b) => b.pct - a.pct);
        const us = country.get('US') || 0;
        const usd = currency.get('USD') || 0;
        return { countries, currencies, regions, us, usd };
    }

    function callouts (x) {
        const topForeign = x.countries.find((r) => r.code !== 'US' && r.code !== 'N/A');
        const topCur = x.currencies.find((r) => r.code !== 'USD' && r.code !== 'N/A');
        return [
            { label: 'Outside the US', value: fmt(100 - x.us - (x.countries.find((r) => r.code === 'N/A') || { pct: 0 }).pct), note: `Largest: ${topForeign ? `${countryLabel(topForeign.code)} ${fmt(topForeign.pct)}` : 'none'}` },
            { label: 'Not priced in US dollars', value: fmt(100 - x.usd - (x.currencies.find((r) => r.code === 'N/A') || { pct: 0 }).pct), note: `Largest: ${topCur ? `${topCur.code} ${fmt(topCur.pct)}` : 'none'} · ${x.currencies.length - 1} currencies` },
            { label: 'Largest region', value: x.regions[0] ? fmt(x.regions[0].pct) : '—', note: x.regions[0] ? x.regions[0].name : '' }
        ];
    }

    // ---------- map ----------

    function drawMap (host, features, data, rows, opts) {
        const w = host.clientWidth || 720;
        const h = Math.round(w * (opts.ratio || 0.5));
        const svg = d3.select(host).append('svg').attr('viewBox', [0, 0, w, h]).attr('class', 'pw-map').attr('role', 'img');
        const projection = d3.geoEqualEarth().fitSize([w, h], { type: 'FeatureCollection', features });
        const path = d3.geoPath(projection);
        const byNum = new Map(rows.map((r) => [data.iso[r.code], r]));
        const max = d3.max(rows.filter((r) => r.code !== 'N/A' && r.code !== opts.cap), (r) => r.pct) || 1;
        const colour = d3.scaleQuantize().domain([0, Math.max(1, max)]).range(BLUES);
        const g = svg.append('g');
        const ids = new Set(features.map((f) => f.id));
        g.selectAll('path').data(features).join('path').attr('d', path)
            .attr('class', 'pw-country')
            .attr('fill', (f) => {
                const r = byNum.get(f.id);
                if (!r) {
                    return 'var(--pw-empty, #eef0f3)';
                }
                return r.pct >= max ? BLUES[BLUES.length - 1] : colour(r.pct);
            })
            .attr('data-code', (f) => (byNum.get(f.id) || {}).code || '')
            .on('pointermove', (e, f) => {
                const r = byNum.get(f.id);
                opts.tooltip(e, [f.properties.name, r ? `${fmt(r.pct)} of your basket` : 'Not in your basket']);
            })
            .on('pointerleave', () => opts.tooltip(null));
        const missing = rows.filter((r) => r.code !== 'N/A' && !ids.has(data.iso[r.code]));
        if (opts.markers) {
            const rs = d3.scaleSqrt().domain([0, max]).range([3, 14]);
            g.selectAll('circle').data(missing.filter((r) => MARKERS[r.code])).join('circle')
                .attr('class', 'pw-marker').attr('data-code', (r) => r.code)
                .attr('transform', (r) => `translate(${projection(MARKERS[r.code])})`)
                .attr('r', (r) => rs(r.pct)).attr('fill', '#256abf').attr('stroke', '#fff').attr('stroke-width', 1.5)
                .attr('tabindex', 0).attr('aria-label', (r) => `${countryLabel(r.code)}: ${fmt(r.pct)} of your basket`)
                .on('pointermove', (e, r) => opts.tooltip(e, [countryLabel(r.code), `${fmt(r.pct)} of your basket`, 'Drawn as a dot: too small for this map']))
                .on('pointerleave', () => opts.tooltip(null));
        }
        if (opts.zoom) {
            svg.call(d3.zoom().scaleExtent([1, 8]).translateExtent([[0, 0], [w, h]]).on('zoom', (e) => {
                g.attr('transform', e.transform);
                g.selectAll('.pw-marker').attr('r', (r) => { return d3.scaleSqrt().domain([0, max]).range([3, 14])(r.pct) / e.transform.k; });
            }));
        }
        svg.attr('aria-label', `Map of your basket by country of issuer. ${rows.slice(0, 5).map((r) => `${countryLabel(r.code)} ${fmt(r.pct)}`).join(', ')}.`);
        // Legend: quantized steps.
        const legend = el('div', 'pw-legend');
        legend.append(el('span', null, '0%'));
        BLUES.forEach((c) => { const s = el('span', 'pw-legend__step'); s.style.background = c; legend.append(s); });
        legend.append(el('span', null, `${fmt(max)}+`));
        host.append(legend);
        return { svg, missing, highlight: (codes) => g.selectAll('.pw-country, .pw-marker').classed('pw-dim', function () { return codes && !codes.includes(this.dataset.code); }) };
    }

    function bars (host, items, opts) {
        const list = el('ol', `pw-bars ${opts.cls || ''}`);
        const max = d3.max(items, (r) => r.pct) || 1;
        items.forEach((r) => {
            const li = el('li', 'pw-bar');
            const b = el(opts.button ? 'button' : 'div', 'pw-bar__row');
            if (opts.button) {
                b.type = 'button';
            }
            const bar = el('span', 'pw-bar__fill');
            bar.style.setProperty('--w', `${r.pct / max * 100}%`);
            b.append(el('span', 'pw-bar__name', opts.label(r)), el('span', 'pw-bar__pct', fmt(r.pct)), bar);
            if (opts.onEnter) {
                b.addEventListener('pointerenter', () => opts.onEnter(r));
                b.addEventListener('focus', () => opts.onEnter(r));
                b.addEventListener('pointerleave', () => opts.onEnter(null));
                b.addEventListener('blur', () => opts.onEnter(null));
            }
            if (opts.onClick) {
                b.addEventListener('click', () => opts.onClick(r, li));
            }
            li.append(b);
            list.append(li);
        });
        host.append(list);
        return list;
    }

    function stacked (host, items, n) {
        const top = items.filter((r) => r.code !== 'N/A').slice(0, n);
        const rest = 100 - d3.sum(top, (r) => r.pct);
        const parts = top.concat(rest > 0.05 ? [{ code: 'Other', pct: rest }] : []);
        const palette = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e87ba4', '#6250d6', '#9ca3af'];
        const bar = el('div', 'pw-stack');
        bar.setAttribute('role', 'img');
        bar.setAttribute('aria-label', `Currencies: ${parts.map((p) => `${p.code} ${fmt(p.pct)}`).join(', ')}`);
        const key = el('ul', 'pw-stack__key');
        parts.forEach((p, i) => {
            const seg = el('span', 'pw-stack__seg');
            seg.style.width = `${p.pct}%`;
            seg.style.background = p.code === 'Other' ? palette[6] : palette[i];
            seg.title = `${p.code}: ${fmt(p.pct)}`;
            bar.append(seg);
            const k = el('li');
            const sw = el('span', 'pw-swatch');
            sw.style.background = seg.style.background;
            k.append(sw, `${p.code} ${fmt(p.pct)}`);
            key.append(k);
        });
        host.append(bar, key);
    }

    function meter (host, label, value, leftName, rightName) {
        const m = el('div', 'pw-meter');
        m.append(el('span', 'pw-meter__label', label));
        const track = el('div', 'pw-meter__track');
        track.setAttribute('role', 'meter');
        track.setAttribute('aria-valuemin', '0');
        track.setAttribute('aria-valuemax', '100');
        track.setAttribute('aria-valuenow', value.toFixed(1));
        track.setAttribute('aria-label', `${label}: ${leftName} ${fmt(value)}, ${rightName} ${fmt(100 - value)}`);
        const fill = el('span', 'pw-meter__fill');
        fill.style.width = `${value}%`;
        track.append(fill);
        const ends = el('div', 'pw-meter__ends');
        ends.append(el('span', null, `${leftName} ${fmt(value)}`), el('span', null, `${rightName} ${fmt(100 - value)}`));
        m.append(track, ends);
        host.append(m);
    }

    // ---------- mount ----------

    async function mount (data) {
        const panel = document.getElementById('research-panel-world');
        const tabButton = document.getElementById('research-tab-world');
        if (!panel) {
            return;
        }
        const atlasResponse = await fetch('/lib/world-atlas/countries-110m.json');
        const atlas = await atlasResponse.json();
        const features = topojson.feature(atlas, atlas.objects.countries).features.filter((f) => f.id !== '010');

        const existing = Array.from(panel.children);
        const seg = el('div', 'pw-seg');
        seg.setAttribute('role', 'group');
        seg.setAttribute('aria-label', 'World view');
        const card = el('div', 'research-card pw-card');
        const views = [['market', 'World market'], ['basket', 'Your basket']];
        const setView = (v) => {
            seg.querySelectorAll('button').forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.v === v)));
            existing.forEach((n) => { n.style.display = v === 'market' ? '' : 'none'; });
            card.style.display = v === 'basket' ? '' : 'none';
            if (v === 'market') {
                window.dispatchEvent(new Event('resize'));
            }
        };
        views.forEach(([v, label]) => {
            const b = el('button', 'pw-seg__btn', label);
            b.type = 'button';
            b.dataset.v = v;
            b.addEventListener('click', () => setView(v));
            seg.append(b);
        });
        panel.prepend(seg, card);

        card.append(el('h2', 'research-card__title', 'Where your basket is invested'),
            el('p', 'research-card__subtitle', 'By the country where each holding\'s issuer is organized, as each fund reports it, and the currency each holding is priced in. A US-listed share of a foreign company (an ADR) counts as US dollars. This is not where companies earn their revenue.'));
        const picker = el('div', 'pw-picker');
        const stats = el('div', 'pw-stats');
        const stage = el('div', `pw-stage pw-stage--${variant}`);
        const foot = el('p', 'pw-hint');
        card.append(picker, stats, stage, foot, el('p', 'research-card__source', `Source: ${data.source}`));
        const tip = el('div', 'research-tooltip');
        tip.hidden = true;
        document.body.append(tip);
        const tooltip = (e, lines) => {
            if (!e) {
                tip.hidden = true;
                return;
            }
            tip.replaceChildren(...lines.map((l, i) => el('span', i ? 'research-tooltip__muted' : 'research-tooltip__name', l)));
            tip.hidden = false;
            tip.style.position = 'fixed';
            tip.style.left = `${Math.min(e.clientX + 14, window.innerWidth - 260)}px`;
            tip.style.top = `${e.clientY + 14}px`;
        };
        const state = { basket: [{ t: 'VTI', w: 60 }, { t: 'VXUS', w: 30 }, { t: 'BNDX', w: 10 }], lens: 'country' };

        function renderPicker () {
            const chips = Object.keys(data.funds).map((t) => {
                const item = state.basket.find((b) => b.t === t);
                const b = el('button', `po-fund${item ? ' po-fund--on' : ''}`, item ? `${t} ${item.w}%` : t);
                b.type = 'button';
                b.style.setProperty('--c', SLOT[t]);
                b.setAttribute('aria-pressed', String(Boolean(item)));
                b.title = data.funds[t].name;
                b.disabled = !item && state.basket.length >= MAX_FUNDS;
                b.addEventListener('click', () => {
                    const next = item ? state.basket.filter((x) => x.t !== t) : state.basket.concat({ t, w: 0 });
                    const each = Math.round(100 / Math.max(1, next.length));
                    state.basket = next.map((x) => ({ t: x.t, w: each }));
                    draw();
                });
                return b;
            });
            picker.replaceChildren(el('span', 'po-picker__label', 'Fund Basket (stub, equal weights after a change):'), ...chips);
        }

        function draw () {
            renderPicker();
            stage.replaceChildren();
            if (!state.basket.length) {
                stats.replaceChildren();
                stage.append(el('p', 'pw-hint', 'Add a fund to your basket to see where it is invested.'));
                return;
            }
            const x = exposure(data, state.basket);
            stats.replaceChildren(...callouts(x).map((c) => {
                const d = el('div', 'po-stat');
                d.append(el('span', 'po-stat__label', c.label), el('span', 'po-stat__value', c.value), el('span', 'po-stat__note', c.note));
                return d;
            }));
            if (variant === 'A') {
                const left = el('div', 'pw-left');
                const right = el('div', 'pw-right');
                stage.append(left, right);
                const map = drawMap(left, features, data, x.countries, { tooltip, markers: true, zoom: true, cap: 'US' });
                right.append(el('h3', 'pw-h', 'By region'));
                bars(right, x.regions.map((r) => ({ ...r, code: r.name })), {
                    button: true,
                    label: (r) => r.name,
                    onEnter: (r) => map.highlight(r ? r.countries.map((c) => c.code) : null)
                });
                right.append(el('h3', 'pw-h', 'Currencies'));
                const cur = x.currencies.filter((r) => r.code !== 'N/A').slice(0, 5);
                bars(right, cur, { label: (r) => currencyLabel(r.code), cls: 'pw-bars--muted' });
                foot.textContent = 'Scroll or pinch to zoom, drag to pan. Dots mark places too small for this map (Hong Kong, Singapore, Cayman Islands...). Hover a region to light its countries. The US is shown at the darkest step so the rest of the world stays readable.';
            } else if (variant === 'B') {
                const left = el('div', 'pw-left pw-left--list');
                const right = el('div', 'pw-right pw-right--inset');
                stage.append(left, right);
                left.append(el('h3', 'pw-h', 'By region and country'));
                const list = el('div', 'pw-regions');
                x.regions.forEach((r, i) => {
                    const det = el('details', 'pw-region');
                    det.open = i < 2;
                    const sum = el('summary', null);
                    sum.append(el('span', 'pw-bar__name', r.name), el('span', 'pw-bar__pct', fmt(r.pct)));
                    det.append(sum);
                    bars(det, r.countries.slice(0, 12), { label: (c) => countryLabel(c.code) });
                    list.append(det);
                });
                left.append(list);
                right.append(el('h3', 'pw-h', 'On the map'));
                drawMap(right, features, data, x.countries, { tooltip, markers: false, zoom: false, ratio: 0.55, cap: 'US' });
                right.append(el('h3', 'pw-h', 'Currencies'));
                stacked(right, x.currencies, 6);
                foot.textContent = 'Regions open to their countries. Small places such as Hong Kong appear in the list only; the inset map has no shape for them.';
            } else {
                const top = el('div', 'pw-meters');
                meter(top, 'Home bias, by country', x.us, 'US', 'Rest of world');
                meter(top, 'Home bias, by currency', x.usd, 'USD', 'Other currencies');
                const toggle = el('div', 'pw-seg pw-seg--small');
                [['country', 'Country'], ['currency', 'Currency']].forEach(([k, label]) => {
                    const b = el('button', 'pw-seg__btn', label);
                    b.type = 'button';
                    b.setAttribute('aria-pressed', String(state.lens === k));
                    b.addEventListener('click', () => { state.lens = k; draw(); });
                    toggle.append(b);
                });
                stage.append(top, toggle);
                const body = el('div', 'pw-c-body');
                stage.append(body);
                if (state.lens === 'country') {
                    const map = drawMap(body, features, data, x.countries, { tooltip, markers: false, zoom: true, cap: 'US' });
                    if (map.missing.length) {
                        const note = el('p', 'pw-hint', `Not on the map: ${map.missing.map((r) => `${countryLabel(r.code)} ${fmt(r.pct)}`).join(' · ')}`);
                        body.append(note);
                    }
                    foot.textContent = 'Scroll to zoom, drag to pan. Switch to Currency to see what your holdings are priced in.';
                } else {
                    bars(body, x.currencies.filter((r) => r.code !== 'N/A').slice(0, 15), { label: (r) => currencyLabel(r.code) });
                    foot.textContent = 'Currency is what each holding is priced in. A US-listed share of a foreign company counts as US dollars.';
                }
            }
        }

        if (tabButton) {
            tabButton.click();
        }
        setView('basket');
        draw();
    }

    function switcher () {
        const i = VARIANTS.findIndex((v) => v.key === variant);
        const go = (d) => {
            const p = new URLSearchParams(window.location.search);
            p.set('variant', VARIANTS[(i + d + VARIANTS.length) % VARIANTS.length].key);
            window.location.search = p.toString();
        };
        const bar = el('div', 'proto-switcher');
        const prev = el('button', null, '←');
        const next = el('button', null, '→');
        prev.type = 'button';
        next.type = 'button';
        prev.setAttribute('aria-label', 'Previous variant');
        next.setAttribute('aria-label', 'Next variant');
        prev.addEventListener('click', () => go(-1));
        next.addEventListener('click', () => go(1));
        bar.append(prev, el('span', null, `PROTOTYPE ${variant} (${VARIANTS[i].name})`), next);
        document.body.append(bar);
        document.addEventListener('keydown', (e) => {
            if (e.target.closest && e.target.closest('input, textarea, [contenteditable], svg, button, summary')) {
                return;
            }
            if (e.key === 'ArrowLeft') {
                go(-1);
            }
            if (e.key === 'ArrowRight') {
                go(1);
            }
        });
    }

    async function init () {
        switcher();
        try {
            const response = await fetch('/prototype/world-exposure/exposure.json');
            await mount(await response.json());
        } catch (err) {
            document.body.append(el('p', 'pw-hint', 'Prototype data failed to load.'));
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
