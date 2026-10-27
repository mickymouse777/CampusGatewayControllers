namespace CampusGatewayControllers.Models;

// Return only safe profile fields; never serialize the storage entity to clients.
// A record is a compact way to describe a response with named properties.
// ASP.NET writes these names as camelCase JSON, for example fullName.
public record UserResponse(string Email, string FullName, string Role);
// The client needs the signed token and its UTC expiry time after login.
public record LoginResponse(string AccessToken, DateTime ExpiresAtUtc);
// These values come from the current token's claims, not directly from the table.
public record CurrentUserResponse(string? FullName, string? Email, string? Role);
