using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace PersonalFinance.Tests.TestSupport;

/// <summary>
/// In-memory TempData for Web controller tests whose actions set or read TempData.
/// </summary>
public class TestTempDataProvider : ITempDataProvider
{
    private readonly Dictionary<string, object> _data = new();

    public IDictionary<string, object> LoadTempData(HttpContext context) => _data;

    public void SaveTempData(HttpContext context, IDictionary<string, object> values)
    {
        _data.Clear();
        foreach (KeyValuePair<string, object> kvp in values)
        {
            _data[kvp.Key] = kvp.Value;
        }
    }
}
