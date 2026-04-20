using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace JOrder.Common.Helpers;

internal sealed class SecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var authenticationSchemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (authenticationSchemes.Any(authScheme => authScheme.Name == "Bearer"))
        {
            var securitySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                ["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    In = ParameterLocation.Header,
                    BearerFormat = "Json Web Token"
                },
                ["OAuth2"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        AuthorizationCode = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri("/oauth2/authorize", UriKind.Relative),
                            TokenUrl = new Uri("/oauth2/token", UriKind.Relative),
                            Scopes = new Dictionary<string, string>
                            {
                                ["openid"] = "OpenID Connect",
                                ["profile"] = "User profile information",
                                ["email"] = "User email address",
                                ["roles"] = "User roles and permissions",
                                ["offline_access"] = "Offline access (refresh token)"
                            }
                        }
                    },
                    Description = "OAuth2 Authorization Code flow for interactive login in documentation tools"
                }
            };
            
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes = securitySchemes;
        }
    }
}