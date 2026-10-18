/* eslint-env browser */
/* eslint eqeqeq: "off" */

module.exports = {
    env: {
        browser: true,
        es2021: true
    },
    extends: [
        'eslint:recommended'
    ],
    globals: {
        jQuery: 'readonly',
        $: 'readonly',
        Chart: 'readonly',
        DataTable: 'readonly',
        bootstrap: 'readonly',
        marked: 'readonly',
        lucide: 'readonly'
    },
    parserOptions: {
        ecmaVersion: 2022
    },
    rules: {
        // --- Variables ---
        'no-var': 'error',
        'prefer-const': 'error',
        'no-undef': 'error',
        'no-unused-vars': ['error', { 
            'argsIgnorePattern': '^_',
            'varsIgnorePattern': '^_' 
        }],

        // --- Style ---
        'indent': ['error', 4],
        'quotes': ['error', 'single', { 'avoidEscape': true }],
        'semi': ['error', 'always'],
        'comma-dangle': ['error', 'never'],
        'comma-spacing': ['error', { 'before': false, 'after': true }],
        'keyword-spacing': 'error',
        'space-before-function-paren': ['error', {
            'anonymous': 'always',
            'named': 'always',
            'asyncArrow': 'always'
        }],
        'space-infix-ops': 'error',
        'space-unary-ops': 'error',
        'no-multi-spaces': 'error',
        'no-trailing-spaces': 'error',
        'eol-last': 'error',
        'arrow-spacing': 'error',
        'block-spacing': 'error',
        'object-curly-spacing': ['error', 'always'],
        'array-bracket-spacing': ['error', 'never'],
        'key-spacing': ['error', { 'beforeColon': false, 'afterColon': true }],
        'no-multiple-empty-lines': ['error', { 'max': 1, 'maxEOF': 0 }],

        // --- Best Practices ---
        'curly': ['error', 'all'],
        'no-console': 'warn',
        'no-debugger': 'error',
        'no-eval': 'error',
        'no-implied-eval': 'error',
        'no-new-func': 'error',
        'no-return-await': 'error',
        'no-throw-literal': 'error',
        'no-constant-condition': ['error', { 'checkLoops': false }],
        'no-duplicate-imports': 'error',
        'no-async-promise-executor': 'error',
        'no-await-in-loop': 'warn',
        'no-inner-declarations': 'error',
        'no-prototype-builtins': 'error',
        'no-self-assign': 'error',
        'no-self-compare': 'error',
        'no-unmodified-loop-condition': 'error',
        'no-unused-expressions': 'error',
        'no-useless-call': 'error',
        'no-useless-concat': 'error',
        'no-useless-escape': 'error',
        'no-with': 'error',
        'require-await': 'error',
        'wrap-iife': ['error', 'inside'],
        'yoda': ['error', 'never'],

        // --- Potential Bugs ---
        'no-extra-semi': 'error',
        'no-extra-boolean-cast': 'error',
        'no-extra-parens': ['error', 'functions'],
        'no-loss-of-precision': 'error',
        'no-obj-calls': 'error',
        'no-regex-spaces': 'error',
        'no-sparse-arrays': 'error',
        'no-template-curly-in-string': 'error',
        'no-unreachable': 'error',
        'no-unreachable-loop': 'error',
        'no-unsafe-finally': 'error',
        'no-unsafe-negation': 'error',
        'no-unsafe-optional-chaining': 'error',
        'require-atomic-updates': 'error',
        'use-isnan': 'error',
        'valid-typeof': 'error'
    }
};
