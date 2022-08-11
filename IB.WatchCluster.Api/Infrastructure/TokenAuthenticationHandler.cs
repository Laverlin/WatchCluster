using System.Security.Claims;
using System.Security.Principal;
using System.Text.Encodings.Web;
using System.Text.Json;

using IB.WatchCluster.Abstract.Entity;
using IB.WatchCluster.Api.Entity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;


namespace IB.WatchCluster.Api.Infrastructure
{
    /// <summary>
    /// Authentication request handler
    /// Looking for a known token to authenticate request, no token is Ok, wrong token - frobidden
    /// </summary>
    // ReSharper disable once ClassNeverInstantiated.Global
    public class TokenAuthenticationHandler : AuthenticationHandler<TokenAuthOptions>
    {
        public TokenAuthenticationHandler(
            IOptionsMonitor<TokenAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
            : base(options, logger, encoder, clock)
        {}

        /// <summary>
        /// Looking for a known token to authenticate request, no token is Ok, wrong token - forbidden
        /// </summary>
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Query.ContainsKey(Options.ApiTokenName))
                return Task.FromResult(AuthenticateResult.NoResult());

            if (Options.ApiToken != Request.Query[Options.ApiTokenName])
                return Task.FromResult(AuthenticateResult.Fail("Invalid auth token."));

            // Create authenticated user
            //
            var identities = new List<ClaimsIdentity> {new GenericIdentity("watch-face"), new (Options.Scheme)};
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identities), Options.Scheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties authProperties)
        {
            Logger.LogInformation("Token forbidden, request {@request}", Request.QueryString);
            
            await ForbidAsync(authProperties);
            await Response.WriteAsync(
                JsonSerializer.Serialize(
                    new ErrorResponse { StatusCode = 403, Description = "Unauthorized access" }));
        }
    }
}