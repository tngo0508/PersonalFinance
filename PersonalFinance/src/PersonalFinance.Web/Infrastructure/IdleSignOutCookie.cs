using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace PersonalFinance.Web.Infrastructure;

/// <summary>
/// The server half of Idle Sign-out: the sign-in cookie expires <see cref="IdleSignOutOptions.TimeoutMinutes"/> after
/// it was last renewed. Sliding expiration renews it only once half its life has passed, so the browser's check-in
/// renews it outright; otherwise the server could end a session up to half the timeout before the browser's countdown.
/// </summary>
internal static class IdleSignOutCookie
{
    /// <summary>
    /// Binds <see cref="IdleSignOutOptions"/> and sets the Identity sign-in cookie's lifetime from it.
    /// </summary>
    public static IServiceCollection AddIdleSignOut(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IdleSignOutOptions>()
            .Bind(configuration.GetSection(IdleSignOutOptions.SectionName))
            .Validate(options => options.IsValid(),
                "IdleSignOut needs TimeoutMinutes > 0 and 0 < WarningSeconds < TimeoutMinutes * 60.")
            .ValidateOnStart();

        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<IdleSignOutOptions>>((cookie, idle) =>
            {
                cookie.ExpireTimeSpan = idle.Value.Timeout;
                cookie.SlidingExpiration = true;
                RenewOnCheckIn(cookie.Events);
            });

        return services;
    }

    /// <summary>
    /// After the existing principal validation (Identity's security stamp check), renews the cookie when the request is
    /// the browser's check-in and the principal is still valid.
    /// </summary>
    public static void RenewOnCheckIn(CookieAuthenticationEvents events)
    {
        Func<CookieValidatePrincipalContext, Task> validate = events.OnValidatePrincipal;
        events.OnValidatePrincipal = async context =>
        {
            await validate(context);
            if (context.Principal is not null && IsCheckIn(context.Request))
            {
                context.ShouldRenew = true;
            }
        };
    }

    private static bool IsCheckIn(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) &&
        request.Path.Equals(IdleSignOutOptions.CheckInPath, StringComparison.OrdinalIgnoreCase);
}
