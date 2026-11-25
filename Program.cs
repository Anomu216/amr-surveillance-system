using AMRSurveillance.Data;
using AMRSurveillance.Data.Interceptors;
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Imports.Mappers;
using AMRSurveillance.Imports.Services;
using AMRSurveillance.Imports.Templates;
using AMRSurveillance.Imports.Validators;
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// MVC
// =====================================================

builder.Services.AddScoped<ActiveUserFilter>();

builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.Filters.Add(new AuthorizeFilter(policy));
    options.Filters.AddService<ActiveUserFilter>();
});

// =====================================================
// Http Context & Session
// =====================================================

builder.Services.AddHttpContextAccessor();

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// =====================================================
// Database Registration & Audit Interceptor
// =====================================================

builder.Services.AddScoped<AuditSaveChangesInterceptor>();

builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"));

    options.AddInterceptors(
        serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
});

// =====================================================
// ASP.NET Core Identity
// =====================================================

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Password policy
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        // Account security
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User account requirements
        options.User.RequireUniqueEmail = true;

        // Require email confirmation before sign-in.
        // We will need to configure email delivery before
        // enabling this in production.
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "AMRSurveillance.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;

    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
});

// =====================================================
// Application Services
// =====================================================

builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAntibioticClassService, AntibioticClassService>();
builder.Services.AddScoped<IAntibioticService, AntibioticService>();
builder.Services.AddScoped<IOrganismService, OrganismService>();
builder.Services.AddScoped<IIntrinsicResistanceService, IntrinsicResistanceService>();
builder.Services.AddScoped<IFacilityService, FacilityService>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IWorksheetService, WorksheetService>();
builder.Services.AddScoped<IASTConfigurationService, ASTConfigurationService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IASTSchemaService, ASTSchemaService>();
builder.Services.AddScoped<IGeneReferenceSchemaService, GeneReferenceSchemaService>();
builder.Services.AddScoped<IGenomicsService, GenomicsService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IShipmentService, ShipmentService>();
builder.Services.AddScoped<IIdentifierValidationService, IdentifierValidationService>();
builder.Services.AddScoped<IDataRequestService, DataRequestService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// =====================================================
// Import Framework
// =====================================================

builder.Services.AddScoped<IExcelImportService, ExcelImportService>();
builder.Services.AddScoped<IImportTemplateService, ImportTemplateService>();

builder.Services.AddScoped<IImportMapper<Facility>, FacilityImportMapper>();
builder.Services.AddScoped<IEntityImportValidator<Facility>, FacilityImportValidator>();
builder.Services.AddScoped<IImportService<Facility>, FacilityImportService>();
builder.Services.AddScoped(typeof(IImportWorkflow<>), typeof(ImportWorkflowService<>));
builder.Services.AddScoped<IImportLookupService, ImportLookupService>();

builder.Services.AddScoped<IImportMapper<Patient>, PatientImportMapper>();
builder.Services.AddScoped<IEntityImportValidator<Patient>, PatientImportValidator>();
builder.Services.AddScoped<IImportService<Patient>, PatientImportService>();

builder.Services.AddScoped<IWorksheetImportService, WorksheetImportService>();
builder.Services.AddScoped<WorksheetImportMapper>();
builder.Services.AddScoped<WorksheetImportValidator>();
builder.Services.AddScoped<WorksheetTemplateGenerator>();

// GeneReference
builder.Services.AddScoped<IImportMapper<GeneReference>, GeneReferenceImportMapper>();
builder.Services.AddScoped<IEntityImportValidator<GeneReference>, GeneReferenceImportValidator>();
builder.Services.AddScoped<IImportService<GeneReference>, GeneReferenceImportService>();

// Genomics
builder.Services.AddScoped<IImportMapper<GenomicsRecordViewModel>, GenomicsImportMapper>();
builder.Services.AddScoped<IEntityImportValidator<GenomicsRecordViewModel>, GenomicsImportValidator>();
builder.Services.AddScoped<IImportService<GenomicsRecordViewModel>, GenomicsImportService>();
builder.Services.AddScoped<IImportWorkflow<GenomicsRecordViewModel>, ImportWorkflowService<GenomicsRecordViewModel>>();

builder.Services.AddSingleton<TemporaryPasswordStore>();

// =====================================================
// Build Application
// =====================================================

var app = builder.Build();

// =====================================================
// Seed Identity Roles
// =====================================================

await IdentitySeeder.SeedRolesAsync(app.Services);

// Schema service
using (var scope = app.Services.CreateScope())
{
    var astSchemaService = scope.ServiceProvider
        .GetRequiredService<IASTSchemaService>();

    await astSchemaService.EnsureASTColumnsAsync();
}

// =====================================================
// Configure HTTP Pipeline
// =====================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.MapStaticAssets();

app.UseRouting();

app.UseSession();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();