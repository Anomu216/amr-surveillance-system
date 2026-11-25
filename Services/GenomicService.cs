
using System.Data;
using System.ComponentModel.DataAnnotations;
using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AMRSurveillance.Services;

public class GenomicsService : IGenomicsService
{
    private readonly ApplicationDbContext _context;
    private readonly IGeneReferenceSchemaService _geneSchemaService;

    public GenomicsService(
        ApplicationDbContext context,
        IGeneReferenceSchemaService geneSchemaService)
    {
        _context = context;
        _geneSchemaService = geneSchemaService;
    }

    // ============================================================
    // RETRIEVE REGISTERED GENES
    // ============================================================

    public async Task<List<string>> GetRegisteredGeneNamesAsync()
    {
        return await _context.Set<GeneReference>()
            .AsNoTracking()
            .Where(g => g.IsActive)
            .OrderBy(g => g.GeneName)
            .Select(g => g.GeneName)
            .ToListAsync();
    }

    // ============================================================
    // RETRIEVE ALL GENOMICS RECORDS
    // ============================================================

    public async Task<List<GenomicsRecordViewModel>> GetAllRecordsAsync()
    {
        var records = await _context.Genomics
            .AsNoTracking()
            .OrderBy(g => g.IsolateID)
            .ToListAsync();

        var geneNames = await GetRegisteredGeneNamesAsync();
        var geneValues = await LoadGeneValuesAsync(records, geneNames);

        return records
            .Select(record => ToViewModel(record, geneValues))
            .ToList();
    }

    // ============================================================
    // RETRIEVE ONE GENOMICS RECORD
    // ============================================================

    public async Task<GenomicsRecordViewModel?> GetRecordByIsolateIdAsync(
        string isolateId)
    {
        if (string.IsNullOrWhiteSpace(isolateId))
            return null;

        isolateId = isolateId.Trim();

        var record = await _context.Genomics
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.IsolateID == isolateId);

        if (record == null)
            return null;

        var geneNames = await GetRegisteredGeneNamesAsync();

        var geneValues = await LoadGeneValuesAsync(
            new List<Genomics> { record },
            geneNames);

