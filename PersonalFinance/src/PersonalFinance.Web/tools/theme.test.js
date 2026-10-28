'use strict';
/* eslint-env node */

/**
 * Tests for the theme mode (wwwroot/js/core/theme.js, issues #296 and #481): with no stored choice the page follows the
 * device setting, a stored Light or Dark wins over it, System clears the choice, and the one top-bar control shows the
 * mode in effect and opens a popover of Light / Dark / System.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const SOURCE = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'core', 'theme.js'), 'utf8');
const CHOICES = ['light', 'dark', 'system'];

/**
 * Does an element match an attribute selector ("[name]" or "[name=\"value\"]")? The only selectors theme.js uses.
 * @param {object} element - A fake element.
 * @param {string} selector - The selector.
 * @returns {boolean}
 */
function matches (element, selector) {
    const parts = /^\[([\w-]+)(?:="([^"]*)")?\]$/.exec(selector);
    if (!parts) {
        throw new Error(`Unsupported selector: ${selector}`);
    }
    return element.hasAttribute(parts[1]) && (parts[2] === undefined || element.getAttribute(parts[1]) === parts[2]);
}

/**
 * An element with just enough of the DOM for theme.js: attributes, hidden, children, closest, querySelector(All),
 * contains and focus.
 * @param {object} doc - The fake document, for focus.
 * @param {object} attributes - Initial attributes.
 * @param {object[]} children - Child elements.
 * @returns {object} The fake element.
 */
function fakeElement (doc, attributes, children) {
    const attrs = Object.assign({}, attributes);
    const element = {
        hidden: false,
        parentNode: null,
        children: children || [],
        getAttribute: (name) => (name in attrs ? attrs[name] : null),
        setAttribute: (name, value) => { attrs[name] = String(value); },
        hasAttribute: (name) => name in attrs,
        removeAttribute: (name) => { delete attrs[name]; },
        closest: (selector) => {
            for (let node = element; node; node = node.parentNode) {
                if (matches(node, selector)) {
                    return node;
                }
            }
            return null;
        },
        querySelectorAll: (selector) => {
            const found = [];
            const walk = (node) => node.children.forEach((child) => {
                if (matches(child, selector)) {
                    found.push(child);
                }
                walk(child);
            });
            walk(element);
            return found;
        },
        querySelector: (selector) => element.querySelectorAll(selector)[0] || null,
        contains: (node) => {
            for (let current = node; current; current = current.parentNode) {
                if (current === element) {
                    return true;
                }
            }
            return false;
        },
        focus: () => { doc.activeElement = element; }
    };
    element.children.forEach((child) => { child.parentNode = element; });
    return element;
}

/**
 * Load theme.js against a fake page holding the _ThemeSwitcher markup.
 * @param {object} options - stored: the localStorage value; deviceDark: prefers-color-scheme; blockStorage: throw on access.
 * @returns {object} The page: ThemeMode, root attributes, the control's parts, storage, and helpers to click, press keys
 * or flip the device.
 */
