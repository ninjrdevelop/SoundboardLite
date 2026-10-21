using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace SoundboardLite.Api;

/// <summary>
/// Requires a matching <c>Authorization: Bearer {token}</c> header on every request. Defense in depth
/// on top of the loopback-only binding — protects against other local processes/users on the same
/// machine, not against anything remote (Kestrel never listens on a non-loopback address here).
/// </summary>
internal static class TokenAuth
{
    public static RequestDelegate Middleware(RequestDelegate next, string token)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(token);
        const string prefix = "Bearer ";

        return async context =>
        {
            var header = context.Request.Headers.Authorization.ToString();
            var authorized = false;

            if (header.StartsWith(prefix, StringComparison.Ordinal))
            {
                var providedBytes = Encoding.UTF8.GetBytes(header[prefix.Length..]);
                authorized = providedBytes.Length == tokenBytes.Length
                    && CryptographicOperations.FixedTimeEquals(providedBytes, tokenBytes);
            }

            if (!authorized)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next(context);
        };
    }
}
