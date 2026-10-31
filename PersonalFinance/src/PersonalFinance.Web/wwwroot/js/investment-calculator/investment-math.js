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
 *
 * Goals: solveGoal() answers how much a month reaches a target in the plan's years, or how many years the current
 * monthly contribution takes (up to 50), by projecting the plan.
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

    /**
     * Solve a savings goal (#590) against a plan. The target is compared with each year's balance, in today's money
     * when goal.todays is set (the same as inflating the target by (1 + inflation)^y).
     *
     * mode 'monthly': the whole monthly contribution (rounded up) that reaches the target by the plan's last year;
     *     'already' with the year when the starting amount alone gets there.
     * mode 'years': the first year the plan with its current monthly contribution reaches the target, looking up to
     *     50 years ahead (LIMITS.years.max); 'unreachable' with the shortfall at year 50 when it never does.
     * A target the starting amount already covers is 'already' in year 0 in either mode.
     * @param {object} inputs - The plan, as for project().
     * @param {{target: number, mode: ('monthly'|'years'), todays?: boolean}} goal - The goal.
     * @returns {{kind: ('monthly'|'years'|'already'|'unreachable'), monthly?: number, year?: number,
     *     shortfall?: number, progress: number, reachedYear: (number|null)}} The answer, plus progress (the plan's
     *     last balance over the target, 1 for no target) and reachedYear (the first year within the plan that the
     *     current plan reaches the target, or null).
     */
    function solveGoal (inputs, goal) {
        const plan = normalize(inputs);
        const settings = goal || {};
        const target = Math.max(0, Number(settings.target) || 0);
        const balances = (changes) => {
            const result = project(Object.assign({}, plan, changes));
            return (settings.todays ? result.real : result).rows;
        };
        const reaches = (row) => row.balance >= target;
        const endOf = (rows) => rows[rows.length - 1].balance;

        const current = balances({});
        const hit = current.find(reaches);
        const context = { progress: target > 0 ? endOf(current) / target : 1, reachedYear: hit ? hit.year : null };
        const answer = (fields) => Object.assign(fields, context);

        if (plan.initial >= target) {
            return answer({ kind: 'already', year: 0 });
        }

        if (settings.mode === 'years') {
            const long = balances({ years: LIMITS.years.max });
            const year = long.find(reaches);
            return year
                ? answer({ kind: 'years', year: year.year })
                : answer({ kind: 'unreachable', shortfall: target - endOf(long) });
        }

        const alone = balances({ monthly: 0 }).find(reaches);
        if (alone) {
            return answer({ kind: 'already', year: alone.year });
        }
        // The last balance rises with the monthly contribution: widen until it reaches, then halve the gap.
        const endWith = (monthly) => endOf(balances({ monthly }));
        let low = 0;
        let high = Math.max(1, target / (12 * plan.years));
        while (endWith(high) < target && high < Number.MAX_SAFE_INTEGER) {
            low = high;
            high *= 2;
        }
        for (let i = 0; i < 60 && high - low > 0.001; i++) {
            const middle = (low + high) / 2;
            if (endWith(middle) >= target) {
                high = middle;
            } else {
                low = middle;
            }
        }
        let monthly = Math.floor(high);
        if (endWith(monthly) < target) {
            monthly += 1;
        }
        return answer({ kind: 'monthly', monthly, year: plan.years });
    }

    root.InvestmentMath = Object.freeze({ LIMITS, DEFAULTS, PRESETS, normalize, project, solveGoal });
})(typeof window !== 'undefined' ? window : globalThis);
