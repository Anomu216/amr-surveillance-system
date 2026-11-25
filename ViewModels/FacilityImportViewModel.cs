using AMRSurveillance.Imports.Models;
using AMRSurveillance.Models;
using Microsoft.AspNetCore.Http;

namespace AMRSurveillance.ViewModels;

public class FacilityImportViewModel
{
    public IFormFile? File { get; set; }

    public List<Facility> Records { get; set; } = new();

    public List<ImportRowError> Errors { get; set; } = new();

    public ImportSummary Summary { get; set; } = new();

    public bool IsValidated { get; set; }
}