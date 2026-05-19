using System.ComponentModel.DataAnnotations;

namespace API.Settings;

public class StripeSettings
{
    [Required]
    public string SecretKey { get; init; } = string.Empty;

    [Required]
    public string WhSecret { get; init; } = string.Empty;
}
