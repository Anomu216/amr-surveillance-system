using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AMRSurveillance.ViewModels;

public class IntrinsicResistanceViewModel
{
    // =============================================
    // Organism
    // =============================================

    [Required]
    [Display(Name = "Organism")]
    public string WHONETID { get; set; } = string.Empty;


    // =============================================
    // Antibiotic
    // =============================================

    [Required]
    [Display(Name = "Antibiotic")]
    public string AntibioticCode { get; set; } = string.Empty;


    // =============================================
    // Display Information
    // =============================================

    // Human-readable antibiotic name.
    // This is for display only.
    public string? AntibioticName { get; set; }


    // =============================================
    // Lookups
    // =============================================

    public IEnumerable<SelectListItem> Organisms { get; set; }
        = Enumerable.Empty<SelectListItem>();

    public IEnumerable<SelectListItem> Antibiotics { get; set; }
        = Enumerable.Empty<SelectListItem>();
}