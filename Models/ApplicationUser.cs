
using Microsoft.AspNetCore.Identity;

namespace AMRSurveillance.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }

    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; } = false;
}