        return ToViewModel(record, geneValues);
    }

    // ============================================================
    // CHECK WHETHER A GENOMICS RECORD EXISTS
    // ============================================================

    public async Task<bool> ExistsAsync(string isolateId)
    {
        if (string.IsNullOrWhiteSpace(isolateId))
            return false;

        isolateId = isolateId.Trim();

        return await _context.Genomics
            .AnyAsync(g => g.IsolateID == isolateId);
    }

    // ============================================================
    // CREATE GENOMICS RECORD
    // ============================================================

    public async Task CreateAsync(GenomicsRecordViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        NormalizeModel(model);
        ValidateModelAnnotations(model);
        ValidateIsolateId(model.IsolateID);

        var isolateId = model.IsolateID;

        var registeredGenes = await GetRegisteredGeneNamesAsync();
        ValidateGeneKeys(model, registeredGenes);

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            // Prevent duplicate Genomics records.
            if (await _context.Genomics.AnyAsync(
                    g => g.IsolateID == isolateId))
            {
                throw new InvalidOperationException(
                    $"A Genomics record already exists for isolate '{isolateId}'.");
            }

            // The isolate must already exist in Worksheets.
            await ValidateWorksheetExistsAsync(isolateId);

            // WHONETID must match Worksheets and, if supplied,
            // must also exist in Organisms.
            await ValidateWHONETIDAsync(
                isolateId,
                model.WHONETID);

            // Ensure the dynamic gene columns exist.
            await EnsureGeneColumnsAsync(registeredGenes);

            var record = new Genomics();
            CopyFixedFields(model, record);

            _context.Genomics.Add(record);
            await _context.SaveChangesAsync();

            // Save fixed fields and gene values in the same transaction.
            await SaveGeneValuesAsync(
                isolateId,
                model,
                registeredGenes);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // UPDATE GENOMICS RECORD
    // ============================================================

    public async Task UpdateAsync(
        string isolateId,
        GenomicsRecordViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrWhiteSpace(isolateId))
        {
            throw new ArgumentException(
                "An isolate ID is required.",
                nameof(isolateId));
        }

        isolateId = isolateId.Trim();

        NormalizeModel(model);
        ValidateModelAnnotations(model);
        ValidateIsolateId(model.IsolateID);

        if (!string.Equals(
                isolateId,
                model.IsolateID,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The IsolateID cannot be changed.");
        }

        var registeredGenes = await GetRegisteredGeneNamesAsync();
        ValidateGeneKeys(model, registeredGenes);

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var record = await _context.Genomics
                .FirstOrDefaultAsync(g => g.IsolateID == isolateId);

            if (record == null)
            {
                throw new KeyNotFoundException(
                    $"No Genomics record was found for isolate '{isolateId}'.");
            }

            await ValidateWorksheetExistsAsync(isolateId);

            await ValidateWHONETIDAsync(
                isolateId,
                model.WHONETID);

            await EnsureGeneColumnsAsync(registeredGenes);

            CopyFixedFields(model, record);
            await _context.SaveChangesAsync();

            await SaveGeneValuesAsync(
                isolateId,
                model,
                registeredGenes);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // DELETE GENOMICS RECORD
    // ============================================================

    public async Task DeleteAsync(string isolateId)
    {
        if (string.IsNullOrWhiteSpace(isolateId))
            return;

        isolateId = isolateId.Trim();

        var record = await _context.Genomics
            .FirstOrDefaultAsync(g => g.IsolateID == isolateId);

        if (record == null)
            return;

        _context.Genomics.Remove(record);
        await _context.SaveChangesAsync();

        // Gene columns are shared by all isolates.
        // Deleting one record must not remove those columns.
    }

    // ============================================================
    // VALIDATION
    // ============================================================

    private static void NormalizeModel(
        GenomicsRecordViewModel model)
    {
        var stringProperties = model.GetType()
            .GetProperties()
            .Where(p =>
                p.CanRead &&
                p.CanWrite &&
                p.PropertyType == typeof(string));

        foreach (var property in stringProperties)
        {
            var value = property.GetValue(model) as string;

            if (value != null)
                property.SetValue(model, value.Trim());
        }

        model.GeneValues ??=
            new Dictionary<string, bool?>(
                StringComparer.OrdinalIgnoreCase);

        model.GeneValues = new Dictionary<string, bool?>(
            model.GeneValues,
            StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateModelAnnotations(
        GenomicsRecordViewModel model)
    {
        var validationContext = new ValidationContext(model);

        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            model,
            validationContext,
            validationResults,
            validateAllProperties: true);

        if (!isValid)
        {
            throw new InvalidOperationException(
                "Genomics validation failed for isolate '" +
                model.IsolateID + "': " +
                string.Join(
                    "; ",
                    validationResults.Select(
                        result => result.ErrorMessage)));
        }
    }

    private static void ValidateIsolateId(string? isolateId)
    {
        if (string.IsNullOrWhiteSpace(isolateId))
        {
            throw new InvalidOperationException(
                "IsolateID is required.");
        }

        if (isolateId.Length > 20)
        {
            throw new InvalidOperationException(
                "IsolateID cannot exceed 20 characters.");
        }
    }

    private async Task ValidateWorksheetExistsAsync(string isolateId)
    {
        var exists = await _context.Worksheets
            .AsNoTracking()
            .AnyAsync(w => w.IsolateID == isolateId);

        if (!exists)
        {
            throw new InvalidOperationException(
                $"Isolate '{isolateId}' does not exist in Worksheets.");
        }
    }

    private async Task ValidateWHONETIDAsync(
        string isolateId,
        string? whonetId)
    {
        // Retrieve the WHONETID assigned to this isolate in Worksheets.
        var worksheetWHONETID = await _context.Set<Worksheet>()
            .AsNoTracking()
            .Where(w => w.IsolateID == isolateId)
            .Select(w => w.WHONETID)
            .FirstOrDefaultAsync();

        var worksheetExists = await _context.Set<Worksheet>()
            .AsNoTracking()
            .AnyAsync(w => w.IsolateID == isolateId);

        if (!worksheetExists)
        {
            throw new InvalidOperationException(
                $"Isolate '{isolateId}' does not exist in Worksheets.");
        }

        var normalizedSubmitted = NormalizeOptionalString(whonetId);
        var normalizedWorksheet = NormalizeOptionalString(worksheetWHONETID);

        // The submitted WHONETID must match the one for this isolate.
        if (!string.Equals(
                normalizedSubmitted,
                normalizedWorksheet,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The WHONETID for isolate '{isolateId}' must match " +
                "the WHONETID recorded in Worksheets.");
        }

        // A blank value is allowed only if the Worksheets value is blank too.
        if (normalizedSubmitted == null)
            return;

        // A supplied WHONETID must also exist in Organisms.
        var organismExists = await _context.Organisms
            .AsNoTracking()
            .AnyAsync(o => o.WHONETID == normalizedSubmitted);

        if (!organismExists)
        {
            throw new InvalidOperationException(
                $"WHONETID '{normalizedSubmitted}' does not exist in Organisms.");
        }
    }

    private static string? NormalizeOptionalString(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static void ValidateGeneKeys(
        GenomicsRecordViewModel model,
        IReadOnlyCollection<string> registeredGenes)
    {
        var registeredGeneSet = new HashSet<string>(
            registeredGenes,
            StringComparer.OrdinalIgnoreCase);

        foreach (var geneName in model.GeneValues.Keys)
        {
            if (string.IsNullOrWhiteSpace(geneName) ||
                !registeredGeneSet.Contains(geneName))
            {
                throw new InvalidOperationException(
                    $"Gene '{geneName}' is not a registered active gene.");
            }
        }
    }

    // ============================================================
    // GENE COLUMN MANAGEMENT
    // ============================================================

    private async Task EnsureGeneColumnsAsync(
        IEnumerable<string> geneNames)
    {
        foreach (var geneName in geneNames)
        {
            await _geneSchemaService.EnsureGeneColumnAsync(geneName);
        }
    }

    // ============================================================
    // LOAD GENE VALUES
    // ============================================================

    private async Task<Dictionary<string, Dictionary<string, bool?>>>
        LoadGeneValuesAsync(
            List<Genomics> records,
            List<string> geneNames)
    {
        var result =
            new Dictionary<string, Dictionary<string, bool?>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            result[record.IsolateID] =
                new Dictionary<string, bool?>(
                    StringComparer.OrdinalIgnoreCase);
        }

        if (records.Count == 0 || geneNames.Count == 0)
            return result;

        var existingGenes = new List<string>();

        foreach (var geneName in geneNames)
        {
            if (await ColumnExistsAsync(geneName))
                existingGenes.Add(geneName);
        }

        if (existingGenes.Count == 0)
            return result;

        var selectColumns = string.Join(
            ", ",
            existingGenes.Select(QuoteIdentifier));

        var sql =
            $"SELECT [IsolateID], {selectColumns} " +
            "FROM [dbo].[Genomics];";

        var connection = _context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;

        if (openedHere)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            await using var reader = await command.ExecuteReaderAsync();

            var isolateOrdinal = reader.GetOrdinal("IsolateID");

            var ordinals = existingGenes.ToDictionary(
                name => name,
                name => reader.GetOrdinal(name),
                StringComparer.OrdinalIgnoreCase);

            while (await reader.ReadAsync())
            {
                var isolateId = reader.GetString(isolateOrdinal);

                if (!result.TryGetValue(isolateId, out var values))
                    continue;

                foreach (var geneName in existingGenes)
                {
                    var ordinal = ordinals[geneName];

                    values[geneName] = reader.IsDBNull(ordinal)
                        ? null
                        : reader.GetBoolean(ordinal);
                }
            }
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }

        return result;
    }

    private async Task<bool> ColumnExistsAsync(string geneName)
    {
        var connection = _context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;

        if (openedHere)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();

            command.CommandText = """
                SELECT CASE
                    WHEN COL_LENGTH(N'dbo.Genomics', @columnName) IS NULL
                    THEN 0
                    ELSE 1
                END;
                """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@columnName";
            parameter.Value = geneName;
            command.Parameters.Add(parameter);

            return Convert.ToInt32(
                await command.ExecuteScalarAsync()) == 1;
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }
    }

    // ============================================================
    // SAVE GENE VALUES
    // ============================================================

    private async Task SaveGeneValuesAsync(
        string isolateId,
        GenomicsRecordViewModel model,
        List<string> geneNames)
    {
        var connection = _context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;

        if (openedHere)
            await connection.OpenAsync();

        try
        {
            foreach (var geneName in geneNames)
            {
                var sql =
                    $"UPDATE [dbo].[Genomics] " +
                    $"SET {QuoteIdentifier(geneName)} = @geneValue " +
                    "WHERE [IsolateID] = @isolateId;";

                await using var command = connection.CreateCommand();

                command.Transaction = _context.Database
                    .CurrentTransaction?
                    .GetDbTransaction();

                command.CommandText = sql;

                var valueParameter = command.CreateParameter();
                valueParameter.ParameterName = "@geneValue";
                valueParameter.DbType = DbType.Boolean;

                if (model.GeneValues.TryGetValue(
                        geneName,
                        out var geneValue))
                {
                    valueParameter.Value = geneValue.HasValue
                        ? geneValue.Value
                        : DBNull.Value;
                }
                else
                {
                    valueParameter.Value = DBNull.Value;
                }

                command.Parameters.Add(valueParameter);

                var isolateParameter = command.CreateParameter();
                isolateParameter.ParameterName = "@isolateId";
                isolateParameter.Value = isolateId;

                command.Parameters.Add(isolateParameter);

                var affectedRows = await command.ExecuteNonQueryAsync();

                if (affectedRows == 0)
                {
                    throw new InvalidOperationException(
                        $"Could not save gene '{geneName}' for isolate '{isolateId}'.");
                }
            }
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static string QuoteIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            name.Length > 100 ||
            name.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Invalid gene column name.",
                nameof(name));
        }

        return "[" + name.Replace("]", "]]") + "]";
    }

    private static GenomicsRecordViewModel ToViewModel(
        Genomics record,
        Dictionary<string, Dictionary<string, bool?>> geneValues)
    {
        var model = new GenomicsRecordViewModel();
        CopyFixedFields(record, model);

        if (geneValues.TryGetValue(record.IsolateID, out var values))
        {
            model.GeneValues = new Dictionary<string, bool?>(
                values,
                StringComparer.OrdinalIgnoreCase);
        }

        return model;
    }

    private static void CopyFixedFields(
        object source,
        object destination)
    {
        var destinationProperties = destination.GetType()
            .GetProperties()
            .Where(p => p.CanWrite)
            .ToDictionary(
                p => p.Name,
                StringComparer.OrdinalIgnoreCase);

        foreach (var sourceProperty in source.GetType().GetProperties())
        {
            if (!sourceProperty.CanRead)
                continue;

            if (!destinationProperties.TryGetValue(
                    sourceProperty.Name,
                    out var destinationProperty))
            {
                continue;
            }

            if (destinationProperty.PropertyType !=
                sourceProperty.PropertyType)
            {
                continue;
            }

            if (sourceProperty.Name == nameof(Genomics.Worksheet) ||
                sourceProperty.Name == nameof(Genomics.Organism) ||
                sourceProperty.Name == nameof(Genomics.Shipment))
            {
                continue;
            }

            destinationProperty.SetValue(
                destination,
                sourceProperty.GetValue(source));
        }
    }
}
