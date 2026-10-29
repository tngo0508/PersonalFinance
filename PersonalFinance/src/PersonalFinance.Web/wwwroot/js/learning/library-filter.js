/* global PFI18n */
/**
 * The Library filter on /Learning (#517): narrows the book spines to the Term Entries whose English Term, Term in the
 * page's Language, aliases or Vietnamese name contain what the reader typed. Matching ignores case and Vietnamese
 * diacritics, so "lai kep" finds "lãi kép". A shelf with no match hides; a polite status says how many match.
 * Runs entirely in the browser, so it works for guests.
 */
(function () {
    'use strict';

    /**
     * Folds text for matching: lower case, no diacritics, "đ" as "d".
     * @param {string} text - The text.
     * @returns {string} The folded text.
     */
    function fold (text) {
        return (text || '')
            .normalize('NFD')
            .replace(/[̀-ͯ]/g, '')
            .replace(/đ/gi, 'd')
            .toLowerCase()
            .trim();
    }

    /**
     * Wires the filter input to the spines on the page.
     */
    function init () {
        const input = document.getElementById('atelierFilter');
        const status = document.getElementById('atelierFilterStatus');
        const none = document.getElementById('atelierFilterNone');
        if (!input) {
            return;
        }

        const spines = Array.from(document.querySelectorAll('[data-spine]')).map((item) => ({
            item: item,
            names: (item.getAttribute('data-search') || '').split('|').map(fold)
        }));
        const shelves = Array.from(document.querySelectorAll('[data-shelf]'));

        input.addEventListener('input', () => {
            const query = fold(input.value);
            let shown = 0;
            spines.forEach((spine) => {
                const match = query === '' || spine.names.some((name) => name.indexOf(query) !== -1);
                spine.item.hidden = !match;
                if (match) {
                    shown += 1;
                }
            });
            shelves.forEach((shelf) => {
                shelf.hidden = !shelf.querySelector('[data-spine]:not([hidden])');
            });
            if (none) {
                none.hidden = shown !== 0;
            }
            if (status) {
                status.textContent = query === '' ? '' : PFI18n.plural('Learning.Js.Filter.Matches', shown);
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