function load (options) {
    const opts = options || {};
    const store = {};
    if (opts.stored !== undefined) {
        store.theme = opts.stored;
    }
    // What _Layout's head script sets before theme.js runs.
    const headTheme = opts.stored === 'light' || opts.stored === 'dark' ? opts.stored : (opts.deviceDark ? 'dark' : 'light');
    const rootAttrs = { 'data-theme': headTheme, 'data-bs-theme': headTheme };
    const meta = { content: '#ffffff', setAttribute (name, value) { this[name] = value; } };
    const listeners = {};
    const mediaListeners = [];
    const media = { matches: Boolean(opts.deviceDark), addEventListener: (type, fn) => mediaListeners.push(fn) };
    const events = [];

    const document = { activeElement: null };
    const icons = CHOICES.map((choice) => fakeElement(document, { 'data-theme-icon': choice }));
    const button = fakeElement(document, {
        'data-theme-button': '',
        'aria-expanded': 'false',
        'data-label-light': 'Theme: Light',
        'data-label-dark': 'Theme: Dark',
        'data-label-system': 'Theme: System'
    }, icons);
    const choices = CHOICES.map((choice) => fakeElement(document, { 'data-theme-choice': choice, role: 'radio' }));
    const popover = fakeElement(document, { 'data-theme-popover': '' }, choices);
    popover.hidden = true;
    const switcher = fakeElement(document, { 'data-theme-switcher': '' }, [button, popover]);
    const outside = fakeElement(document, { 'data-outside': '' });
    const body = fakeElement(document, {}, [switcher, outside]);
    document.activeElement = body;

    const guard = (fn) => function () {
        if (opts.blockStorage) {
            throw new Error('SecurityError');
        }
        return fn.apply(null, arguments);
    };

    const window = {
        localStorage: {
            getItem: guard((key) => (key in store ? store[key] : null)),
            setItem: guard((key, value) => { store[key] = String(value); }),
            removeItem: guard((key) => { delete store[key]; })
        },
        matchMedia: () => media,
        addEventListener: () => {},
        CustomEvent: function (type, init) { this.type = type; this.detail = init.detail; },
        Element: Object,
        document: Object.assign(document, {
            readyState: 'complete',
            body: body,
            documentElement: {
                getAttribute: (name) => (name in rootAttrs ? rootAttrs[name] : null),
                setAttribute: (name, value) => { rootAttrs[name] = value; }
            },
            querySelector: (selector) => (selector === 'meta[name="theme-color"]' ? meta : body.querySelector(selector)),
            querySelectorAll: (selector) => body.querySelectorAll(selector),
            addEventListener: (type, fn) => { listeners[type] = fn; },
            dispatchEvent: (event) => events.push(event)
        })
    };
    window.window = window;
    vm.createContext(window);
    vm.runInContext(SOURCE, window);

    return {
        theme: window.ThemeMode,
        root: rootAttrs,
        meta: meta,
        button: button,
        popover: popover,
        choices: choices,
        outside: outside,
        store: store,
        events: events,
        focused: () => document.activeElement,
        visibleIcons: () => icons.filter((icon) => !icon.hidden).map((icon) => icon.getAttribute('data-theme-icon')),
        checked: () => choices.map((choice) => choice.getAttribute('aria-checked')),
        isOpen: () => button.getAttribute('aria-expanded') === 'true' && !popover.hidden,
        click: (element) => {
            element.focus();
            listeners.click({ target: element });
        },
        press: (key) => {
            const event = { key: key, target: document.activeElement, defaultPrevented: false };
            event.preventDefault = () => { event.defaultPrevented = true; };
            listeners.keydown(event);
            return event;
        },
        setDevice: (dark) => {
            media.matches = dark;
            mediaListeners.forEach((fn) => fn());
        }
    };
}

test('with no stored choice the page follows a dark device setting', () => {
    const page = load({ deviceDark: true });
    assert.strictEqual(page.root['data-theme'], 'dark');
    assert.strictEqual(page.root['data-bs-theme'], 'dark');
    assert.strictEqual(page.meta.content, '#0E1821');
    assert.strictEqual(page.theme.getChoice(), 'system');
});

test('a stored Light wins over a dark device setting', () => {
    const page = load({ stored: 'light', deviceDark: true });
    assert.strictEqual(page.root['data-theme'], 'light');
    assert.strictEqual(page.root['data-bs-theme'], 'light');
});

test('an unknown stored value reads as System', () => {
    const page = load({ stored: 'sepia', deviceDark: false });
    assert.strictEqual(page.theme.getChoice(), 'system');
    assert.strictEqual(page.root['data-theme'], 'light');
});

test('the button shows the sun, moon or monitor and names the mode for each choice', () => {
    const cases = [
        { stored: 'light', icon: 'light', label: 'Theme: Light' },
        { stored: 'dark', icon: 'dark', label: 'Theme: Dark' },
        { stored: undefined, icon: 'system', label: 'Theme: System' }
    ];
    cases.forEach((c) => {
        const page = load({ stored: c.stored, deviceDark: true });
        assert.deepStrictEqual(page.visibleIcons(), [c.icon], c.label);
        assert.strictEqual(page.button.getAttribute('aria-label'), c.label);
        assert.strictEqual(page.button.getAttribute('title'), c.label);
    });
});

test('only the current choice is checked and reachable by Tab', () => {
    const page = load({ stored: 'dark' });
    assert.deepStrictEqual(page.checked(), ['false', 'true', 'false']);
    assert.deepStrictEqual(page.choices.map((c) => c.getAttribute('tabindex')), ['-1', '0', '-1']);
});

