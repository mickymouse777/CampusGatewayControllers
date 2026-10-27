using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CampusGatewayControllers.Models;
using Microsoft.IdentityModel.Tokens;
namespace CampusGatewayControllers.Services;

// This service has one job: create the login token. Keeping it here makes the
// controller easier to follow and keeps token settings out of endpoint logic.
public class JwtTokenService(SymmetricSecurityKey signingKey, string issuer, string audience)
{
    public LoginResponse CreateToken(CampusUser user)
    {
        // Keep the token short-lived. Once it expires, sign in again to get a new one.
        var expires = DateTime.UtcNow.AddMinutes(30);
        // Claims are facts we include about the signed-in user. Anyone holding the JWT
        // can decode its contents, so these facts must not include secrets.
        var token = new JwtSecurityToken(issuer, audience,
            claims: [new Claim(ClaimTypes.Name, user.FullName),
                     new Claim(ClaimTypes.Email, user.RowKey),
                     new Claim(ClaimTypes.Role, user.PartitionKey)],
            expires: expires,
            // Sign with the same key and algorithm that Program.cs expects when validating.
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
        // Turn the token into the text the client will send in its Authorization header.
        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
