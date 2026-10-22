using PersonalFinance.ApiService.Services;

namespace PersonalFinance.Tests.Todos;

/// <summary>
/// The open-To-do list query must run on Azure SQL, not only on the InMemory provider the endpoint tests use.
/// </summary>
public class TodoQueryTranslationTests
{
    [Fact]
    public void OpenTodosQuery_TranslatesToAzureSql_WithUndatedTodosLast()
    {
        using var db = AzureSqlTranslation.CreateContext();

        var sql = AzureSqlTranslation.AssertTranslates(new TodoService(db).OpenTodosQuery("todo-user"));

        Assert.Contains("[t].[CompletedAt] IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("[t].[IsDeleted]", sql, StringComparison.Ordinal);
        Assert.Contains("WHEN [t].[DueDate] IS NULL THEN CAST(1 AS bit)", sql, StringComparison.Ordinal);
        Assert.Contains("[t].[DueDate], [t].[CreatedAt], [t].[Id]", sql, StringComparison.Ordinal);
    }
}
