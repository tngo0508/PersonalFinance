using PersonalFinance.ApiService.Infrastructure;

namespace PersonalFinance.Tests.TestSupport;

/// <summary>Assertions on the keyed rejections ApiService services throw (ADR 0017).</summary>
public static class ApiErrorAssert
{
    /// <summary>
    /// Asserts that <paramref name="call"/> is rejected with <paramref name="key"/>, and returns the rejection. A Feature
    /// throws <see cref="ApiErrorException"/> for every rejection, so checking only the type passes when a different
    /// guard fires first, for example "not found" masking "archived" (#350).
    /// </summary>
    public static async Task<ApiErrorException> ThrowsAsync(string key, Func<Task> call)
    {
        ApiErrorException rejection = await Assert.ThrowsAsync<ApiErrorException>(call);
        Assert.Equal(key, rejection.Key);
        return rejection;
    }
}
