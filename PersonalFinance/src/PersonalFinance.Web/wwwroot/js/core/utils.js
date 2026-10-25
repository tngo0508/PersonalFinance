/* global PFI18n */
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
     * Format a numeric value as a whole-unit US dollar string (no cents), in the page culture.
     * @param {number} value - The numeric value to format.
     * @returns {string} Formatted currency string (e.g., "$1,234,567"; "1.234.567 $" in vi-VN).
     */
    function formatCurrency (value) {
        return PFI18n.formatMoney(value, 'USD', { whole: true });
    }

    /**
     * Format a numeric value as a US dollar string with cents, in the page culture.
     * @param {number} num - The numeric value to format.
     * @returns {string} Formatted currency string (e.g., "$1,234.56"; "-$1,234.56").
     */
    function formatCurrencyCents (num) {
        if (num === null || num === undefined || isNaN(num)) { return PFI18n.formatMoney(0, 'USD'); }
        return PFI18n.formatMoney(Number(num), 'USD');
    }

    /**
     * Format a numeric value as a compact US dollar string for small displays, in the page culture.
     * @param {number} value - The numeric value to format.
     * @returns {string} Compact currency string (e.g., "$1.2M", "$50K").
     */
    function formatCompactCurrency (value) {
        return PFI18n.formatCompactMoney(value, 'USD');
    }

    /**
     * Format a numeric value as a compact US dollar string with sign support, in the page culture.
     * @param {number} val - The numeric value to format.
     * @returns {string} Compact currency string with sign (e.g., "-$1.2M").
     */
    function formatCompactCurrencySigned (val) {
        return PFI18n.formatCompactMoney(val, 'USD');
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
