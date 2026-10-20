// Isolated browser checks; requests are intercepted, so no account or AI tokens are needed.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const path = require('node:path');

test('assistant conversation, recovery, keyboard access and mobile layout', async () => {
    const browser = await chromium.launch();
    try {
        const page = await browser.newPage();
        const requests = [];
        let fail = false;
        await page.route('https://assistant.test/**', async route => {
            if (route.request().method() === 'POST') {
                requests.push(route.request().postDataJSON());
                await route.fulfill({ status: fail ? 502 : 200, contentType: 'application/json', body: JSON.stringify({ response: '**Review** your savings goals.\n\n<script>window.unsafe = true</script><img src=x onerror="window.unsafe = true">\n\n[Unsafe](javascript:alert(1)) [Guide](https://example.com)' }) });
            } else {
                await route.fulfill({ contentType: 'text/html', body: '<!doctype html><html><body><button>Page action</button></body></html>' });
            }
        });
        await page.goto('https://assistant.test/');
        const root = path.resolve(__dirname, '../PersonalFinance/src/PersonalFinance.Web/wwwroot');
        await page.addStyleTag({ path: path.join(root, 'css/ai-chat-widget.css') });
        await page.addScriptTag({ path: path.join(root, 'lib/marked/marked.min.js') });
        await page.addScriptTag({ path: path.join(root, 'js/core/ai-chat-widget.js') });
        const panel = page.locator('#ai-chat-panel');
        const input = page.locator('#ai-chat-input');
        assert.equal(await panel.isVisible(), false);
        await page.locator('#ai-chat-button').click();
        await page.locator('.ai-chat-chip').first().waitFor();
        assert.equal(await page.locator('.ai-chat-chip').count(), 4);
        await input.fill('Budget\nquestion');
        await input.press('Shift+Enter');
        assert.equal(requests.length, 0);
        await input.fill('Review my budget');
        await input.press('Enter');
        await page.waitForFunction(() => document.querySelectorAll('.ai-chat-message-assistant').length === 1 && !document.querySelector('#ai-chat-input').disabled);
        assert.equal(await page.locator('.ai-chat-bubble strong').textContent(), 'Review');
        assert.equal(await page.locator('.ai-chat-bubble script, .ai-chat-bubble img, .ai-chat-bubble a[href^="javascript:"]').count(), 0);
        assert.equal(await page.evaluate(() => window.unsafe), undefined);
        await input.fill('How can I save more?');
        await input.press('Enter');
        await page.waitForFunction(() => document.querySelectorAll('.ai-chat-message-assistant').length === 2 && !document.querySelector('#ai-chat-input').disabled);
        assert.equal(await page.locator('.ai-chat-message').count(), 4);
        assert.equal(requests[0].conversationHistory.length, 0);
        assert.equal(requests[1].conversationHistory.length, 2);
        fail = true;
        await input.fill('Review my savings goals');
        await input.press('Enter');
        await page.waitForFunction(() => document.querySelector('#ai-chat-status').textContent.includes('try again'));
        assert.equal(await input.inputValue(), 'Review my savings goals');
        assert.equal(await page.locator('.ai-chat-message').count(), 5);
        await page.setViewportSize({ width: 375, height: 812 });
        const bounds = await panel.boundingBox();
        assert.ok(bounds.x >= 0 && bounds.x + bounds.width <= 375);
        assert.ok(bounds.y >= 0 && bounds.y + bounds.height <= 812 - 76);
        await input.press('Escape');
        assert.equal(await panel.isVisible(), false);
        assert.equal(await page.locator('#ai-chat-button').evaluate(el => el === document.activeElement), true);
        await page.locator('#ai-chat-button').click();
        assert.equal(await page.locator('.ai-chat-message').count(), 5);
        await page.locator('#ai-chat-reset').click();
        assert.equal(await page.locator('.ai-chat-chip').count(), 4);
        assert.equal(await page.locator('.ai-chat-message').count(), 0);
        assert.equal(await input.inputValue(), '');
        assert.equal(await page.locator('#ai-chat-send').isDisabled(), true);
        if (process.env.ASSISTANT_SCREENSHOT) {
            await page.screenshot({ path: process.env.ASSISTANT_SCREENSHOT });
        }
    } finally {
        await browser.close();
    }
});
