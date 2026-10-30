'use strict';
/* eslint-env node */

/**
 * Tests for the Review's card rules (wwwroot/js/learning/review-cards.js, issue #520): a card turns back and forth, is
 * marked once and only once it is turned, a failed mark can be made again, and focus moves to the next unmarked card.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const SOURCE = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'learning', 'review-cards.js'), 'utf8');

/**
 * Loads review-cards.js without a DOM and returns a deck.
 * @param {number} total - How many cards the Review shows.
 * @returns {object} The deck.
 */
function deck (total) {
    const context = vm.createContext({});
    vm.runInContext(SOURCE, context);
    return context.PFReviewCards.createDeck(total);
}

test('a card turns to its back and back again', () => {
    const cards = deck(3);

    assert.strictEqual(cards.turn(0), true);
    assert.strictEqual(cards.isTurned(0), true);
    assert.strictEqual(cards.turn(0), false);
    assert.strictEqual(cards.isTurned(0), false);
});

test('a card cannot be marked before it is turned', () => {
    const cards = deck(3);

    assert.strictEqual(cards.mark(1), false);
    assert.strictEqual(cards.isMarked(1), false);
});

test('a turned card is marked once', () => {
    const cards = deck(3);
    cards.turn(1);

    assert.strictEqual(cards.mark(1), true);
    assert.strictEqual(cards.mark(1), false);
    assert.strictEqual(cards.isMarked(1), true);
});

test('a released mark can be made again', () => {
    const cards = deck(2);
    cards.turn(0);
    cards.mark(0);

    cards.release(0);

    assert.strictEqual(cards.isMarked(0), false);
    assert.strictEqual(cards.mark(0), true);
});

test('the next unmarked card wraps past the end and skips marked ones', () => {
    const cards = deck(3);
    cards.turn(0);
    cards.mark(0);
    cards.turn(2);
    cards.mark(2);

    assert.strictEqual(cards.nextUnmarked(2), 1);
    assert.strictEqual(cards.nextUnmarked(0), 1);
});

test('the Review is done once every card is marked', () => {
    const cards = deck(2);
    cards.turn(0);
    cards.mark(0);
    assert.strictEqual(cards.allMarked(), false);

    cards.turn(1);
    cards.mark(1);

    assert.strictEqual(cards.allMarked(), true);
    assert.strictEqual(cards.nextUnmarked(1), -1);
});

test('ignores a card outside the deck', () => {
    const cards = deck(2);

    assert.strictEqual(cards.turn(2), false);
    assert.strictEqual(cards.turn(-1), false);
    assert.strictEqual(cards.mark(2), false);
});
