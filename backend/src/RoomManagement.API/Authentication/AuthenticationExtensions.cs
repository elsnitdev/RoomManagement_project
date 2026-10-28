using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace RoomManagement.API.Authentication;

public static class AuthenticationExtensions
{
  public static IServiceCollection AddRoomManagementAuthentication(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddOptions<JwtOptions>()
        .Bind(configuration.GetSection(JwtOptions.SectionName))
        .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer is required.")
        .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience is required.")
        .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey) && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
            "Configure Jwt:SigningKey with at least 32 bytes in appsettings.Local.json or an environment variable.")
        .Validate(o => o.AccessTokenMinutes is >= 1 and <= 60, "Jwt:AccessTokenMinutes must be between 1 and 60.")
        .ValidateOnStart();

    services.AddSingleton(TimeProvider.System);
    services.AddScoped<JwtTokenService>();
    services.AddScoped<CurrentUserJwtEvents>();
    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
    services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
        {
          var jwt = jwtOptions.Value;
          bearer.MapInboundClaims = false;
          bearer.IncludeErrorDetails = false;
          bearer.EventsType = typeof(CurrentUserJwtEvents);
          bearer.TokenValidationParameters = new TokenValidationParameters
          {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidAlgorithms = new List<string> { SecurityAlgorithms.HmacSha256 },
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role"
          };
        });
    services.AddAuthorization();
    return services;
  }
}
