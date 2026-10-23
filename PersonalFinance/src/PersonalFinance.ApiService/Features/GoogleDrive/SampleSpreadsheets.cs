using PersonalFinance.ApiService.Infrastructure;

namespace PersonalFinance.ApiService.Features.GoogleDrive;

/// <summary>
/// Built-in sample spreadsheet content behind the preview files (ids starting with <c>sample-</c>) that are shown
/// when no Google Drive API key is configured. Always labelled as sample data, never presented as the user's own.
/// </summary>
internal static class SampleSpreadsheets
{
    /// <summary>
    /// Provides sample transactions CSV laid out like the Transactions sheet of Google's Monthly budget template.
    /// </summary>
    public static string GetSampleTransactionsCsv()
    {
        return @"Transactions

Expenses,,,,,Income
Date,Amount,Description,Category,,Date,Amount,Description,Category
9/1/2026,1500.00,September rent,Housing & Rent,,9/1/2026,3000.00,Employer direct deposit,Paycheck 1
9/2/2026,84.12,Trader Joe's,Groceries & Food,,9/15/2026,2650.00,Employer direct deposit,Paycheck 2
9/3/2026,62.40,Shell gas station,Transportation & Gas,,9/30/2026,320.00,Brokerage dividends,Investment Returns
9/5/2026,155.00,Electric & water bill,Utilities & Internet
9/7/2026,58.75,Dinner with friends,Dining & Entertainment
9/9/2026,300.00,Health insurance premium,Healthcare & Insurance
9/10/2026,1150.00,Transfer to savings,Savings & Investments
9/12/2026,212.30,Costco,Groceries & Food
9/14/2026,155.00,Internet & phone,Utilities & Internet
9/18/2026,96.50,Concert tickets,Dining & Entertainment
9/20/2026,180.60,Car service,Transportation & Gas
9/22/2026,230.00,Clothing & household,Personal & Miscellaneous
9/24/2026,388.58,Whole Foods,Groceries & Food
9/27/2026,284.75,Restaurants & takeout,Dining & Entertainment
9/29/2026,182.00,Gas & parking,Transportation & Gas";
    }

    /// <summary>
    /// Provides standard sample CSV content matching Google Sheets / Excel Monthly Budget spreadsheets.
    /// </summary>
    public static string GetSampleMonthlyBudgetSpreadsheetCsv()
    {
        return @"Monthly budget - September 2026

STARTING BALANCE,1500.00,1500.00
ENDING BALANCE,2380.00,2430.00

INCOME,Planned,Actual,Difference
Paycheck 1,3000.00,3000.00,0.00
Paycheck 2,2500.00,2650.00,150.00
Investment Returns,250.00,320.00,70.00
Total Income,5750.00,5970.00,220.00

EXPENSES,Planned,Actual,Difference
Housing & Rent,1500.00,1500.00,0.00
Groceries & Food,650.00,685.00,-35.00
Utilities & Internet,320.00,310.00,10.00
Transportation & Gas,450.00,425.00,25.00
Healthcare & Insurance,300.00,300.00,0.00
Dining & Entertainment,400.00,440.00,-40.00
Savings & Investments,1000.00,1150.00,-150.00
Personal & Miscellaneous,250.00,230.00,20.00
Total Expenses,4870.00,5040.00,-170.00";
    }
}
