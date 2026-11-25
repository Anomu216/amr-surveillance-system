using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AMRSurveillance.ViewModels;

public class UserListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
    public bool MustChangePassword { get; set; }
    public List<string> Roles { get; set; } = [];
}

public class ManageUsersIndexViewModel
{
    public string? Search { get; set; }
    public string? RoleFilter { get; set; }
    public string? StatusFilter { get; set; }
    public string CurrentAdminId { get; set; } = string.Empty;
    public List<UserListItemViewModel> Users { get; set; } = [];
    public List<SelectListItem> AvailableRoles { get; set; } = [];
}

public class EditUserRolesViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsCurrentAdmin { get; set; }

    [Display(Name = "Assigned Roles")]
    public List<string> SelectedRoles { get; set; } = [];

    public List<SelectListItem> AvailableRoles { get; set; } = [];
}