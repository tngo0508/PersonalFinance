// PROTOTYPE data. ILLUSTRATIVE SAMPLE, not N-PORT filings: approximate % of net assets at Q4 2019 and Q2 2026,
// interpolated per quarter in research-prototype.js. The real build reads SEC N-PORT data sets.
window.RP_ETF = {
    quarters: { from: [2019, 4], to: [2026, 2] },
    funds: [
        {
            ticker: 'SPY', name: 'SPDR S&P 500 ETF Trust', holdings: [503, 503],
            cats: { 'Equity, common': [99.7, 99.6], 'Short-term investment vehicles': [0.2, 0.3], 'Cash and other': [0.1, 0.1] },
            top: [
                ['NVIDIA', 0.4, 7.6], ['Microsoft', 4.5, 6.6], ['Apple', 4.6, 6.2], ['Amazon', 2.9, 4.0], ['Meta Platforms', 1.8, 2.9],
                ['Broadcom', 0.5, 2.7], ['Alphabet A', 1.5, 2.3], ['Alphabet C', 1.5, 1.9], ['Tesla', 0, 1.9], ['Berkshire Hathaway B', 1.7, 1.6],
                ['JPMorgan Chase', 1.6, 1.5], ['Eli Lilly', 0.4, 1.2], ['Visa', 1.2, 1.1], ['Mastercard', 1.0, 0.9], ['Oracle', 0.5, 0.9],
                ['Exxon Mobil', 0.9, 0.9], ['Netflix', 0.5, 0.8], ['Costco', 0.5, 0.8], ['Walmart', 0.6, 0.8], ['Johnson & Johnson', 1.5, 0.7],
                ['Home Depot', 0.9, 0.7], ['Procter & Gamble', 1.2, 0.6], ['Bank of America', 1.0, 0.6], ['AMD', 0.2, 0.6], ['Intel', 1.0, 0.2],
                ['UnitedHealth', 1.0, 0.5], ['AT&T', 1.0, 0.4], ['Verizon', 0.9, 0.4], ['Coca-Cola', 0.8, 0.5], ['Pfizer', 0.8, 0.3]
            ]
        },
        {
            ticker: 'QQQ', name: 'Invesco QQQ Trust', holdings: [103, 101],
            cats: { 'Equity, common': [99.9, 99.8], 'Cash and other': [0.1, 0.2] },
            top: [
                ['NVIDIA', 1.3, 9.4], ['Microsoft', 11.6, 8.6], ['Apple', 11.4, 7.8], ['Amazon', 8.9, 5.5], ['Broadcom', 1.7, 5.1],
                ['Meta Platforms', 4.4, 3.8], ['Alphabet A', 4.2, 2.8], ['Alphabet C', 4.8, 2.7], ['Tesla', 1.2, 3.0], ['Netflix', 1.9, 2.4],
                ['Costco', 1.4, 2.4], ['Palantir', 0, 2.0], ['AMD', 0.6, 1.6], ['Cisco', 2.7, 1.5], ['T-Mobile US', 0.6, 1.4],
                ['PepsiCo', 2.4, 1.2], ['Intuitive Surgical', 0.9, 1.2], ['Adobe', 2.1, 1.0], ['Qualcomm', 1.4, 1.0], ['Comcast', 2.7, 0.9],
                ['Intel', 3.4, 0.6], ['Amgen', 1.9, 0.9], ['Texas Instruments', 1.6, 0.8], ['Booking Holdings', 1.2, 0.9], ['Micron', 0.8, 0.9],
                ['PayPal', 1.7, 0.5], ['Starbucks', 1.1, 0.5]
            ]
        },
        {
            ticker: 'VTI', name: 'Vanguard Total Stock Market ETF', holdings: [3531, 3580],
            cats: { 'Equity, common': [99.6, 99.5], 'Short-term investment vehicles': [0.3, 0.4], 'Derivatives': [0.05, 0.05], 'Cash and other': [0.05, 0.05] },
            top: [
                ['NVIDIA', 0.3, 6.6], ['Microsoft', 3.8, 5.9], ['Apple', 3.9, 5.5], ['Amazon', 2.5, 3.5], ['Meta Platforms', 1.5, 2.5],
                ['Broadcom', 0.4, 2.3], ['Alphabet A', 1.3, 1.9], ['Alphabet C', 1.3, 1.6], ['Tesla', 0.2, 1.7], ['Berkshire Hathaway B', 1.4, 1.4],
                ['JPMorgan Chase', 1.4, 1.3], ['Eli Lilly', 0.3, 1.1], ['Visa', 1.0, 1.0], ['Mastercard', 0.8, 0.8], ['Oracle', 0.4, 0.8],
                ['Exxon Mobil', 0.8, 0.8], ['Netflix', 0.4, 0.7], ['Costco', 0.4, 0.7], ['Walmart', 0.5, 0.7], ['Johnson & Johnson', 1.3, 0.6],
                ['Home Depot', 0.8, 0.6], ['Procter & Gamble', 1.0, 0.6], ['Bank of America', 0.8, 0.5], ['AMD', 0.2, 0.5], ['Intel', 0.9, 0.2],
                ['UnitedHealth', 0.9, 0.4], ['AT&T', 0.9, 0.3]
            ]
        },
        {
            ticker: 'VT', name: 'Vanguard Total World Stock ETF', holdings: [8547, 9850],
            cats: { 'Equity, common': [98.9, 98.8], 'Equity, preferred': [0.2, 0.2], 'Short-term investment vehicles': [0.6, 0.7], 'Derivatives': [0.1, 0.1], 'Cash and other': [0.2, 0.2] },
            top: [
                ['NVIDIA', 0.2, 4.1], ['Microsoft', 2.1, 3.7], ['Apple', 2.2, 3.4], ['Amazon', 1.4, 2.2], ['Meta Platforms', 0.8, 1.5],
                ['Broadcom', 0.2, 1.4], ['Taiwan Semiconductor', 0.6, 1.3], ['Alphabet A', 0.7, 1.2], ['Alphabet C', 0.7, 1.0], ['Tesla', 0.1, 1.0],
                ['JPMorgan Chase', 0.8, 0.8], ['Berkshire Hathaway B', 0.8, 0.8], ['Eli Lilly', 0.2, 0.7], ['Tencent', 0.5, 0.6], ['Visa', 0.6, 0.6],
                ['ASML', 0.3, 0.5], ['Samsung Electronics', 0.5, 0.5], ['Exxon Mobil', 0.5, 0.5], ['Walmart', 0.3, 0.4], ['Mastercard', 0.5, 0.5],
                ['Nestle', 0.6, 0.3], ['Johnson & Johnson', 0.7, 0.4], ['Roche', 0.4, 0.3], ['Novo Nordisk', 0.2, 0.2], ['Toyota', 0.4, 0.3],
                ['Alibaba', 0.6, 0.3], ['Procter & Gamble', 0.6, 0.3]
            ]
        },
        {
            ticker: 'IWM', name: 'iShares Russell 2000 ETF', holdings: [2004, 1960],
            cats: { 'Equity, common': [98.8, 98.6], 'Short-term investment vehicles': [1.0, 1.2], 'Derivatives': [0.1, 0.1], 'Cash and other': [0.1, 0.1] },
            top: [
                ['Sample small-cap A', 0.4, 0.6], ['Sample small-cap B', 0.3, 0.5], ['Sample small-cap C', 0.3, 0.5], ['Sample small-cap D', 0.3, 0.4],
                ['Sample small-cap E', 0.3, 0.4], ['Sample small-cap F', 0.3, 0.4], ['Sample small-cap G', 0.2, 0.4], ['Sample small-cap H', 0.2, 0.3],
                ['Sample small-cap I', 0.2, 0.3], ['Sample small-cap J', 0.2, 0.3], ['Sample small-cap K', 0.2, 0.3], ['Sample small-cap L', 0.2, 0.3],
                ['Sample small-cap M', 0.2, 0.3], ['Sample small-cap N', 0.2, 0.3], ['Sample small-cap O', 0.2, 0.3], ['Sample small-cap P', 0.2, 0.3],
                ['Sample small-cap Q', 0.2, 0.3], ['Sample small-cap R', 0.2, 0.2], ['Sample small-cap S', 0.2, 0.2], ['Sample small-cap T', 0.2, 0.2],
                ['Sample small-cap U', 0.2, 0.2], ['Sample small-cap V', 0.2, 0.2], ['Sample small-cap W', 0.2, 0.2], ['Sample small-cap X', 0.2, 0.2],
                ['Sample small-cap Y', 0.2, 0.2]
            ]
        },
        {
            ticker: 'AIQ', name: 'Global X Artificial Intelligence & Technology ETF', holdings: [88, 90],
            cats: { 'Equity, common': [99.6, 99.7], 'Cash and other': [0.4, 0.3] },
            top: [
                ['NVIDIA', 2.6, 4.2], ['Broadcom', 2.2, 4.0], ['Meta Platforms', 2.9, 3.8], ['Alphabet A', 3.0, 3.6], ['Microsoft', 3.1, 3.5],
                ['Tesla', 1.4, 3.4], ['Oracle', 2.4, 3.3], ['Netflix', 2.5, 3.2], ['Amazon', 3.0, 3.1], ['Samsung Electronics', 2.8, 3.0],
                ['Tencent', 2.9, 2.9], ['Alibaba', 3.1, 2.8], ['Taiwan Semiconductor', 2.0, 2.7], ['Cisco', 3.0, 2.6], ['IBM', 3.0, 2.6],
                ['Salesforce', 2.7, 2.3], ['ServiceNow', 2.3, 2.3], ['AMD', 1.8, 2.2], ['Micron', 2.2, 2.1], ['Qualcomm', 2.6, 2.0],
                ['Shopify', 1.1, 2.0], ['Uber', 0.9, 1.9], ['SAP', 2.6, 1.9], ['Accenture', 2.7, 1.7], ['Adobe', 2.8, 1.6],
                ['Intel', 2.9, 1.0]
            ]
        }
    ]
};
