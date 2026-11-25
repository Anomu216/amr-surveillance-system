using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AMRSurveillance.ViewModels;

public class ASTCardListItemViewModel
{
    public string ASTCard { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public int AntibioticCount { get; set; }

    public List<string> MappedOrganisms { get; set; } = [];
}

public class ASTCardEditViewModel
{
    [Required(ErrorMessage = "AST Card code is required.")]
    [StringLength(20)]
    [Display(Name = "AST Card Code")]
    public string ASTCard { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Mapped Organisms (WHONET IDs)")]
    public List<string> SelectedWHONETIDs { get; set; } = [];

    public List<SelectListItem> AvailableOrganisms { get; set; } = [];

    public List<ASTCardPanelItemViewModel> Antibiotics { get; set; } = [];

    public List<SelectListItem> AvailableAntibioticsToAdd { get; set; } = [];
}

public class ASTCardPanelItemViewModel
{
    public string AntibioticCode { get; set; } = string.Empty;

    public string AntibioticName { get; set; } = string.Empty;

    public string AntibioticClassName { get; set; } = string.Empty;

    [Range(1, 999)]
    public int DisplayOrder { get; set; }

    public bool Remove { get; set; }
}