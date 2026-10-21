/**
 * Shared JavaScript utilities for the Personal Finance application.
 * Provides common formatting, escaping, and parsing functions used across modules.
 */
(function (global) {
    'use strict';

    /**
     * Escape HTML special characters to prevent XSS when inserting user-controlled data.
     * @param {*} value - The value to escape.
     * @returns {string} HTML-escaped string.
     */
    function escapeHtml (value) {
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    /**
     * Format a numeric value as a whole-dollar currency string (no cents).
     * @param {number} value - The numeric value to format.
     * @returns {string} Formatted currency string (e.g., "$1,234,567").
     */
    function formatCurrency (value) {
        return '$' + Math.round(value).toLocaleString('en-US');
    }

    /**
     * Format a numeric value as a currency string with cents.
     * @param {number} num - The numeric value to format.
     * @returns {string} Formatted currency string (e.g., "$1,234.56").
     */
    function formatCurrencyCents (num) {
        if (num === null || num === undefined || isNaN(num)) { return '$0.00'; }
        const n = Number(num);
        const formatted = '$' + Math.abs(n).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        return n < 0 ? '-' + formatted : formatted;
    }

    /**
     * Format a numeric value as a compact currency string for small displays.
     * @param {number} value - The numeric value to format.
     * @returns {string} Compact currency string (e.g., "$1.2M", "$50K").
     */
    function formatCompactCurrency (value) {
        if (value >= 1_000_000) {
            return '$' + (value / 1_000_000).toFixed(1) + 'M';
        }
        if (value >= 1_000) {
            return '$' + (value / 1_000).toFixed(0) + 'K';
        }
        return '$' + Math.round(value).toLocaleString('en-US');
    }

    /**
     * Format a numeric value as a compact currency string with sign support.
     * @param {number} val - The numeric value to format.
     * @returns {string} Compact currency string with sign (e.g., "-$1.2M").
     */
    function formatCompactCurrencySigned (val) {
        const abs = Math.abs(val);
        const sign = val < 0 ? '-' : '';
        if (abs >= 1000000) { return sign + '$' + (abs / 1000000).toFixed(1).replace(/\.0$/, '') + 'M'; }
        if (abs >= 1000) { return sign + '$' + (abs / 1000).toFixed(1).replace(/\.0$/, '') + 'K'; }
        return sign + '$' + abs.toLocaleString('en-US');
    }

    /**
     * Safely parse a string to float, returning a default value if parsing fails.
     * @param {string|number} str - The value to parse.
     * @param {number} [defaultValue=0] - The default value if parsing fails.
     * @returns {number} The parsed number or the default value.
     */
    function safeParseFloat (str, defaultValue) {
        if (defaultValue === undefined) { defaultValue = 0; }
        const num = parseFloat(str);
        return isNaN(num) ? defaultValue : num;
    }

    /**
     * Get the antiforgery token to send as the RequestVerificationToken header on state-changing requests:
     * the form field inside container (or the page) if present, otherwise the layout's meta tag.
     * @param {ParentNode} [container] - Element to look in first for a __RequestVerificationToken form field.
     * @returns {string} The token, or an empty string when the page has none.
     */
    function getAntiForgeryToken (container) {
        const input = (container || document).querySelector('input[name="__RequestVerificationToken"]');
        if (input && input.value) {
            return input.value;
        }
        const meta = document.querySelector('meta[name="__RequestVerificationToken"]');
        return meta ? meta.getAttribute('content') || '' : '';
    }

    // Expose utilities on the global object
    global.PFUtils = {
        getAntiForgeryToken: getAntiForgeryToken,
        escapeHtml: escapeHtml,
        formatCurrency: formatCurrency,
        formatCurrencyCents: formatCurrencyCents,
        formatCompactCurrency: formatCompactCurrency,
        formatCompactCurrencySigned: formatCompactCurrencySigned,
        safeParseFloat: safeParseFloat
    };

})(window);
