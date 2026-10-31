/**
 * Investment Calculator math (#587): the projection the page draws, with no DOM access, so the node tests
 * (tools/investment-math.test.js) load it as is. The page script reads the inputs, calls project() and hands the
 * result to the charts; nothing here formats or draws.
 *
 * The model: the balance compounds monthly at the assumed yearly return less the yearly fund fee (both divided by
 * twelve), and each month's contribution is added at the month's end. The monthly contribution rises by the yearly
 * increase at the start of each year after the first. The same plan without the fee gives the fee's cost.
 *
 * Today's money: every amount of year y is divided by (1 + inflation)^y, the prices of that year. A year's growth in
 * today's money is the change in the deflated balance less that year's deflated deposits, so it includes what
 * inflation took and the deflated rows still add up to the deflated totals.
 */
(function (root) {
    'use strict';

    /** The bounds of every non-money input; money inputs are bounded per currency by the view (MoneyBounds). */
    const LIMITS = Object.freeze({
        years: Object.freeze({ min: 1, max: 50 }),
        rate: Object.freeze({ min: 0, max: 15 }),
        increase: Object.freeze({ min: 0, max: 10 }),
        fee: Object.freeze({ min: 0, max: 3 }),
        inflation: Object.freeze({ min: 0, max: 10 })
    });

    /** The starting plan, in US dollars; the view scales the amounts to the Reporting Currency. */
    const DEFAULTS = Object.freeze({ initial: 10000, monthly: 500, rate: 7, years: 20, increase: 0, fee: 0.1, inflation: 2.5 });

    /** The assumed-return presets: the Money Priorities rates. */
    const PRESETS = Object.freeze([
        Object.freeze({ key: 'conservative', rate: 4 }),
        Object.freeze({ key: 'base', rate: 7 }),
        Object.freeze({ key: 'aggressive', rate: 10 })
    ]);

    /**
     * A finite number within [min, max]; anything else reads as min.
     * @param {*} value - The raw value.
     * @param {number} min - The lower bound.
     * @param {number} max - The upper bound.
     * @returns {number} The clamped number.
     */
    function clamp (value, min, max) {
        const number = Number(value);
        if (!Number.isFinite(number)) {
            return min;
        }
        return Math.min(max, Math.max(min, number));
    }

    /**
     * The inputs made safe to project: amounts at least 0, years a whole number of 1-50, rates within their bounds.
     * @param {object} inputs - initial, monthly, rate, years, increase, fee, inflation (rates in percent).
     * @returns {{initial: number, monthly: number, rate: number, years: number, increase: number, fee: number,
     *     inflation: number}} The normalized inputs.
     */
    function normalize (inputs) {
        const source = inputs || {};
        return {
            initial: clamp(source.initial, 0, Number.MAX_SAFE_INTEGER),
            monthly: clamp(source.monthly, 0, Number.MAX_SAFE_INTEGER),
            rate: clamp(source.rate, LIMITS.rate.min, LIMITS.rate.max),
            years: Math.round(clamp(source.years, LIMITS.years.min, LIMITS.years.max)),
            increase: clamp(source.increase, LIMITS.increase.min, LIMITS.increase.max),
            fee: clamp(source.fee, LIMITS.fee.min, LIMITS.fee.max),
            inflation: clamp(source.inflation, LIMITS.inflation.min, LIMITS.inflation.max)
        };
    }

    /**
     * The projection in today's money: each year's amounts at that year's prices.
     * @param {Array<object>} rows - The nominal rows, year 0 first.
     * @param {number} inflation - The yearly inflation, in percent.
     * @returns {{rows: Array<{year: number, contributedYear: number, growthYear: number, contributed: number,
     *     balance: number, noFee: number}>, final: number, contributed: number, growth: number, noFee: number,
     *     feeCost: number}} The same fields as the nominal projection, deflated.
     */
    function deflate (rows, inflation) {
        let contributed = 0;
        let previous = 0;
        const real = rows.map((row) => {
            const factor = Math.pow(1 + inflation / 100, row.year);
            const balance = row.balance / factor;
            const contributedYear = row.contributedYear / factor;
            contributed += contributedYear;
            const growthYear = row.year === 0 ? 0 : balance - previous - contributedYear;
            previous = balance;
            return { year: row.year, contributedYear, growthYear, contributed, balance, noFee: row.noFee / factor };
        });
        const last = real[real.length - 1];
        return {
            rows: real,
            final: last.balance,
            contributed,
            growth: last.balance - contributed,
            noFee: last.noFee,
            feeCost: last.noFee - last.balance
        };
    }

    /**
     * Project a plan year by year.
     * @param {object} inputs - initial, monthly, rate (assumed yearly return, %), years, increase (yearly
     *     contribution increase, %), fee (yearly fund fee, %), inflation (yearly, %). Out-of-range values are
     *     clamped (normalize).
     * @returns {{rows: Array<{year: number, contributedYear: number, growthYear: number, contributed: number,
     *     balance: number, noFee: number}>, final: number, contributed: number, growth: number, noFee: number,
     *     feeCost: number, milestoneYear: (number|null), real: object}} rows: year 0 (today: the initial amount) then one per year,
     *     with what was put in and what grew that year, the running total put in, the balance and the balance had
     *     there been no fee. final, contributed, growth: the last year's balance, total put in and their difference.
     *     feeCost: what the fee took. milestoneYear: the first year growth exceeds the total put in, or null.
     *     real: rows, final, contributed, growth, noFee and feeCost in today's money (deflate).
     */
    function project (inputs) {
        const plan = normalize(inputs);
        const netMonthly = (plan.rate - plan.fee) / 100 / 12;
        const grossMonthly = plan.rate / 100 / 12;
        let balance = plan.initial;
        let noFee = plan.initial;
        let contributed = plan.initial;
        let milestoneYear = null;
        const rows = [{ year: 0, contributedYear: plan.initial, growthYear: 0, contributed, balance, noFee }];

        for (let year = 1; year <= plan.years; year++) {
            const deposit = plan.monthly * Math.pow(1 + plan.increase / 100, year - 1);
            const startBalance = balance;
            for (let month = 0; month < 12; month++) {
                balance = balance * (1 + netMonthly) + deposit;
                noFee = noFee * (1 + grossMonthly) + deposit;
            }
            const contributedYear = deposit * 12;
            contributed += contributedYear;
            rows.push({
                year,
                contributedYear,
                growthYear: balance - startBalance - contributedYear,
                contributed,
                balance,
                noFee
            });
            if (milestoneYear === null && contributed > 0 && balance - contributed > contributed) {
                milestoneYear = year;
            }
        }

        return {
            rows,
            final: balance,
            contributed,
            growth: balance - contributed,
            noFee,
            feeCost: noFee - balance,
            milestoneYear,
            real: deflate(rows, plan.inflation)
        };
    }

    root.InvestmentMath = Object.freeze({ LIMITS, DEFAULTS, PRESETS, normalize, project });
})(typeof window !== 'undefined' ? window : globalThis);
