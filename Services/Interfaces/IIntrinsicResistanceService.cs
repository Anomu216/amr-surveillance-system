using AMRSurveillance.Models;

namespace AMRSurveillance.Services.Interfaces;

public interface IIntrinsicResistanceService
{
    Task<List<IntrinsicResistance>> GetAllAsync();

    Task<List<IntrinsicResistance>> SearchAsync(
        string? whonetID,
        string? antibioticCode,
        string? search);

    Task<IntrinsicResistance?> GetByIdAsync(
        string whonetID,
        string antibioticCode);

    Task<bool> ExistsAsync(
        string whonetID,
        string antibioticCode);

    Task<bool> OrganismExistsAsync(
        string whonetID);

    Task<bool> AntibioticExistsAsync(
        string antibioticCode);

    Task CreateAsync(
        IntrinsicResistance intrinsicResistance);

    Task DeleteAsync(
        string whonetID,
        string antibioticCode);
}