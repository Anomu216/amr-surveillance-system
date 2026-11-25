using Microsoft.AspNetCore.Http;

namespace AMRSurveillance.Imports.Models;

public class ImportViewModel<TEntity>
    where TEntity : class
{
    // =====================================================
    // UPLOADED FILE
    // =====================================================

    public IFormFile? File { get; set; }


    // =====================================================
    // VALIDATED RECORDS
    // =====================================================

    public List<TEntity> Records { get; set; } = new();


    // =====================================================
    // VALIDATION ERRORS
    // These prevent the affected rows from being imported.
    // =====================================================

    public List<ImportRowError> Errors { get; set; } = new();


    // =====================================================
    // DATA QUALITY WARNINGS
    // These do not prevent records from being imported.
    // =====================================================

    public List<ImportRowError> Warnings { get; set; } = new();


    // =====================================================
    // IMPORT SUMMARY
    // =====================================================

    public ImportSummary Summary { get; set; } = new();


    // =====================================================
    // VALIDATION STATE
    // =====================================================

    public bool IsValidated { get; set; }


    // =====================================================
    // READY TO IMPORT
    // Warnings do not affect readiness to import.
    // =====================================================

    public bool ReadyToImport =>
        IsValidated &&
        !Errors.Any() &&
        Records.Any();
}