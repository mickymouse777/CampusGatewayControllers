using System.ComponentModel.DataAnnotations;
namespace CampusGatewayControllers.Models;

// This model describes the JSON we accept from the caller.
// The validation attributes are checked automatically by ApiController.
public class LoginRequest
{
    // A backing field lets the Email setter trim spaces before validation.
    private string _email = "";
    // The email must be present and short enough for this sample. The pattern also
    // keeps characters that Table Storage cannot use in a RowKey out of our email keys.
    [Required, StringLength(254)]
    [RegularExpression(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$")]
    public string Email { get => _email; set => _email = value?.Trim() ?? ""; }

    // No minimum length here: a wrong password should normally produce 401.
    // We check the supplied password against the stored hash in AuthController.
    // This is a separate model because login does not need the user's full name.
    [Required, StringLength(128)]
    public string Password { get; set; } = "";
}
