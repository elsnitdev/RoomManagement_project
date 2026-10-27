using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RoomManagement.API.Contracts.Auth;
using RoomManagement.API.Contracts.Users;
using RoomManagement.Business.DTOs.Users;

namespace RoomManagement.API.Authentication;

public sealed class JwtTokenService
{
    private readonly IOptions<JwtOptions> options;
    private readonly TimeProvider clock;

    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock)
    {
        this.options = options;
        this.clock = clock;
    }

    public LoginResponse Create(UserDto user)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(settings.AccessTokenMinutes);
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: new Claim[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim("role", user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            },
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256));

        return new LoginResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expiresAt,
            User = UserResponse.FromDto(user)
        };
    }
}
