using Microsoft.AspNetCore.Http;
using Shouldly;
using SoundboardLite.Api;

namespace SoundboardLite.Api.Tests;

public sealed class TokenAuthTests
{
    private const string Token = "secret-token-value";

    private static (DefaultHttpContext Context, bool NextCalled) Invoke(string? authorizationHeader)
    {
        var context = new DefaultHttpContext();
        if (authorizationHeader is not null)
        {
            context.Request.Headers.Authorization = authorizationHeader;
        }

        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var middleware = TokenAuth.Middleware(next, Token);

        middleware(context).GetAwaiter().GetResult();
        return (context, nextCalled);
    }

    [Fact]
    public void Middleware_CorrectBearerToken_CallsNext()
    {
        var (_, nextCalled) = Invoke($"Bearer {Token}");

        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public void Middleware_MissingAuthorizationHeader_Returns401AndDoesNotCallNext()
    {
        var (context, nextCalled) = Invoke(null);

        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void Middleware_WrongToken_Returns401()
    {
        var (context, nextCalled) = Invoke("Bearer wrong-token");

        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void Middleware_NonBearerScheme_Returns401()
    {
        var (context, nextCalled) = Invoke($"Basic {Token}");

        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void Middleware_TokenWithTrailingGarbage_Returns401()
    {
        var (context, nextCalled) = Invoke($"Bearer {Token}extra");

        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void Middleware_EmptyBearerToken_Returns401()
    {
        var (context, nextCalled) = Invoke("Bearer ");

        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }
}
