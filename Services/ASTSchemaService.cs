using System.Data;
using System.Text.RegularExpressions;
using AMRSurveillance.Data;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public partial class ASTSchemaService(ApplicationDbContext context) : IASTSchemaService
{
    private readonly ApplicationDbContext _context = context;

    private static readonly Regex ValidColumnNameRegex =
        new(@"^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    private static readonly Regex ValidAntibioticCodeRegex =
        new(@"^[A-Z0-9_]+$", RegexOptions.Compiled);

    public async Task EnsureASTColumnsAsync()
    {
        var antibiotics = await _context.Antibiotics
            .AsNoTracking()
            .ToListAsync();

        foreach (var antibiotic in antibiotics)
        {
            var code = NormalizeCode(antibiotic.AntibioticCode);

            if (code == null)
                continue;

            await EnsureColumnExistsAsync(
                $"{code}_MIC",
                "nvarchar(50)");

            await EnsureColumnExistsAsync(
                $"{code}_INT",
                "nvarchar(10)");
        }
    }

    public async Task LoadASTResultsAsync(
        string isolateID,
        IList<WorksheetAntibioticViewModel> antibiotics)
    {
        if (string.IsNullOrWhiteSpace(isolateID))
        {
            throw new ArgumentException(
                "Isolate ID is required.",
                nameof(isolateID));
        }

        if (antibiotics == null || antibiotics.Count == 0)
            return;

        await EnsureASTColumnsAsync();

        var validAntibiotics = antibiotics
            .Where(a => a != null)
            .Select(a => new
            {
                Antibiotic = a,
                Code = NormalizeCode(a.AntibioticCode)
            })
            .Where(x => x.Code != null)
            .GroupBy(
                x => x.Code!,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (validAntibiotics.Count == 0)
            return;

        var columns = validAntibiotics
            .SelectMany(group => new[]
            {
                $"{group.Key}_MIC",
                $"{group.Key}_INT"
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var column in columns)
        {
            if (!IsValidColumnName(column))
            {
                throw new InvalidOperationException(
                    $"Invalid AST column name: {column}");
            }
        }

        var selectColumns = string.Join(
            ", ",
            columns.Select(QuoteIdentifier));

        var sql = $"""
            SELECT {selectColumns}
            FROM [dbo].[Worksheet]
            WHERE [IsolateID] = @IsolateID;
            """;

        var connection = _context.Database.GetDbConnection();

        var shouldCloseConnection =
            connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();

            command.CommandText = sql;

            var isolateParameter = command.CreateParameter();
            isolateParameter.ParameterName = "@IsolateID";
            isolateParameter.Value = isolateID.Trim();
            command.Parameters.Add(isolateParameter);

            await using var reader =
                await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return;

            foreach (var group in validAntibiotics)
            {
                var code = group.Key;

                var micColumn = $"{code}_MIC";
                var interpretationColumn = $"{code}_INT";

                var micOrdinal = reader.GetOrdinal(micColumn);
                var interpretationOrdinal =
                    reader.GetOrdinal(interpretationColumn);

                var mic = reader.IsDBNull(micOrdinal)
                    ? null
                    : reader.GetString(micOrdinal);

                var interpretation =
                    reader.IsDBNull(interpretationOrdinal)
                        ? null
                        : reader.GetString(interpretationOrdinal);

                foreach (var item in group)
                {
                    item.Antibiotic.MIC = mic;
                    item.Antibiotic.Interpretation = interpretation;
                }
            }
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }

    public async Task SaveASTResultsAsync(
        string isolateID,
        IEnumerable<WorksheetAntibioticViewModel> antibiotics)
    {
        if (string.IsNullOrWhiteSpace(isolateID))
        {
            throw new ArgumentException(
                "Isolate ID is required.",
                nameof(isolateID));
        }

        List<WorksheetAntibioticViewModel> results = antibiotics is not null
            ? [.. antibiotics]
            : [];

        if (results.Count == 0)
            return;

        await EnsureASTColumnsAsync();

        var connection = _context.Database.GetDbConnection();

        var shouldCloseConnection =
            connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
            await connection.OpenAsync();

        try
        {
            foreach (var antibiotic in results)
            {
                if (antibiotic == null)
                    continue;

                var code = NormalizeCode(antibiotic.AntibioticCode);

                if (code == null)
                    continue;

                var micColumn = QuoteIdentifier($"{code}_MIC");
                var interpretationColumn =
                    QuoteIdentifier($"{code}_INT");

                var sql = $"""
                    UPDATE [dbo].[Worksheet]
                    SET
                        {micColumn} = @MIC,
                        {interpretationColumn} = @Interpretation
                    WHERE [IsolateID] = @IsolateID;
                    """;

                await using var command = connection.CreateCommand();

                command.CommandText = sql;

                var micParameter = command.CreateParameter();
                micParameter.ParameterName = "@MIC";
                micParameter.Value =
                    (object?)Clean(antibiotic.MIC) ?? DBNull.Value;
                command.Parameters.Add(micParameter);

                var interpretationParameter =
                    command.CreateParameter();

                interpretationParameter.ParameterName =
                    "@Interpretation";

                interpretationParameter.Value =
                    (object?)Clean(antibiotic.Interpretation)
                    ?? DBNull.Value;

                command.Parameters.Add(interpretationParameter);

                var isolateParameter = command.CreateParameter();
                isolateParameter.ParameterName = "@IsolateID";
                isolateParameter.Value = isolateID.Trim();
                command.Parameters.Add(isolateParameter);

                await command.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }

    private async Task EnsureColumnExistsAsync(
        string columnName,
        string sqlType)
    {
        if (!IsValidColumnName(columnName))
        {
            throw new InvalidOperationException(
                $"Invalid AST column name: {columnName}");
        }

        if (sqlType is not "nvarchar(50)" and not "nvarchar(10)")
        {
            throw new InvalidOperationException(
                $"Invalid AST SQL data type: {sqlType}");
        }

        var connection = _context.Database.GetDbConnection();

        var shouldCloseConnection =
            connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();

            command.CommandText = """
                IF COL_LENGTH('dbo.Worksheet', @ColumnName) IS NULL
                BEGIN
                    DECLARE @SQL nvarchar(max);

                    SET @SQL =
                        N'ALTER TABLE [dbo].[Worksheet] ADD '
                        + QUOTENAME(@ColumnName)
                        + N' '
                        + @SqlType;

                    EXEC sp_executesql @SQL;
                END;
                """;

            var columnParameter = command.CreateParameter();
            columnParameter.ParameterName = "@ColumnName";
            columnParameter.Value = columnName;
            command.Parameters.Add(columnParameter);

            var typeParameter = command.CreateParameter();
            typeParameter.ParameterName = "@SqlType";
            typeParameter.Value = sqlType;
            command.Parameters.Add(typeParameter);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }

    private static bool IsValidColumnName(string columnName)
    {
        return ValidColumnNameRegex.IsMatch(columnName);
    }

    private static string QuoteIdentifier(string identifier)
    {
        if (!IsValidColumnName(identifier))
        {
            throw new InvalidOperationException(
                $"Invalid SQL identifier: {identifier}");
        }

        return $"[{identifier}]";
    }

    private static string? NormalizeCode(string? antibioticCode)
    {
        if (string.IsNullOrWhiteSpace(antibioticCode))
            return null;

        var code = antibioticCode.Trim().ToUpperInvariant();

        if (!ValidAntibioticCodeRegex.IsMatch(code))
        {
            throw new InvalidOperationException(
                $"Invalid Antibiotic Code: {antibioticCode}");
        }

        return code;
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}