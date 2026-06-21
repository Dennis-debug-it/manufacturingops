using Ecosys.Platform.Entities;
using Ecosys.Platform.Enums;
using Ecosys.ServiceOps.Entities;
using Ecosys.ServiceOps.Enums;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Ecosys.Infrastructure.Persistence;

public sealed class EcosysDbContext(DbContextOptions<EcosysDbContext> options, ITenantContext tenantContext) : DbContext(options)
{
    private Guid CurrentTenantId => tenantContext.TenantId ?? Guid.Empty;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<EmailSetting> EmailSettings => Set<EmailSetting>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<TenantIdentityProvider> TenantIdentityProviders => Set<TenantIdentityProvider>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<WorkOrderType> WorkOrderTypes => Set<WorkOrderType>();
    public DbSet<AssignmentGroup> AssignmentGroups => Set<AssignmentGroup>();
    public DbSet<AssignmentGroupMember> AssignmentGroupMembers => Set<AssignmentGroupMember>();
    public DbSet<CustomerContract> CustomerContracts => Set<CustomerContract>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<WorkOrderUpdate> WorkOrderUpdates => Set<WorkOrderUpdate>();
    public DbSet<WorkOrderImage> WorkOrderImages => Set<WorkOrderImage>();
    public DbSet<StoreItem> StoreItems => Set<StoreItem>();
    public DbSet<MaterialRequest> MaterialRequests => Set<MaterialRequest>();
    public DbSet<MaterialRequestLine> MaterialRequestLines => Set<MaterialRequestLine>();
    public DbSet<MaterialUsage> MaterialUsages => Set<MaterialUsage>();
    public DbSet<PmSchedule> PmSchedules => Set<PmSchedule>();
    public DbSet<WorkOrderReport> WorkOrderReports => Set<WorkOrderReport>();
    public DbSet<WorkOrderAcknowledgement> WorkOrderAcknowledgements => Set<WorkOrderAcknowledgement>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditFields();
        return base.SaveChanges();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigurePlatform(modelBuilder);
        ConfigureServiceOps(modelBuilder);
    }

    private void ConfigurePlatform(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Plan).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SubscriptionStatus).HasConversion<string>().HasMaxLength(50);
            entity.HasIndex(x => x.Code).IsUnique();

            entity.HasData(new Tenant
            {
                Id = PlatformConstants.RootTenantId,
                Name = "Ecosys Platform",
                Code = "PLATFORM",
                Plan = "Platform",
                SubscriptionStatus = SubscriptionStatus.Active,
                IsActive = true,
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
            entity.HasData(new AppUser
            {
                Id = PlatformConstants.SuperAdminUserId,
                TenantId = PlatformConstants.RootTenantId,
                FirstName = "Platform",
                LastName = "SuperAdmin",
                Email = "superadmin@ecosys.local",
                Role = AppRoles.SuperAdmin,
                IsActive = true,
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        modelBuilder.Entity<TenantSetting>(entity =>
        {
            entity.ToTable("tenant_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.LogoUrl).HasMaxLength(500);
            entity.Property(x => x.PrimaryColor).HasMaxLength(20).IsRequired();
            entity.Property(x => x.SecondaryColor).HasMaxLength(20).IsRequired();
            entity.Property(x => x.AccentColor).HasMaxLength(20).IsRequired();
            entity.Property(x => x.EmailSenderName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.EmailSenderAddress).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<NumberSequence>(entity =>
        {
            entity.ToTable("number_sequences");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Prefix).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ResetFrequency).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(x => new { x.TenantId, x.EntityType }).IsUnique();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<EmailSetting>(entity =>
        {
            entity.ToTable("email_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Host).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Username).HasMaxLength(200);
            entity.Property(x => x.Password).HasMaxLength(500);
            entity.Property(x => x.SenderName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.SenderAddress).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Subject).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(4000).IsRequired();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Details).HasMaxLength(4000);
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<TenantIdentityProvider>(entity =>
        {
            entity.ToTable("tenant_identity_providers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProviderName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ClientId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Authority).HasMaxLength(500).IsRequired();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });
    }

    private void ConfigureServiceOps(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.Property(x => x.ContactPerson).HasMaxLength(200);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.ToTable("locations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(50);
            entity.HasOne(x => x.ParentLocation)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentLocationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.ToTable("assets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.SerialNumber).HasMaxLength(100);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<WorkOrderType>(entity =>
        {
            entity.ToTable("work_order_types");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<AssignmentGroup>(entity =>
        {
            entity.ToTable("assignment_groups");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<AssignmentGroupMember>(entity =>
        {
            entity.ToTable("group_members");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.AssignmentGroupId, x.UserId }).IsUnique();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<CustomerContract>(entity =>
        {
            entity.ToTable("customer_contracts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<SlaPolicy>(entity =>
        {
            entity.ToTable("sla_policies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.ToTable("work_orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Number).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000);
            entity.Property(x => x.ResolutionNotes).HasMaxLength(4000);
            entity.Property(x => x.Priority).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.SlaStatus).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<WorkOrderUpdate>(entity =>
        {
            entity.ToTable("work_order_updates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Message).HasMaxLength(4000).IsRequired();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<WorkOrderImage>(entity =>
        {
            entity.ToTable("work_order_images");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Url).HasMaxLength(500).IsRequired();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<StoreItem>(entity =>
        {
            entity.ToTable("store_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Sku).HasMaxLength(50).IsRequired();
            entity.Property(x => x.QuantityOnHand).HasColumnType("numeric(18,2)");
            entity.Property(x => x.UnitOfMeasure).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<MaterialRequest>(entity =>
        {
            entity.ToTable("material_requests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Number).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<MaterialRequestLine>(entity =>
        {
            entity.ToTable("material_request_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequestedQuantity).HasColumnType("numeric(18,2)");
            entity.Property(x => x.IssuedQuantity).HasColumnType("numeric(18,2)");
            entity.Property(x => x.UsedQuantity).HasColumnType("numeric(18,2)");
            entity.Property(x => x.ReturnedQuantity).HasColumnType("numeric(18,2)");
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<MaterialUsage>(entity =>
        {
            entity.ToTable("material_usages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuantityUsed).HasColumnType("numeric(18,2)");
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<PmSchedule>(entity =>
        {
            entity.ToTable("pm_schedules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<WorkOrderReport>(entity =>
        {
            entity.ToTable("work_order_reports");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Content).HasMaxLength(20000).IsRequired();
            entity.HasIndex(x => x.WorkOrderId).IsUnique();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<WorkOrderAcknowledgement>(entity =>
        {
            entity.ToTable("work_order_acknowledgements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AcknowledgedBy).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.WorkOrderId).IsUnique();
            entity.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });
    }

    private void ApplyAuditFields()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedUtc = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedUtc = utcNow;
            }
        }
    }
}
