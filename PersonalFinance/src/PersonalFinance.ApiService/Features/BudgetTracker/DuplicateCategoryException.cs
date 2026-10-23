using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;

namespace PersonalFinance.ApiService.Features.BudgetTracker;

/// <summary>
/// Exception thrown when attempting to create or rename a category with a name that already exists for that user and kind.
/// </summary>
public class DuplicateCategoryException : Exception
{
    public DuplicateCategoryException(string message) : base(message)
    {
    }
}
