namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Exception thrown when attempting to create or rename a category with a name that already exists for that user and kind.
/// </summary>
public class DuplicateCategoryException : Exception
{
    public DuplicateCategoryException(string message) : base(message)
    {
    }
}
