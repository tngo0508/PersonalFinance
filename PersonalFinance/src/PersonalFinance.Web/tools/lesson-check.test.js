'use strict';
/* eslint-env node */

/**
 * Tests for the Check's finishing rule (wwwroot/js/learning/lesson-check.js, issue #519): "finish" is posted once, and
 * only after every question is answered, whatever the answers and however often a question is answered again.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const SOURCE = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'learning', 'lesson-check.js'), 'utf8');

/**
 * Loads lesson-check.js without a DOM and returns its progress tracker with a count of finish posts.
 * @param {number} total - How many questions the Check has.
 * @returns {{progress: object, posts: function(): number}} The tracker and how many times it posted.
 */
function load (total) {
    const context = vm.createContext({});
    vm.runInContext(SOURCE, context);
    let posts = 0;
    const progress = context.PFLessonCheck.createProgress(total, () => {
        posts += 1;
    });
    return { progress, posts: () => posts };
}

test('does not finish before every question is answered', () => {
    const { progress, posts } = load(3);

    progress.answer(0);
    progress.answer(2);

    assert.strictEqual(posts(), 0);
    assert.strictEqual(progress.answeredCount(), 2);
});

test('finishes once the last question is answered, in any order', () => {
    const { progress, posts } = load(3);

    assert.strictEqual(progress.answer(2), false);
    assert.strictEqual(progress.answer(0), false);
    assert.strictEqual(progress.answer(1), true);

    assert.strictEqual(posts(), 1);
});

test('answering the same question twice does not count as two answers', () => {
    const { progress, posts } = load(2);

    progress.answer(0);
    progress.answer(0);

    assert.strictEqual(posts(), 0);
});

test('posts only once, even if answers keep coming', () => {
    const { progress, posts } = load(2);

    progress.answer(0);
    progress.answer(1);
    progress.answer(1);
    progress.answer(0);

    assert.strictEqual(posts(), 1);
});

test('ignores an index outside the Check', () => {
    const { progress, posts } = load(2);

    progress.answer(0);
    progress.answer(2);
    progress.answer(-1);

    assert.strictEqual(posts(), 0);
});
