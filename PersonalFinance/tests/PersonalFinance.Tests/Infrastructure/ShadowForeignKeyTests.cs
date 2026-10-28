using Microsoft.EntityFrameworkCore.Metadata;
using PersonalFinance.Data;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// Every foreign key is a property the code sets. A shadow FK means EF Core discovered a relationship the configuration
/// never named (an unpaired navigation collection), and adds a column no row ever fills (issue #309).
/// Read from the model the SQL Server migrations are generated from.
/// </summary>
public class ShadowForeignKeyTests
{
    [Fact]
    public void NoEntity_HasAShadowForeignKeyProperty()
    {
        IModel model = AzureSqlTranslation.CreateContext().Model;

        List<string> shadowForeignKeys = model.GetEntityTypes()
            .SelectMany(type => type.GetForeignKeys())
            .SelectMany(foreignKey => foreignKey.Properties)
            .Where(property => property.IsShadowProperty())
            .Select(property => $"{property.DeclaringType.ClrType.Name}.{property.Name}")
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(shadowForeignKeys);
    }
}
