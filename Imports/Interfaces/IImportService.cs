namespace AMRSurveillance.Imports.Interfaces;

public interface IImportService<TEntity>
    where TEntity : class
{
    Task CreateRangeAsync(
        IEnumerable<TEntity> entities);
}