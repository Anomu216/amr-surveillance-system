using System.ComponentModel.DataAnnotations;

namespace AMRSurveillance.ViewModels;

public class FacilityViewModel
{
    [Required]
    [Display(Name = "Facility Code")]
    public string FacilityID { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Facility Name")]
    public string FacilityName { get; set; } = string.Empty;

    [Display(Name = "County")]
    public string County { get; set; } = string.Empty;

    [Display(Name = "Sub County")]
    public string SubCounty { get; set; } = string.Empty;

    [Display(Name = "Facility Level")]
    public string FacilityLevel { get; set; } = string.Empty;

    [Display(Name = "Ownership")]
    public string Ownership { get; set; } = string.Empty;
}