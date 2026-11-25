using System.Security.Claims;
using System.Text.Json;
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AMRSurveillance.Data;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IHttpContextAccessor? httpContextAccessor = null)
    : IdentityDbContext<ApplicationUser>(options)
{
    private static readonly JsonSerializerOptions s_auditJsonOptions = new()
    {
        WriteIndented = false
    };

    private static readonly HashSet<string> s_sensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(ApplicationUser.PasswordHash),
        nameof(ApplicationUser.SecurityStamp),
        nameof(ApplicationUser.ConcurrencyStamp)
    };

    // ============================================================
    // DbSets
    // ============================================================

    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Worksheet> Worksheets => Set<Worksheet>();
    public DbSet<Shipment> Shipments => Set<Shipment>();

    public DbSet<Organism> Organisms => Set<Organism>();
    public DbSet<Antibiotic> Antibiotics => Set<Antibiotic>();
    public DbSet<AntibioticClass> AntibioticClasses => Set<AntibioticClass>();
    public DbSet<IntrinsicResistance> IntrinsicResistances => Set<IntrinsicResistance>();
    public DbSet<ASTCardDefinition> ASTCards => Set<ASTCardDefinition>();
    public DbSet<ASTCardOrganism> ASTCardOrganisms => Set<ASTCardOrganism>();
    public DbSet<ASTCardAntibiotic> ASTCardAntibiotics => Set<ASTCardAntibiotic>();
    public DbSet<GeneReference> GeneReferences => Set<GeneReference>();
    public DbSet<Genomics> Genomics => Set<Genomics>();

    public DbSet<DataRequest> DataRequests => Set<DataRequest>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();


    // ============================================================
    // Automatic Audit Log Capture
    // ============================================================

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var pendingAuditEntries = PrepareAuditEntries();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);

        if (pendingAuditEntries.Count > 0)
        {
            FinalizeAndSaveAuditEntries(pendingAuditEntries);
        }

        return result;
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var pendingAuditEntries = PrepareAuditEntries();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        if (pendingAuditEntries.Count > 0)
        {
            await FinalizeAndSaveAuditEntriesAsync(pendingAuditEntries, cancellationToken);
        }

        return result;
    }

    private List<PendingAuditEntry> PrepareAuditEntries()
    {
        ChangeTracker.DetectChanges();

        var httpContext = httpContextAccessor?.HttpContext;
        var user = httpContext?.User;

        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = user?.Identity?.Name ?? "System";
        var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();
        var timestampEat = EastAfricaTime.Now;

        List<PendingAuditEntry> entries = [];

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog
                or Notification
                or IdentityUserToken<string>
                or IdentityUserLogin<string>)
            {
                continue;
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var entityName = entry.Metadata.ClrType.Name;
            var action = entry.State switch
            {
                EntityState.Added => "Create",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => entry.State.ToString()
            };

            Dictionary<string, object?> oldValues = [];
            Dictionary<string, object?> newValues = [];
            List<PropertyEntry> temporaryProperties = [];

            foreach (var property in entry.Properties)
            {
                var propertyName = property.Metadata.Name;

                if (s_sensitiveProperties.Contains(propertyName))
                {
                    continue;
                }

                if (property.IsTemporary)
                {
                    temporaryProperties.Add(property);
                    continue;
                }

                switch (entry.State)
                {
                    case EntityState.Added:
                        newValues[propertyName] = property.CurrentValue;
                        break;

                    case EntityState.Deleted:
                        oldValues[propertyName] = property.OriginalValue;
                        break;

                    case EntityState.Modified when property.IsModified:
                        if (!Equals(property.OriginalValue, property.CurrentValue))
                        {
                            oldValues[propertyName] = property.OriginalValue;
                            newValues[propertyName] = property.CurrentValue;
                        }
                        break;
                }
            }

            // Skip no-op updates where no tracked non-sensitive field changed
            if (entry.State == EntityState.Modified && oldValues.Count == 0 && newValues.Count == 0)
            {
                continue;
            }

            entries.Add(new PendingAuditEntry(
                Entry: entry,
                AuditLog: new AuditLog
                {
                    Action = action,
                    EntityName = entityName,
                    EntityKey = BuildEntityKey(entry),
                    UserId = userId,
                    UserName = userName,
                    IpAddress = ipAddress,
                    TimestampEat = timestampEat
                },
                OldValues: oldValues,
                NewValues: newValues,
                TemporaryProperties: temporaryProperties));
        }

        return entries;
    }

    private void FinalizeAndSaveAuditEntries(List<PendingAuditEntry> pendingEntries)
    {
        foreach (var item in pendingEntries)
        {
            PopulatePostSaveValues(item);
            AuditLogs.Add(item.AuditLog);
        }

        base.SaveChanges(acceptAllChangesOnSuccess: true);
    }

    private async Task FinalizeAndSaveAuditEntriesAsync(
        List<PendingAuditEntry> pendingEntries,
        CancellationToken cancellationToken)
    {
        foreach (var item in pendingEntries)
        {
            PopulatePostSaveValues(item);
            AuditLogs.Add(item.AuditLog);
        }

        await base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }

    private static void PopulatePostSaveValues(PendingAuditEntry item)
    {
        foreach (var tempProp in item.TemporaryProperties)
        {
            var propName = tempProp.Metadata.Name;
            if (!s_sensitiveProperties.Contains(propName))
            {
                item.NewValues[propName] = tempProp.CurrentValue;
            }
        }

        item.AuditLog.EntityKey ??= BuildEntityKey(item.Entry);
        item.AuditLog.Summary = $"{item.AuditLog.Action} {item.AuditLog.EntityName}" +
            (string.IsNullOrWhiteSpace(item.AuditLog.EntityKey) ? string.Empty : $" ({item.AuditLog.EntityKey})");

        if (item.OldValues.Count > 0)
        {
            item.AuditLog.OldValuesJson = JsonSerializer.Serialize(item.OldValues, s_auditJsonOptions);
        }

        if (item.NewValues.Count > 0)
        {
            item.AuditLog.NewValuesJson = JsonSerializer.Serialize(item.NewValues, s_auditJsonOptions);
        }
    }

    private static string? BuildEntityKey(EntityEntry entry)
    {
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null)
        {
            return null;
        }

        List<string> keyParts = [];

        foreach (var keyProperty in primaryKey.Properties)
        {
            var propEntry = entry.Property(keyProperty.Name);
            if (propEntry.IsTemporary)
            {
                return null;
            }

            var value = entry.State == EntityState.Deleted
                ? propEntry.OriginalValue
                : propEntry.CurrentValue;

            if (value is not null)
            {
                keyParts.Add(value.ToString() ?? string.Empty);
            }
        }

        return keyParts.Count == 0 ? null : string.Join(" | ", keyParts);
    }

    private sealed record PendingAuditEntry(
        EntityEntry Entry,
        AuditLog AuditLog,
        Dictionary<string, object?> OldValues,
        Dictionary<string, object?> NewValues,
        List<PropertyEntry> TemporaryProperties);


    // ============================================================
    // Model Configuration
    // ============================================================

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureFacility(modelBuilder);
        ConfigurePatient(modelBuilder);
        ConfigureOrganism(modelBuilder);
        ConfigureAntibioticClass(modelBuilder);
        ConfigureAntibiotic(modelBuilder);
        ConfigureIntrinsicResistance(modelBuilder);
        ConfigureASTCardDefinitions(modelBuilder);
        ConfigureWorksheet(modelBuilder);
        ConfigureShipment(modelBuilder);
        ConfigureGeneReference(modelBuilder);
        ConfigureGenomics(modelBuilder);
        ConfigureAuditLog(modelBuilder);
    }


    // ============================================================
    // AuditLog
    // ============================================================

    private static void ConfigureAuditLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLog");

            entity.HasKey(e => e.AuditLogID);

            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.EntityName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.EntityKey)
                .HasMaxLength(150);

            entity.Property(e => e.Summary)
                .HasMaxLength(500);

            entity.Property(e => e.UserId)
                .HasMaxLength(450);

            entity.Property(e => e.UserName)
                .HasMaxLength(256);

            entity.Property(e => e.IpAddress)
                .HasMaxLength(64);

            entity.Property(e => e.TimestampEat)
                .IsRequired();

            entity.HasIndex(e => e.TimestampEat);
            entity.HasIndex(e => e.EntityName);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.EntityKey);
        });
    }


    // ============================================================
    // Facility
    // ============================================================

    private static void ConfigureFacility(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Facility>(entity =>
        {
            entity.ToTable("Facility");

            entity.HasKey(e => e.FacilityID);

            entity.Property(e => e.FacilityID)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.FacilityName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.County)
                .HasMaxLength(100);

            entity.Property(e => e.SubCounty)
                .HasMaxLength(100);

            entity.Property(e => e.FacilityLevel)
                .HasMaxLength(50);

            entity.Property(e => e.Ownership)
                .HasMaxLength(50);

            entity.HasIndex(e => e.FacilityName);
        });
    }


    // ============================================================
    // Patient
    // ============================================================

    private static void ConfigurePatient(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Patient>(entity =>
        {
            entity.ToTable("Patient");

            entity.HasKey(e => e.PatientID);

            entity.Property(e => e.PatientID)
                .IsRequired();

            entity.Property(e => e.AcceptReject)
                .HasMaxLength(20);

            entity.Property(e => e.FacilityID)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Sex)
                .HasMaxLength(10);

            entity.Property(e => e.ResidenceVillage)
                .HasMaxLength(100);

            entity.Property(e => e.ResidenceCounty)
                .HasMaxLength(100);

            entity.Property(e => e.ResidenceSubCounty)
                .HasMaxLength(100);

            entity.Property(e => e.ResidenceDescription)
                .HasMaxLength(100);

            entity.Property(e => e.MILCIV)
                .HasMaxLength(20);

            entity.Property(e => e.OccupationCategory)
                .HasMaxLength(100);


            // ----------------------------------------------------
            // Risk Factors
            // ----------------------------------------------------

            entity.Property(e => e.ChronicIllness)
                .HasMaxLength(20);

            entity.Property(e => e.ChronicSkinCondition)
                .HasMaxLength(20);

            entity.Property(e => e.BodyPiercings)
                .HasMaxLength(20);

            entity.Property(e => e.AntacidUse)
                .HasMaxLength(20);

            entity.Property(e => e.Immunosuppressed)
                .HasMaxLength(20);

            entity.Property(e => e.HouseholdMemberInfection)
                .HasMaxLength(20);

            entity.Property(e => e.HouseholdHospitalWorker)
                .HasMaxLength(20);

            entity.Property(e => e.OutdoorInjury)
                .HasMaxLength(20);

            entity.Property(e => e.Pregnant)
                .HasMaxLength(20);

            entity.Property(e => e.PreviousInfection)
                .HasMaxLength(20);

            entity.Property(e => e.PriorHospitalAdmission)
                .HasMaxLength(20);

            entity.Property(e => e.LongTermFacility)
                .HasMaxLength(20);

            entity.Property(e => e.InjectionDrugUse)
                .HasMaxLength(20);

            entity.Property(e => e.NonInjectionDrugUse)
                .HasMaxLength(20);

            entity.Property(e => e.Homeless)
                .HasMaxLength(20);

            entity.Property(e => e.Imprisoned)
                .HasMaxLength(20);

            entity.Property(e => e.NurseryChildcareBoarding)
                .HasMaxLength(20);

            entity.Property(e => e.ContactSports)
                .HasMaxLength(20);

            entity.Property(e => e.TravelOutsideCounty)
                .HasMaxLength(20);


            // ----------------------------------------------------
            // Clinical Presentation
            // ----------------------------------------------------

            entity.Property(e => e.ClinicalPresentation)
                .HasMaxLength(200);

            entity.Property(e => e.SkinInfectionSymptoms)
                .HasMaxLength(500);

            entity.Property(e => e.PatientType)
                .HasMaxLength(30);

            entity.Property(e => e.HospitalLocation)
                .HasMaxLength(100);

            entity.Property(e => e.HospitalWard)
                .HasMaxLength(100);

            entity.Property(e => e.ICUAdmission)
                .HasMaxLength(20);

            entity.Property(e => e.WardAdmission)
                .HasMaxLength(20);


            // ----------------------------------------------------
            // Clinical Assessment
            // ----------------------------------------------------

            entity.Property(e => e.Sedated)
                .HasMaxLength(20);

            entity.Property(e => e.InvasiveDevice)
                .HasMaxLength(20);

            entity.Property(e => e.InfectionPresentAtAdmission)
                .HasMaxLength(20);


            // ----------------------------------------------------
            // Vital Signs
            // ----------------------------------------------------

            entity.Property(e => e.WeightKg)
                .HasPrecision(5, 2);

            entity.Property(e => e.TemperatureC)
                .HasPrecision(4, 1);

            entity.Property(e => e.OxygenSaturation)
                .HasPrecision(5, 2);


            // ----------------------------------------------------
            // Antibiotic History
            // ----------------------------------------------------

            entity.Property(e => e.CurrentAntibioticUse)
                .HasMaxLength(20);

            entity.Property(e => e.SurgeryForInfection)
                .HasMaxLength(20);

            entity.Property(e => e.HAICAI)
                .HasMaxLength(20);


            // ----------------------------------------------------
            // Infection
            // ----------------------------------------------------

            entity.Property(e => e.InfectionSource)
                .HasMaxLength(100);

            entity.Property(e => e.InfectionSite)
                .HasMaxLength(100);


            // ----------------------------------------------------
            // Consent
            // ----------------------------------------------------

            entity.Property(e => e.ConsentSample)
                .HasMaxLength(20);


            // ----------------------------------------------------
            // Facility Relationship
            // ----------------------------------------------------

            entity.HasOne(e => e.Facility)
                .WithMany(f => f.Patients)
                .HasForeignKey(e => e.FacilityID)
                .OnDelete(DeleteBehavior.Restrict);


            // ----------------------------------------------------
            // Patient → Worksheet
            // ----------------------------------------------------

            entity.HasMany(e => e.Worksheets)
                .WithOne(w => w.Patient)
                .HasForeignKey(w => w.PatientID)
                .OnDelete(DeleteBehavior.Restrict);


            // ----------------------------------------------------
            // Indexes
            // ----------------------------------------------------

            entity.HasIndex(e => e.FacilityID);
            entity.HasIndex(e => e.EnrollmentDate);
            entity.HasIndex(e => e.Sex);
            entity.HasIndex(e => e.PatientType);
        });
    }


    // ============================================================
    // Organism
    // ============================================================

    private static void ConfigureOrganism(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organism>(entity =>
        {
            entity.ToTable("Organism");

            entity.HasKey(e => e.WHONETID);

            entity.Property(e => e.WHONETID)
                .HasMaxLength(100)
                .IsRequired();


            // ----------------------------------------------------
            // Organism → Worksheet
            // ----------------------------------------------------

            entity.HasMany(e => e.Worksheets)
                .WithOne(w => w.Organism)
                .HasForeignKey(w => w.WHONETID)
                .OnDelete(DeleteBehavior.Restrict);


            // ----------------------------------------------------
            // Organism → Intrinsic Resistance
            // ----------------------------------------------------

            entity.HasMany(e => e.IntrinsicResistances)
                .WithOne(ir => ir.Organism)
                .HasForeignKey(ir => ir.WHONETID)
                .OnDelete(DeleteBehavior.Cascade);


            // ----------------------------------------------------
            // Organism → ASTCardOrganism
            // ----------------------------------------------------

            entity.HasMany(e => e.ASTCardOrganisms)
                .WithOne(co => co.Organism)
                .HasForeignKey(co => co.WHONETID)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }


    // ============================================================
    // Antibiotic Class
    // ============================================================

    private static void ConfigureAntibioticClass(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AntibioticClass>(entity =>
        {
            entity.ToTable("AntibioticClass");

            entity.HasKey(e => e.AntibioticClassName);

            entity.Property(e => e.AntibioticClassName)
                .HasMaxLength(100)
                .IsRequired();
        });
    }


    // ============================================================
    // Antibiotic
    // ============================================================

    private static void ConfigureAntibiotic(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Antibiotic>(entity =>
        {
            entity.ToTable("Antibiotic");


            // ----------------------------------------------------
            // Primary Key
            // ----------------------------------------------------

            entity.HasKey(e => e.AntibioticCode);

            entity.Property(e => e.AntibioticCode)
                .HasMaxLength(20)
                .IsRequired();


            // ----------------------------------------------------
            // Antibiotic Name
            // ----------------------------------------------------

            entity.Property(e => e.AntibioticName)
                .HasMaxLength(200)
                .IsRequired();


            // ----------------------------------------------------
            // Antibiotic Class
            // ----------------------------------------------------

            entity.Property(e => e.AntibioticClassName)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasOne(e => e.AntibioticClass)
                .WithMany(c => c.Antibiotics)
                .HasForeignKey(e => e.AntibioticClassName)
                .OnDelete(DeleteBehavior.Restrict);


            // ----------------------------------------------------
            // AST Card
            // ----------------------------------------------------

            entity.Property(e => e.ASTCard)
                .HasMaxLength(500)
                .IsRequired();


            // ----------------------------------------------------
            // AST Configuration
            // ----------------------------------------------------

            entity.Property(e => e.MICEnabled)
                .IsRequired();

            entity.Property(e => e.InterpretationEnabled)
                .IsRequired();

            entity.Property(e => e.Active)
                .IsRequired();

            entity.Property(e => e.IncludeInMonthlyReport)
                .IsRequired()
                .HasDefaultValue(false);


            // ----------------------------------------------------
            // Antibiotic → Intrinsic Resistance
            // ----------------------------------------------------

            entity.HasMany(e => e.IntrinsicResistances)
                .WithOne(ir => ir.Antibiotic)
                .HasForeignKey(ir => ir.AntibioticCode)
                .OnDelete(DeleteBehavior.Cascade);


            // ----------------------------------------------------
            // Indexes
            // ----------------------------------------------------

            entity.HasIndex(e => e.AntibioticName);
            entity.HasIndex(e => e.AntibioticClassName);
            entity.HasIndex(e => e.Active);
        });
    }


    // ============================================================
    // Intrinsic Resistance
    // ============================================================

    private static void ConfigureIntrinsicResistance(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IntrinsicResistance>(entity =>
        {
            entity.ToTable("IntrinsicResistance");


            // ----------------------------------------------------
            // Composite Primary Key
            // ----------------------------------------------------

            entity.HasKey(e => new
            {
                e.WHONETID,
                e.AntibioticCode
            });


            entity.Property(e => e.WHONETID)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.AntibioticCode)
                .HasMaxLength(20)
                .IsRequired();


            // ----------------------------------------------------
            // Organism Relationship
            // ----------------------------------------------------

            entity.HasOne(e => e.Organism)
                .WithMany(o => o.IntrinsicResistances)
                .HasForeignKey(e => e.WHONETID)
                .OnDelete(DeleteBehavior.Cascade);


            // ----------------------------------------------------
            // Antibiotic Relationship
            // ----------------------------------------------------

            entity.HasOne(e => e.Antibiotic)
                .WithMany(a => a.IntrinsicResistances)
                .HasForeignKey(e => e.AntibioticCode)
                .OnDelete(DeleteBehavior.Cascade);


            // ----------------------------------------------------
            // Index
            // ----------------------------------------------------

            entity.HasIndex(e => e.AntibioticCode);
        });
    }


    // ============================================================
    // AST Card Definitions, Organism Mappings, and Panel Order
    // ============================================================

    private static void ConfigureASTCardDefinitions(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ASTCardDefinition>(entity =>
        {
            entity.ToTable("ASTCards");

            entity.HasKey(e => e.ASTCard);

            entity.Property(e => e.ASTCard)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasMaxLength(150);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.HasMany(e => e.ASTCardOrganisms)
                .WithOne(co => co.Card)
                .HasForeignKey(co => co.ASTCard)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.ASTCardAntibiotics)
                .WithOne(ca => ca.Card)
                .HasForeignKey(ca => ca.ASTCard)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ASTCardOrganism>(entity =>
        {
            entity.ToTable("ASTCardOrganisms");

            entity.HasKey(e => new
            {
                e.ASTCard,
                e.WHONETID
            });

            entity.Property(e => e.ASTCard)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.WHONETID)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasOne(e => e.Card)
                .WithMany(c => c.ASTCardOrganisms)
                .HasForeignKey(e => e.ASTCard)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organism)
                .WithMany(o => o.ASTCardOrganisms)
                .HasForeignKey(e => e.WHONETID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.WHONETID);
        });

        modelBuilder.Entity<ASTCardAntibiotic>(entity =>
        {
            entity.ToTable("ASTCardAntibiotics");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.ASTCard)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.AntibioticCode)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.DisplayOrder)
                .IsRequired();

            entity.HasOne(e => e.Card)
                .WithMany(c => c.ASTCardAntibiotics)
                .HasForeignKey(e => e.ASTCard)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Antibiotic>()
                .WithMany()
                .HasForeignKey(e => e.AntibioticCode)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ASTCard, e.AntibioticCode })
                .IsUnique();

            entity.HasIndex(e => new { e.ASTCard, e.DisplayOrder });
        });
    }


    // ============================================================
    // Worksheet
    // ============================================================

    private static void ConfigureWorksheet(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Worksheet>(entity =>
        {
            entity.ToTable("Worksheet");


            // ----------------------------------------------------
            // Primary Key
            // ----------------------------------------------------
            // IsolateID is the ONLY primary key.
            // There is no WorksheetID.
            // ----------------------------------------------------

            entity.HasKey(e => e.IsolateID);

            entity.Property(e => e.IsolateID)
                .HasMaxLength(20)
                .IsRequired();


            // ----------------------------------------------------
            // Patient
            // ----------------------------------------------------

            entity.Property(e => e.PatientID)
                .HasMaxLength(20)
                .IsRequired();


            // ----------------------------------------------------
            // Culture
            // ----------------------------------------------------

            entity.Property(e => e.CultureResult)
                .HasMaxLength(30)
                .IsRequired();


            // ----------------------------------------------------
            // Identification
            // ----------------------------------------------------

            entity.Property(e => e.AcceptedID)
                .HasMaxLength(100);

            entity.Property(e => e.WHONETID)
                .HasMaxLength(100);


            // ----------------------------------------------------
            // Laboratory Identification
            // ----------------------------------------------------

            entity.Property(e => e.GramReaction)
                .HasMaxLength(20);

            entity.Property(e => e.Morphology)
                .HasMaxLength(50);

            entity.Property(e => e.Hemolysis)
                .HasMaxLength(20);

            entity.Property(e => e.Catalase)
                .HasMaxLength(20);

            entity.Property(e => e.Oxidase)
                .HasMaxLength(20);

            entity.Property(e => e.Coagulase)
                .HasMaxLength(20);

            entity.Property(e => e.LactoseFermentation)
                .HasMaxLength(20);

            entity.Property(e => e.Comments)
                .HasMaxLength(500);


            // ----------------------------------------------------
            // AST Information
            // ----------------------------------------------------

            entity.Property(e => e.ASTCard)
                .HasMaxLength(500);

            entity.Property(e => e.ASTComments)
                .HasMaxLength(500);


            // ----------------------------------------------------
            // Manual Laboratory Findings
            // ----------------------------------------------------

            entity.Property(e => e.ESBL)
                .HasMaxLength(10);

            entity.Property(e => e.CefoxitinScreen)
                .HasMaxLength(10);

            entity.Property(e => e.InducibleClindamycinResistance)
                .HasMaxLength(10);


            // ----------------------------------------------------
            // Calculated Findings
            // ----------------------------------------------------
            // These are calculated by application logic.
            // They are not manually entered by the user.
            // ----------------------------------------------------

            entity.Property(e => e.MDR)
                .IsRequired(false);

            entity.Property(e => e.Carbapenemase)
                .IsRequired(false);

            entity.Property(e => e.MRSA)
                .IsRequired(false);


            // ----------------------------------------------------
            // Manuscript / Publication Tracking
            // ----------------------------------------------------

            entity.Property(e => e.ManuscriptID)
                .HasMaxLength(100);


            // ----------------------------------------------------
            // Patient Relationship
            // ----------------------------------------------------

            entity.HasOne(e => e.Patient)
                .WithMany(p => p.Worksheets)
                .HasForeignKey(e => e.PatientID)
                .OnDelete(DeleteBehavior.Restrict);


            // ----------------------------------------------------
            // Organism Relationship
            // ----------------------------------------------------

            entity.HasOne(e => e.Organism)
                .WithMany(o => o.Worksheets)
                .HasForeignKey(e => e.WHONETID)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);


            // ----------------------------------------------------
            // Shipment Relationship
            // ----------------------------------------------------
            // A Worksheet may have zero or one Shipment.
            // Shipment.IsolateID is the FK.
            // ----------------------------------------------------

            entity.HasMany(e => e.Shipments)
                .WithOne(s => s.Worksheet)
                .HasForeignKey(s => s.IsolateID)
                .OnDelete(DeleteBehavior.Restrict);


            // ----------------------------------------------------
            // Indexes
            // ----------------------------------------------------

            entity.HasIndex(e => e.PatientID);
            entity.HasIndex(e => e.WHONETID);
            entity.HasIndex(e => e.CultureResult);
            entity.HasIndex(e => e.ASTCard);
            entity.HasIndex(e => e.MDR);
            entity.HasIndex(e => e.Carbapenemase);
            entity.HasIndex(e => e.MRSA);
            entity.HasIndex(e => e.ManuscriptID);
        });
    }


    // ============================================================
    // Shipment
    // ============================================================

    private static void ConfigureShipment(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.ToTable("Shipment");


            // ----------------------------------------------------
            // Primary Key
            // ----------------------------------------------------
            // KENID is the Shipment identifier.
            // ----------------------------------------------------

            entity.HasKey(e => e.KENID);

            entity.Property(e => e.KENID)
                .HasMaxLength(150)
                .IsRequired();


            // ----------------------------------------------------
            // Worksheet Foreign Key
            // ----------------------------------------------------

            entity.Property(e => e.IsolateID)
                .HasMaxLength(20)
                .IsRequired();


            // ----------------------------------------------------
            // Shipment Information
            // ----------------------------------------------------

            entity.Property(e => e.MALDIID)
                .HasMaxLength(150);

            entity.Property(e => e.MRSNID)
                .HasMaxLength(150);

            entity.Property(e => e.ShipmentNumber)
                .HasMaxLength(100);

            entity.Property(e => e.BoxNumber)
                .HasMaxLength(100);


            // ----------------------------------------------------
            // Worksheet Relationship
            // ----------------------------------------------------

            entity.HasOne(e => e.Worksheet)
                .WithMany(w => w.Shipments)
                .HasForeignKey(e => e.IsolateID)
                .OnDelete(DeleteBehavior.Restrict);


            // ----------------------------------------------------
            // Indexes
            // ----------------------------------------------------

            entity.HasIndex(e => e.IsolateID);
            entity.HasIndex(e => e.MRSNID);
        });
    }


    // ============================================================
    // GeneReference
    // ============================================================

    private static void ConfigureGeneReference(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GeneReference>(entity =>
        {
            entity.ToTable("GeneReference");

            entity.HasKey(e => e.GeneName);

            entity.Property(e => e.GeneName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.GeneDescription)
                .HasMaxLength(255);

            entity.Property(e => e.GeneCategory)
                .HasMaxLength(50);

            entity.Property(e => e.ApplicableOrganism)
                .HasMaxLength(100);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.HasIndex(e => e.GeneCategory);
            entity.HasIndex(e => e.ApplicableOrganism);
            entity.HasIndex(e => e.IsActive);
        });
    }


    // ============================================================
    // Genomics
    // ============================================================

    private static void ConfigureGenomics(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Genomics>(entity =>
        {
            entity.ToTable("Genomics");

            entity.HasKey(e => e.IsolateID);

            entity.Property(e => e.IsolateID)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.KENID)
                .HasMaxLength(150);

            entity.Property(e => e.MRSNID)
                .HasMaxLength(150);

            entity.Property(e => e.DNASampleID)
                .HasMaxLength(100);

            entity.Property(e => e.GenomicONTID)
                .HasMaxLength(100);

            entity.Property(e => e.GenomicILMID)
                .HasMaxLength(100);

            entity.Property(e => e.ProjectCode)
                .HasMaxLength(100);

            entity.Property(e => e.SeqRunID)
                .HasMaxLength(100);

            entity.Property(e => e.SubmittingProtocol)
                .HasMaxLength(255);

            entity.Property(e => e.WHONETID)
                .HasMaxLength(100);

            entity.Property(e => e.WGSID)
                .HasMaxLength(100);

            entity.Property(e => e.GCPercent)
                .HasPrecision(5, 2);

            entity.Property(e => e.StrainType)
                .HasMaxLength(50);

            entity.Property(e => e.Phylogroup)
                .HasMaxLength(50);

            entity.Property(e => e.Serotype)
                .HasMaxLength(100);

            entity.Property(e => e.Fimtype)
                .HasMaxLength(100);

            entity.Property(e => e.SCCmecType)
                .HasMaxLength(50);

            entity.Property(e => e.SpaType)
                .HasMaxLength(50);

            entity.Property(e => e.OLocus)
                .HasMaxLength(50);

            entity.Property(e => e.KLocus)
                .HasMaxLength(50);

            entity.Property(e => e.VirulenceScore)
                .HasPrecision(8, 2);

            entity.Property(e => e.ResistanceScore)
                .HasPrecision(8, 2);

            // ============================================================
            // Worksheet Relationship
            // One Genomics record per Worksheet isolate
            // ============================================================

            entity.HasOne(g => g.Worksheet)
                .WithOne()
                .HasForeignKey<Genomics>(g => g.IsolateID)
                .OnDelete(DeleteBehavior.Restrict);

            // ============================================================
            // Organism Relationship
            // Genomics.WHONETID -> Organism.WHONETID
            // ============================================================

            entity.HasOne(g => g.Organism)
                .WithMany()
                .HasForeignKey(g => g.WHONETID)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // ============================================================
            // Shipment Relationship
            // Genomics.KENID -> Shipment.KENID
            // ============================================================

            entity.HasOne(g => g.Shipment)
                .WithMany()
                .HasForeignKey(g => g.KENID)
                .HasPrincipalKey(s => s.KENID)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.WHONETID);
            entity.HasIndex(e => e.KENID);
            entity.HasIndex(e => e.MRSNID);
            entity.HasIndex(e => e.SequencingDate);
            entity.HasIndex(e => e.StrainType);
        });
    }
}