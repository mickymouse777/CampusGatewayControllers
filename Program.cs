using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CampusGatewayControllers.Models;
using CampusGatewayControllers.Services;
using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Start here: this file starts up all the servicees. The endpoint logic belongs in AuthController.
// The builder reads configuration and collects the services our app will need.
var builder = WebApplication.CreateBuilder(args);

// 1. Read configuration. This keeps secrets out of source code.
// Think of configuration as settings supplied from outside the code.
// User secrets let you run locally without putting passwords or storage keys in Git.
var connection = builder.Configuration["Storage:ConnectionString"]
    ?? throw new InvalidOperationException("Set Storage:ConnectionString using dotnet user-secrets.");
var key = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Set Jwt:Key using dotnet user-secrets.");
if (Encoding.UTF8.GetByteCount(key) < 32)
    throw new InvalidOperationException("Jwt:Key must contain at least 32 bytes.");
// Issuer means who issued this token; audience means which application it is for.
// These values must match the ones used when JwtTokenService creates the token.
const string issuer = "CampusGateway";
const string audience = "CampusStudents";
// This key signs tokens so the API can detect changes to them.
// Signing is not encryption: never put passwords or secrets inside a JWT.
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

// 2. Connect to the Users table and prepare the password hasher.
var users = new TableClient(connection, "Users");
// These registrations are dependency injection. When a controller asks for one
// of these types, ASP.NET supplies the object instead of the controller creating it.
// Singleton means the app reuses the same service instance across requests.
builder.Services.AddSingleton(users);
builder.Services.AddSingleton<IPasswordHasher<CampusUser>, PasswordHasher<CampusUser>>();
builder.Services.AddSingleton(new JwtTokenService(signingKey, issuer, audience));
// This enables controller support, including request binding and validation.
builder.Services.AddControllers();

// 3. Tell ASP.NET how to validate an incoming JWT.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep the claim names exactly as we create them in JwtTokenService.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // Check who issued the token, who it is for, its signature and its expiry.
            // A token with the wrong values will not authenticate the request.
            ValidateIssuer = true, ValidIssuer = issuer,
            ValidateAudience = true, ValidAudience = audience,
            ValidateIssuerSigningKey = true, IssuerSigningKey = signingKey,
            ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
            // Tell ASP.NET which claims represent the user's name and role.
            NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role
        };
    });
// Authentication asks 'who are you?'; authorization asks 'may you use this endpoint?'. Ensure Authentication happens first please.
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
// Swagger gives you a browser-based way to try the API without building a frontend.
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Campus Gateway — Controllers", Version = "v1" });
    // This makes the Authorize button accept a JWT. Paste the token only; Swagger
    // adds the Bearer prefix. The controller attributes still decide which routes need it.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference
          { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});

var app = builder.Build();
try
{
    // Make sure the Users table exists before accepting requests. This contacts storage
    // now, so Azurite or your Azure Storage account must be reachable at startup.
    await users.CreateIfNotExistsAsync();
}
catch (RequestFailedException ex)
{
    throw new InvalidOperationException(
        "Cannot connect to Table Storage. If using UseDevelopmentStorage=true, start Azurite with its Table service on port 10002. Otherwise check the Azure connection string and network access.", ex);
}
// Unexpected request failures get a problem response. Expected cases such as a
// duplicate email are handled directly in the controller.
app.UseExceptionHandler();
// Swagger is available in Development. Do not assume it will be exposed on a server.
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
// Order matters: read and validate the token before checking endpoint permissions.
app.UseAuthentication();
app.UseAuthorization();

// Connect HTTP routes to controller actions, then start listening for requests.
app.MapControllers();
app.Run();
