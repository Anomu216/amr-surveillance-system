
using System.ComponentModel.DataAnnotations;

namespace AMRSurveillance.ViewModels;

public class AdminPasswordResetViewModel
{
    [Required]
    [Display(Name = "User account")]
    public string UserId { get; set; } = string.Empty;
}
