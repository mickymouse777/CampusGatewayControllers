using System.Security.Claims;
using Azure;
using Azure.Data.Tables;
using CampusGatewayControllers.Models;
using CampusGatewayControllers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CampusGatewayControllers.Controllers;

// Welcome to the endpoint code. ApiController turns JSON into request models
// and checks their validation attributes before an action runs.
[ApiController]
// This shared prefix combines with each action's route, such as register or login.
[Route("api/auth")]
// This is a primary constructor: ASP.NET injects the services listed below.
// ControllerBase gives us helpers such as Ok(), Unauthorized() and Conflict().
public class AuthController(
    TableClient users,
    IPasswordHasher<CampusUser> hasher,
    JwtTokenService tokens) : ControllerBase
{
    // ApiController validates the request's DataAnnotations before this runs.
    // POST /api/auth/register: create an account. You do not need a token yet.
    // The ProducesResponseType attributes describe possible responses in Swagger.
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        // The request model describes what the caller sends. CampusUser describes what
        // we store. Keeping them separate stops callers supplying storage-only fields.
        var user = new CampusUser
        {
            PartitionKey = "Student", // Public registration never chooses a privileged role.
            // Store email consistently so Student@Campus.ac.za and student@campus.ac.za
            // refer to the same account. The request model already trims outside spaces.
            RowKey = request.Email.ToLowerInvariant(),
            FullName = request.FullName.Trim(),
            CreatedDate = DateTimeOffset.UtcNow
        };
        // We save a salted password hash, never the actual password.
        // Let PasswordHasher handle the hashing rather than writing your own algorithm.
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        try
        {
            // Atomic (all or nothing) insert: duplicate emails return 409, including simultaneous requests.
            // await lets the request wait for storage without blocking a thread.
            // The cancellation token lets the operation be cancelled if the request is cancelled.
            await users.AddEntityAsync(user, cancellationToken);
        }
        // A duplicate key means this Student email already exists. Return a useful 409
        // instead of overwriting the existing account or reporting a server failure.
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            return Conflict(new { error = "This email is already registered." });
        }
        // Return a safe response model. Notice that PasswordHash is not included.
        return StatusCode(StatusCodes.Status201Created,
            new UserResponse(user.RowKey, user.FullName, user.PartitionKey));
    }

    // POST /api/auth/login: check an existing account and issue a token.
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        // Look up both keys: the Student partition and the normalized email.
        // This small sample only creates and signs in Students.
        var found = await users.GetEntityIfExistsAsync<CampusUser>(
            "Student", request.Email.ToLowerInvariant(), cancellationToken: cancellationToken);
        // Unknown account and wrong password both return 401. We do not tell the caller
        // which part of the credentials was wrong.
        if (!found.HasValue) return Unauthorized();
        // We checked HasValue above, so we can now use the stored profile.
        // The ! tells the compiler we have already checked that a value exists.
        var user = found.Value!;
        // Verify against the saved hash; hashing the input again and comparing strings
        // would not work reliably because password hashing uses a salt.
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed) return Unauthorized();
        // The service signs the JWT. The client keeps it and sends it on later requests;
        // logging in does not automatically authorize future requests without the token.
        return Ok(tokens.CreateToken(user));
    }

    // GET /api/auth/me: a small endpoint to prove your token works.
    // Authorize requires a valid token; it does not check for a particular role here.
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    // The authentication middleware has already built User from the validated claims.
    // This action reads those claims; it does not query Table Storage again.
    public IActionResult Me() => Ok(new CurrentUserResponse(
        User.Identity?.Name,
        User.FindFirst(ClaimTypes.Email)?.Value,
        User.FindFirst(ClaimTypes.Role)?.Value));
}
