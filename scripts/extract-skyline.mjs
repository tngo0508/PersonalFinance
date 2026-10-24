#!/usr/bin/env node
// Extracts the Skyline data for the Landing Page from OpenStreetMap (ADR 0013).
//
// Pulls Stanton, California's buildings, main roads, parks, Coyote Creek and the city boundary from the Overpass API
// and writes one trimmed GeoJSON file the Skyline draws. Regenerate by hand when the city changes:
//
//   node scripts/extract-skyline.mjs
//
// The output is OpenStreetMap data, © OpenStreetMap contributors, available under the ODbL. The script fails if the
// file exceeds the 400 KB gzipped budget.

import { writeFileSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const OVERPASS_URL = 'https://overpass-api.de/api/interpreter';
const USER_AGENT = 'PersonalFinance-skyline-extract/1.0 (https://github.com/tngo0508/PersonalFinance)';

// OSM relation 112980 is the City of Stanton boundary; Overpass areas are the relation id + 3600000000.
const STANTON_RELATION_ID = 112980;
const STANTON_AREA_ID = 3600000000 + STANTON_RELATION_ID;

const GZIP_BUDGET_BYTES = 400 * 1024;
const COORDINATE_DECIMALS = 5; // about 1 m, finer than the Skyline can show

const OUTPUT_PATH = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    '../PersonalFinance/src/PersonalFinance.Web/wwwroot/data/stanton-skyline.json'
);

const QUERY = `
[out:json][timeout:120];
area(${STANTON_AREA_ID})->.city;
(
  way["building"](area.city);
  way["highway"~"^(motorway|trunk|primary|secondary|tertiary)$"](area.city);
  way["leisure"~"^(park|golf_course|pitch)$"](area.city);
  way["waterway"](area.city);
);
out geom;
rel(${STANTON_RELATION_ID});
out geom;
`;

/**
 * Feature kinds, kept to one letter to keep the file small:
 * b = building, r = road, p = park, w = water, c = city boundary.
 */
async function main () {
    const response = await fetch(OVERPASS_URL, {
        method: 'POST',
        headers: { 'User-Agent': USER_AGENT, 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({ data: QUERY })
    });
    if (!response.ok) {
        throw new Error(`Overpass returned ${response.status} ${response.statusText}`);
    }

    const { elements } = await response.json();
    const features = [];

    for (const element of elements) {
        const feature = toFeature(element);
        if (feature) {
            features.push(feature);
        }
    }

    // Deterministic order: kind, then OSM id, so regenerating unchanged data gives an identical file.
    features.sort((a, b) => a.properties.k.localeCompare(b.properties.k) || a.id - b.id);

    const collection = {
        type: 'FeatureCollection',
        attribution: '© OpenStreetMap contributors, ODbL (https://www.openstreetmap.org/copyright)',
        features
    };

    const json = JSON.stringify(collection);
    const gzipped = gzipSync(json, { level: 9 }).length;

    const counts = features.reduce((acc, f) => ({ ...acc, [f.properties.k]: (acc[f.properties.k] || 0) + 1 }), {});
    console.log(`Features: ${JSON.stringify(counts)}`);
    console.log(`Size: ${(json.length / 1024).toFixed(0)} KB raw, ${(gzipped / 1024).toFixed(0)} KB gzipped`);

    if (gzipped > GZIP_BUDGET_BYTES) {
        throw new Error(`The extract is ${(gzipped / 1024).toFixed(0)} KB gzipped, over the 400 KB budget.`);
    }

    writeFileSync(OUTPUT_PATH, json);
    console.log(`Wrote ${OUTPUT_PATH}`);
}

function toFeature (element) {
    const tags = element.tags || {};

    if (element.type === 'relation' && element.id === STANTON_RELATION_ID) {
        const rings = element.members
            .filter((m) => m.type === 'way' && m.role === 'outer' && m.geometry)
            .map((m) => m.geometry.map(toPosition));
        return feature(element.id, 'c', { type: 'MultiLineString', coordinates: rings });
    }

    if (element.type !== 'way' || !element.geometry) {
        return null;
    }

    const line = element.geometry.map(toPosition);

    if (tags.building) {
        return closedRing(line) ? feature(element.id, 'b', polygon(line), { h: buildingHeight(element.id, tags) }) : null;
    }
    if (tags.leisure) {
        return closedRing(line) ? feature(element.id, 'p', polygon(line)) : null;
    }
    if (tags.highway) {
        return feature(element.id, 'r', { type: 'LineString', coordinates: line });
    }
    if (tags.waterway) {
        return feature(element.id, 'w', { type: 'LineString', coordinates: line });
    }
    return null;
}

/**
 * Height in metres: the `height` tag, else `building:levels`, else a typical height for the building type. Untagged
 * houses get a small deterministic variation so the Skyline does not read as one flat slab.
 */
function buildingHeight (id, tags) {
    const height = parseFloat(tags.height);
    if (Number.isFinite(height) && height > 0) {
        return Math.max(3, Math.round(height));
    }

    const levels = parseFloat(tags['building:levels']);
    if (Number.isFinite(levels) && levels > 0) {
        return Math.round(levels * 3.2 + 1);
    }

    const typical = {
        apartments: 10, commercial: 8, retail: 7, industrial: 9, warehouse: 9, office: 12,
        school: 7, church: 10, hotel: 12, garage: 3, shed: 3, roof: 4
    };
    const base = typical[tags.building] ?? 5;
    return base + (id % 3);
}

function feature (id, kind, geometry, extra = {}) {
    return { type: 'Feature', id, properties: { k: kind, ...extra }, geometry };
}

function polygon (ring) {
    return { type: 'Polygon', coordinates: [ring] };
}

function closedRing (line) {
    if (line.length < 4) {
        return false;
    }
    const [first, last] = [line[0], line[line.length - 1]];
    return first[0] === last[0] && first[1] === last[1];
}

function toPosition (point) {
    const factor = 10 ** COORDINATE_DECIMALS;
    return [Math.round(point.lon * factor) / factor, Math.round(point.lat * factor) / factor];
}

main().catch((error) => {
    console.error(error.message);
    process.exit(1);
});
