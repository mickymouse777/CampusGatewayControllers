using System.ComponentModel.DataAnnotations;
namespace CampusGatewayControllers.Models;

// This model describes the JSON we accept from the caller.
// The validation attributes are checked automatically by ApiController.
public class RegisterRequest
{
    // A backing field lets the Email setter trim spaces before validation.
    private string _email = "";
    // Trim email before validation, preserving the original endpoint's behavior.
    // The email must be present and short enough for this sample. The pattern also
    // keeps characters that Table Storage cannot use in a RowKey out of our email keys.
    [Required, StringLength(254)]
    [RegularExpression(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$")]
    public string Email { get => _email; set => _email = value?.Trim() ?? ""; }

    // Choose a password between 12 and 128 characters. We validate it here, then
    // hash it in the controller. We do not trim or change the password.
    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = "";

    // This is the friendly name returned by the profile endpoint.
    [Required, StringLength(100)]
    public string FullName { get; set; } = "";
}
