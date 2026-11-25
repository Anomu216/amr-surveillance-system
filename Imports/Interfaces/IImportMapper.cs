using ClosedXML.Excel;

namespace AMRSurveillance.Imports.Interfaces;

public interface IImportMapper<TEntity>
    where TEntity : class
{
    TEntity Map(
        IXLWorksheet worksheet,
        IXLRow row,
        IReadOnlyDictionary<string, int> headerMap);
}