test('clicking the button opens the popover on the checked choice, and again closes it', () => {
    const page = load({ stored: 'light' });
    assert.strictEqual(page.isOpen(), false);

    page.click(page.button);
    assert.strictEqual(page.isOpen(), true);
    assert.strictEqual(page.focused(), page.choices[0]);

    page.click(page.button);
    assert.strictEqual(page.isOpen(), false);
    assert.strictEqual(page.focused(), page.button);
});

test('choosing Dark stores it, applies it, closes the popover and returns focus to the button', () => {
    const page = load({ deviceDark: false });
    assert.strictEqual(page.events.length, 0);
    page.click(page.button);
    page.click(page.choices[1]);

    assert.strictEqual(page.store.theme, 'dark');
    assert.strictEqual(page.root['data-theme'], 'dark');
    assert.deepStrictEqual(page.checked(), ['false', 'true', 'false']);
    assert.deepStrictEqual(page.visibleIcons(), ['dark']);
    assert.strictEqual(page.button.getAttribute('aria-label'), 'Theme: Dark');
    assert.strictEqual(page.isOpen(), false);
    assert.strictEqual(page.focused(), page.button);
    assert.strictEqual(page.events.length, 1);
    assert.strictEqual(page.events[0].detail.theme, 'dark');
});

test('choosing System clears the stored choice and returns to the device setting', () => {
    const page = load({ stored: 'dark', deviceDark: false });
    page.click(page.button);
    page.click(page.choices[2]);
    assert.ok(!('theme' in page.store));
    assert.strictEqual(page.root['data-theme'], 'light');
    assert.deepStrictEqual(page.visibleIcons(), ['system']);
});

test('Escape closes the popover without a choice and returns focus to the button', () => {
    const page = load({ stored: 'light' });
    page.click(page.button);
    page.press('ArrowDown');
    const event = page.press('Escape');

    assert.strictEqual(event.defaultPrevented, true);
    assert.strictEqual(page.isOpen(), false);
    assert.strictEqual(page.focused(), page.button);
    assert.strictEqual(page.store.theme, 'light');
});

test('a click outside closes the popover without a choice', () => {
    const page = load({ stored: 'light' });
    page.click(page.button);
    page.click(page.outside);

    assert.strictEqual(page.isOpen(), false);
    assert.strictEqual(page.store.theme, 'light');
    assert.strictEqual(page.root['data-theme'], 'light');
});

test('the arrow keys move between the choices and wrap around', () => {
    const page = load({ stored: 'dark' });
    page.click(page.button);
    assert.strictEqual(page.focused(), page.choices[1]);

    page.press('ArrowDown');
    assert.strictEqual(page.focused(), page.choices[2]);
    page.press('ArrowRight');
    assert.strictEqual(page.focused(), page.choices[0]);
    page.press('ArrowUp');
    assert.strictEqual(page.focused(), page.choices[2]);
    page.press('ArrowLeft');
    assert.strictEqual(page.focused(), page.choices[1]);
    assert.strictEqual(page.store.theme, 'dark', 'moving does not choose');
});

test('arrow keys outside the open popover are left alone', () => {
    const page = load({ stored: 'dark' });
    page.outside.focus();
    const event = page.press('ArrowDown');
    assert.strictEqual(event.defaultPrevented, false);
    assert.strictEqual(page.focused(), page.outside);
});

test('a device change is followed under System and ignored under an explicit choice', () => {
    const followed = load({ deviceDark: false });
    followed.setDevice(true);
    assert.strictEqual(followed.root['data-theme'], 'dark');
    assert.deepStrictEqual(followed.visibleIcons(), ['system']);
    assert.strictEqual(followed.button.getAttribute('aria-label'), 'Theme: System');

    const pinned = load({ stored: 'light', deviceDark: false });
    pinned.setDevice(true);
    assert.strictEqual(pinned.root['data-theme'], 'light');
});

test('blocked storage still switches the page', () => {
    const page = load({ deviceDark: false, blockStorage: true });
    assert.strictEqual(page.theme.getChoice(), 'system');
    page.click(page.button);
    page.click(page.choices[1]);
    assert.strictEqual(page.root['data-theme'], 'dark');
});
