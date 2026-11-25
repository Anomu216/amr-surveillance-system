using AMRSurveillance.Data;
using AMRSurveillance.Security;
using AMRSurveillance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Controllers.ReferenceData;

[Authorize(Roles = AppRoles.SystemAdministrator + "," + AppRoles.Analyst + "," + AppRoles.GenomicsStaff)]
public class ReferenceDataController(ApplicationDbContext context) : Controller
{
    public async Task<IActionResult> Index()
    {
        var model = new ReferenceDataDashboardViewModel
        {
            AntibioticClassCount =
                await context.AntibioticClasses.CountAsync(),

            AntibioticCount =
                await context.Antibiotics.CountAsync(),

            ASTCardCount =
                await context.ASTCards.CountAsync(),

            OrganismCount =
                await context.Organisms.CountAsync(),

            IntrinsicResistanceCount =
                await context.IntrinsicResistances.CountAsync()
        };

        return View(model);
    }
}