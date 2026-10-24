#!/usr/bin/env node
// Captures the Skyline poster for the Landing Page (ADR 0013).
//
// Serves the Web project's wwwroot locally, renders the finished Skyline with the real skyline.js in headless
// Chromium, and saves the canvas as a WebP of at most 150 KB. Rerun after changing the Skyline's look or data:
//
//   node scripts/capture-skyline-poster.mjs
//
// The poster shows OpenStreetMap data, © OpenStreetMap contributors (ODbL); the Landing Page credits it.

import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { chromium } from 'playwright';

const WWWROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../PersonalFinance/src/PersonalFinance.Web/wwwroot');
const OUTPUT_PATH = path.join(WWWROOT, 'images/landing/skyline-poster.webp');

const WIDTH = 1920;
const HEIGHT = 760;
const BUDGET_BYTES = 150 * 1024;
const QUALITIES = [0.82, 0.74, 0.66, 0.58, 0.5];

const CONTENT_TYPES = { '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json' };

const HARNESS = `<!doctype html>
<html><head><meta charset="utf-8"><style>
  html, body { margin: 0; }
  .lp-skyline { position: relative; width: ${WIDTH}px; height: ${HEIGHT}px; }
  .lp-skyline .lp-skyline__map { position: absolute; inset: 0; }
</style></head><body>
  <div class="lp-skyline" data-skyline data-skyline-capture
       data-skyline-script="/lib/maplibre-gl/dist/maplibre-gl.js"
       data-skyline-css="/lib/maplibre-gl/dist/maplibre-gl.css"
       data-skyline-data="/data/stanton-skyline.json">
    <div class="lp-skyline__map" data-skyline-map></div>
  </div>
  <script src="/js/landing/skyline.js"></script>
</body></html>`;

const server = createServer(async (request, response) => {
    const url = new URL(request.url, 'http://localhost');
    if (url.pathname === '/') {
        response.writeHead(200, { 'Content-Type': 'text/html' }).end(HARNESS);
        return;
    }
    const file = path.join(WWWROOT, decodeURIComponent(url.pathname));
    if (!file.startsWith(WWWROOT)) {
        response.writeHead(403).end();
        return;
    }
    try {
        const body = await readFile(file);
        response.writeHead(200, { 'Content-Type': CONTENT_TYPES[path.extname(file)] || 'application/octet-stream' }).end(body);
    } catch {
        response.writeHead(404).end();
    }
});

await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
const { port } = server.address();

const browser = await chromium.launch({ args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
try {
    const page = await browser.newPage({ viewport: { width: WIDTH, height: HEIGHT } });
    await page.goto(`http://127.0.0.1:${port}/`);
    await page.waitForSelector('[data-skyline-ready="true"]', { timeout: 120000 });

    let image = null;
    for (const quality of QUALITIES) {
        const dataUrl = await page.evaluate((q) => document.querySelector('[data-skyline-map] canvas').toDataURL('image/webp', q), quality);
        image = Buffer.from(dataUrl.split(',')[1], 'base64');
        console.log(`Quality ${quality}: ${(image.length / 1024).toFixed(0)} KB`);
        if (image.length <= BUDGET_BYTES) {
            break;
        }
    }

    if (image.length > BUDGET_BYTES) {
        throw new Error(`The poster is ${(image.length / 1024).toFixed(0)} KB, over the 150 KB budget.`);
    }

    await mkdir(path.dirname(OUTPUT_PATH), { recursive: true });
    await writeFile(OUTPUT_PATH, image);
    console.log(`Wrote ${OUTPUT_PATH}`);
} finally {
    await browser.close();
    server.close();
}
