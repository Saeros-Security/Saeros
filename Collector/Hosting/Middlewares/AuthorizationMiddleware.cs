using System.Net;
using System.Text.RegularExpressions;
using Collector.Databases.Abstractions.Stores.Authentication;

namespace Collector.Hosting.Middlewares;

internal sealed class AuthorizationMiddleware(RequestDelegate next, IAuthenticationStore authenticationStore)
{
    private static readonly Regex BearerRegex = new("Bearer (.*)", RegexOptions.Compiled);
    private const string ApiPath = "/api";
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(ApiPath))
        {
            await next(context);
            return;
        }
        
        if (context.Request.Path.StartsWithSegments(new PathString(Shared.Constants.Routes.HardwareId)) ||
            context.Request.Path.StartsWithSegments(new PathString(Shared.Constants.Routes.Login)) ||
            context.Request.Path.StartsWithSegments(new PathString(Shared.Constants.Routes.Users)) ||
            context.Request.Path.StartsWithSegments(new PathString(Shared.Constants.Routes.CreateUser)) ||
            context.Request.Path.StartsWithSegments(new PathString(Shared.Constants.Routes.ChangeUserPassword)) ||
            context.Request.Path.StartsWithSegments(new PathString(Shared.Constants.Routes.AnyDomain)) ||
            context.Request.Path.StartsWithSegments(new PathString(Shared.Constants.Routes.AnyLicense)) ||
            context.Request.Path.StartsWithSegments(new PathString(Shared.Constants.Routes.License)) ||
            context.Request.Path.StartsWithSegments(new PathString(Shared.Constants.Routes.UpdateLicense)) ||
            context.Request.Path.StartsWithSegments(new PathString(Shared.Constants.Routes.ValidateLicense)))
        {
            await next(context);
            return;
        }
        
        if (context.Request.Headers.TryGetValue(Shared.Constants.Headers.Authorization, out var authorizationValue))
        {
            var headerValue = authorizationValue.SingleOrDefault();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                var match = BearerRegex.Match(headerValue);
                if (match.Success)
                {
                    var tokens = await authenticationStore.GetAuthorizationValuesAsync(context.RequestAborted);
                    if (tokens.Contains(match.Groups[1].Value))
                    {
                        await next(context);
                        return;
                    }
                }
            }
        }

        ReturnUnauthorizedError(context);
    }

    private static void ReturnUnauthorizedError(HttpContext context)
    {
        context.Response.Clear();
        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
    }
}