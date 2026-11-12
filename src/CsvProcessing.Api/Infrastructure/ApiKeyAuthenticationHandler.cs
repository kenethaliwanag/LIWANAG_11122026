using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CsvProcessing.Api.Infrastructure;

public static class ApiKeyDefaults
{
    public const string AuthenticationScheme = "ApiKey";

    public const string HeaderName = "X-Api-Key";
}

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public IList<string> Keys { get; } = new List<string>();
}

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyDefaults.HeaderName, out var presentedValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var presentedKey = presentedValues.ToString();
        if (string.IsNullOrWhiteSpace(presentedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("The API key is empty."));
        }

        if (!ApiKeyComparer.Matches(presentedKey, Options.Keys))
        {
            return Task.FromResult(AuthenticateResult.Fail("The API key is not valid."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "api-key")],
            ApiKeyDefaults.AuthenticationScheme);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), ApiKeyDefaults.AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = ApiKeyDefaults.AuthenticationScheme;

        return Task.CompletedTask;
    }
}
