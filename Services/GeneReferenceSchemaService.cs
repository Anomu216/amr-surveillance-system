
using AMRSurveillance.Data;
using AMRSurveillance.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AMRSurveillance.Services;

public class GeneReferenceSchemaService : IGeneReferenceSchemaService
{
    private readonly ApplicationDbContext _context;

    public GeneReferenceSchemaService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task EnsureGeneColumnAsync(string geneName)
    {
        if (string.IsNullOrWhiteSpace(geneName))
        {
            throw new ArgumentException(
                "Gene name cannot be empty.", nameof(geneName));
        }

        geneName = geneName.Trim();

        if (geneName.Length > 100 ||
            geneName.Any(char.IsControl))
        {
            throw new ArgumentException(
                "The gene name must be 100 characters or fewer and cannot contain control characters.",
                nameof(geneName));
        }

        // Quote the SQL identifier safely, including any closing bracket.
        var quotedColumnName = "[" + geneName.Replace("]", "]]") + "]";

        var connection = _context.Database.GetDbConnection();
        var openedHere = connection.State !=
                         System.Data.ConnectionState.Open;

        if (openedHere)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();

            command.Transaction =
                _context.Database.CurrentTransaction?.GetDbTransaction();

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

            var exists = Convert.ToInt32(
                await command.ExecuteScalarAsync()) == 1;

            if (exists)
            {
                return;
            }

            await using var alterCommand = connection.CreateCommand();

            alterCommand.Transaction =
                _context.Database.CurrentTransaction?.GetDbTransaction();

            alterCommand.CommandText =
                $"ALTER TABLE [dbo].[Genomics] ADD {quotedColumnName} BIT NULL;";

            await alterCommand.ExecuteNonQueryAsync();
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }
}
