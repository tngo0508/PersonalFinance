using System.Reflection;

namespace PersonalFinance.Tests;

/// <summary>
/// Creates <see cref="ApiProxy{TContract}"/> fakes.
/// </summary>
public static class ApiProxy
{
    /// <summary>Creates a fake where every method throws <see cref="HttpRequestException"/>.</summary>
    public static (TContract Api, ApiProxy<TContract> Proxy) Create<TContract>() where TContract : class
    {
        TContract api = DispatchProxy.Create<TContract, ApiProxy<TContract>>();
        return (api, (ApiProxy<TContract>)(object)api);
    }
}

/// <summary>
/// Builds a Refit contract fake whose methods fail like an unreachable ApiService, unless a method is given a result.
/// Saves writing a fake class per contract when a test only cares about one or two calls.
/// </summary>
public class ApiProxy<TContract> : DispatchProxy where TContract : class
{
    private readonly Dictionary<string, Func<object?>> _results = new();
    private readonly Dictionary<string, Exception> _failures = new();

    /// <summary>Names of the contract methods called, in order.</summary>
    public List<string> Calls { get; } = new();

    /// <summary>The arguments of each call in <see cref="Calls"/>, in the same order.</summary>
    public List<object?[]> Arguments { get; } = new();

    /// <summary>Makes <paramref name="methodName"/> succeed with <paramref name="result"/>.</summary>
    public ApiProxy<TContract> Returns(string methodName, object? result)
    {
        _results[methodName] = () => result;
        return this;
    }

    /// <summary>Makes <paramref name="methodName"/> fail with <paramref name="failure"/> instead.</summary>
    public ApiProxy<TContract> Throws(string methodName, Exception failure)
    {
        _failures[methodName] = failure;
        return this;
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        Calls.Add(targetMethod.Name);
        Arguments.Add(args ?? []);
        Type returnType = targetMethod.ReturnType;

        if (!_results.TryGetValue(targetMethod.Name, out Func<object?>? result))
        {
            Exception failure = _failures.GetValueOrDefault(targetMethod.Name) ?? new HttpRequestException("ApiService unreachable");
            if (returnType == typeof(Task))
            {
                return Task.FromException(failure);
            }

            Type resultType = returnType.GetGenericArguments()[0];
            return typeof(Task).GetMethod(nameof(Task.FromException), 1, new[] { typeof(Exception) })!
                .MakeGenericMethod(resultType)
                .Invoke(null, new object[] { failure });
        }

        if (returnType == typeof(Task))
        {
            return Task.CompletedTask;
        }

        return typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(returnType.GetGenericArguments()[0])
            .Invoke(null, new[] { result() });
    }
}
