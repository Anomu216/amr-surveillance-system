using AMRSurveillance.Imports.Models;
using ClosedXML.Excel;

namespace AMRSurveillance.Imports.Interfaces;

public interface IEntityImportValidator<TEntity>
    where TEntity : class
{
    Task<ImportResult<TEntity>> ValidateAsync(
        IXLWorksheet worksheet);
}