namespace PersonalFinance.Shared.Constants;

/// <summary>
/// Size limits for To-dos, shared by the database schema, the ApiService validator and the Web page.
/// </summary>
public static class TodoLimits
{
    /// <summary>
    /// Maximum length of a To-do title, after trimming.
    /// </summary>
    public const int TitleMaxLength = 200;
}
