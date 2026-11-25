using AMRSurveillance.Data;
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Models;
using AMRSurveillance.Services;

namespace AMRSurveillance.Imports.Services;

public class PatientImportService(ApplicationDbContext context) : IImportService<Patient>
{
    public async Task CreateRangeAsync(
        IEnumerable<Patient> entities)
    {
        var patients = entities.ToList();

        if (patients.Count == 0)
        {
            return;
        }

        foreach (var patient in patients)
        {
            PatientService.ApplyApplicabilityRules(patient);
        }

        var previousDetectChanges = context.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            context.ChangeTracker.AutoDetectChangesEnabled = false;
            await context.Patients.AddRangeAsync(patients);
            await context.SaveChangesAsync();
        }
        finally
        {
            context.ChangeTracker.AutoDetectChangesEnabled = previousDetectChanges;
        }
    }
}