using AMRSurveillance.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AMRSurveillance.ViewModels;

public class AuditLogIndexViewModel
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? EntityName { get; set; }
    public string? Action { get; set; }
    public string? UserQuery { get; set; }
    public string? EntityKeyQuery { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public List<AuditLog> Items { get; set; } = [];
    public List<SelectListItem> AvailableEntities { get; set; } = [];
    public List<SelectListItem> AvailableActions { get; set; } = [];
}