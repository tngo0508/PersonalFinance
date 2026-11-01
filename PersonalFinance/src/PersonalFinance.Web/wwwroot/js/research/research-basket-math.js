/**
 * Research, the Fund Basket's figures: one place that turns the basket and the snapshots into the basket's annualized
 * return, volatility and Sharpe ratio (the risk chart's basket diamond and the Basket page's overview), its average
 * expense ratio and its US share.
 *
 * The basket's monthly return is its funds' monthly returns weighted by their shares, rebalanced every month; from
 * those months come the same figures the refresh computes for a fund: annualized return, volatility (sample standard
 * deviation of the monthly returns times √12) and a Sharpe ratio against the average 3-month Treasury bill over the
 * same months (from the macro snapshot). Weights that do not add up to 100 are scaled to 100.
 *
 * Pure: no DOM access, so `node --test` loads it (tools/research-basket-math.test.js). Exposed as
 * `window.ResearchBasketMath`.
 */
(function () {
    'use strict';

    const US = 'US';

    /**
     * A month's key.
     * @param {string} isoDate - The month's first day, e.g. "2026-06-01".
     * @returns {string} The key, e.g. "2026-06".
     */
    function monthKey (isoDate) {
        return isoDate.slice(0, 7);
    }

    /**
     * The month a number of months after another.
     * @param {string} key - The month.
     * @param {number} count - How many months later (negative for earlier).
     * @returns {string} The month's key.
     */
    function addMonths (key, count) {
        const [year, month] = key.split('-').map(Number);
        const index = year * 12 + (month - 1) + count;
        return `${Math.floor(index / 12)}-${String(index % 12 + 1).padStart(2, '0')}`;
    }

    /**
     * How many months from one month to another.
     * @param {string} from - The first month.
     * @param {string} to - The second month.
     * @returns {number} The months between them.
     */
    function monthsBetween (from, to) {
        const [y1, m1] = from.split('-').map(Number);
        const [y2, m2] = to.split('-').map(Number);
        return (y2 - y1) * 12 + (m2 - m1);
    }

    /**
     * A basket fund's weight: its share of the basket when the page computed one, else its address weight.
     * @param {{share: (number|undefined), weight: (number|null|undefined)}} fund - The basket fund.
     * @returns {number|null} The weight, or null when the basket shares equally.
     */
    function weightOf (fund) {
        if (typeof fund.share === 'number') {
            return fund.share;
        }
        return typeof fund.weight === 'number' ? fund.weight : null;
    }

    /**
     * Each fund's part of the basket, adding up to 1: its weight over the funds' total, or an equal part when a fund has
     * no weight.
     * @param {Object[]} funds - The basket funds to share among.
     * @returns {number[]} The parts, in order.
     */
    function partsOf (funds) {
        const weights = funds.map(weightOf);
        if (weights.some((w) => w === null)) {
            return funds.map(() => 1 / funds.length);
        }
        const total = weights.reduce((sum, w) => sum + w, 0);
        return weights.map((w) => (total > 0 ? w / total : 1 / funds.length));
    }

    /**
     * The basket's figures over its last months: its funds' monthly returns over those months, weighted, then the same
     * figures the refresh computes for a fund. The months end at the earliest last month of the basket's funds.
     * @param {Object} returns - The fund returns snapshot.
     * @param {Object|null} macro - The macro snapshot, for the Treasury bill rate; null leaves the Sharpe ratio out.
     * @param {Object[]} basket - The basket's funds (ticker, and share or weight).
     * @param {number} months - How many months.
     * @returns {{annualizedReturn: number, volatility: number, sharpe: (number|null), treasuryBill: (number|null),
     *     start: string, end: string}|null} The figures, or null when no basket fund has returns or one lacks a month.
     */
    function pointForMonths (returns, macro, basket, months) {
        const series = new Map(returns.funds.map((f) => [f.ticker, f]));
        const funds = basket.filter((f) => series.has(f.ticker));
        if (funds.length === 0 || months < 2) {
            return null;
        }
        const parts = partsOf(funds);
        const end = funds.map((f) => monthKey(series.get(f.ticker).lastMonth)).sort()[0];
        const start = addMonths(end, 1 - months);
        const monthly = [];
        for (let i = 0; i < months; i++) {
            const month = addMonths(start, i);
            let sum = 0;
            for (let f = 0; f < funds.length; f++) {
                const s = series.get(funds[f].ticker);
                const value = s.returns[monthsBetween(monthKey(s.firstMonth), month)];
                if (value === undefined || value === null) {
                    return null;
                }
                sum += parts[f] * value;
            }
            monthly.push(sum);
        }
        const growth = monthly.reduce((g, r) => g * (1 + r / 100), 1);
        const annualized = (Math.pow(growth, 12 / months) - 1) * 100;
        const mean = monthly.reduce((a, r) => a + r, 0) / months;
        const volatility = Math.sqrt(monthly.reduce((a, r) => a + (r - mean) * (r - mean), 0) / (months - 1)) * Math.sqrt(12);
        const bills = new Map((macro ? macro.months : [])
            .filter((m) => m.treasuryBill !== null && m.treasuryBill !== undefined)
            .map((m) => [monthKey(m.month), m.treasuryBill]));
        const windowBills = Array.from({ length: months }, (_, i) => bills.get(addMonths(start, i))).filter((b) => b !== undefined);
        const treasuryBill = windowBills.length ? windowBills.reduce((a, b) => a + b, 0) / windowBills.length : null;
        return {
            annualizedReturn: annualized,
            volatility,
            sharpe: treasuryBill === null || volatility <= 0 ? null : (annualized - treasuryBill) / volatility,
            treasuryBill,
            start,
            end
        };
    }

    /**
     * The basket's figures over a window of years: see `pointForMonths`.
     * @param {Object} returns - The fund returns snapshot.
     * @param {Object|null} macro - The macro snapshot.
     * @param {Object[]} basket - The basket's funds (ticker, and share or weight).
     * @param {number} years - The window's length in years.
     * @returns {Object|null} The figures, or null when a basket fund lacks a month of the window.
     */
    function point (returns, macro, basket, years) {
        return pointForMonths(returns, macro, basket, years * 12);
    }

    /**
     * The basket's average expense ratio: the funds that filed one, re-weighted among themselves; the others are listed.
     * @param {Object|null} facts - The fund facts snapshot.
     * @param {Object[]} basket - The basket's funds.
     * @returns {{expenseRatio: (number|null), missingFees: string[]}} The average (null when no fund filed one) and the
     *     tickers without one.
     */
    function averageFee (facts, basket) {
        const fees = new Map((facts ? facts.funds : [])
            .filter((f) => f.expenseRatio !== null && f.expenseRatio !== undefined)
            .map((f) => [f.ticker, f.expenseRatio]));
        const filed = basket.filter((f) => fees.has(f.ticker));
        const parts = partsOf(filed);
        return {
            expenseRatio: filed.length === 0 ? null : filed.reduce((sum, f, i) => sum + parts[i] * fees.get(f.ticker), 0),
            missingFees: basket.filter((f) => !fees.has(f.ticker)).map((f) => f.ticker)
        };
    }

    /**
     * The basket's share in US issuers: each fund's US share weighted by its part of the basket; a fund without
     * exposure data is left out and the others scaled up to cover it, as the World basket view does.
     * @param {Object|null} exposure - The fund exposure snapshot.
     * @param {Object[]} basket - The basket's funds.
     * @returns {number|null} The share in percent, or null when no basket fund has exposure data.
     */
    function usShare (exposure, basket) {
        const byTicker = new Map((exposure ? exposure.funds : []).map((f) => [f.ticker, f]));
        const funds = basket.filter((f) => byTicker.has(f.ticker));
        if (funds.length === 0) {
            return null;
        }
        const parts = partsOf(funds);
        return funds.reduce((sum, f, i) => {
            const us = byTicker.get(f.ticker).countries.find((c) => c.code === US);
            return sum + parts[i] * (us ? us.percent : 0);
        }, 0);
    }

    /**
     * The Basket page's overview: the basket's return figures over a window (as the risk chart's basket point), its
     * average expense ratio and its US share.
     * @param {Object|null} returns - The fund returns snapshot, or null when it has never loaded.
     * @param {Object|null} macro - The macro snapshot.
     * @param {Object|null} facts - The fund facts snapshot.
     * @param {Object|null} exposure - The fund exposure snapshot.
     * @param {Object[]} basket - The basket's funds (ticker, and share or weight).
     * @param {number} years - The window's length in years.
     * @returns {{annualReturn: (number|null), volatility: (number|null), sharpe: (number|null),
     *     treasuryBill: (number|null), expenseRatio: (number|null), usShare: (number|null), missingFees: string[]}}
     *     The figures; a figure is null when its data is missing.
     */
    function summary (returns, macro, facts, exposure, basket, years) {
        const figures = returns ? point(returns, macro, basket, years) : null;
        const fee = averageFee(facts, basket);
        return {
            annualReturn: figures ? figures.annualizedReturn : null,
            volatility: figures ? figures.volatility : null,
            sharpe: figures ? figures.sharpe : null,
            treasuryBill: figures ? figures.treasuryBill : null,
            expenseRatio: fee.expenseRatio,
            usShare: usShare(exposure, basket),
            missingFees: fee.missingFees
        };
    }

    window.ResearchBasketMath = { monthKey, addMonths, monthsBetween, point, pointForMonths, summary };
})();
