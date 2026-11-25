
using System.ComponentModel.DataAnnotations;

namespace AMRSurveillance.ViewModels;

public class CreateUserViewModel
{
    [Required]
    [StringLength(150)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(256, MinimumLength = 3)]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Assigned roles")]
    public List<string> SelectedRoles { get; set; } = new();
}
