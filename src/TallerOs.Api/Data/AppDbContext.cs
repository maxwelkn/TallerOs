using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Workshop;

namespace TallerOs.Api.Data;

public enum UserRole { Standard, Administrator }
public enum MailState { Pending, Sent, Failed }

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Standard;
    public bool EmailConfirmed { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockedUntilUtc { get; set; }
}

public sealed class ActivationToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresUtc { get; set; }
    public DateTimeOffset? UsedUtc { get; set; }
}

public sealed class RecoveryCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = "";
    public DateTimeOffset IssuedUtc { get; set; }
    public DateTimeOffset ExpiresUtc { get; set; }
    public DateTimeOffset? UsedUtc { get; set; }
}

public sealed class UserSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset ExpiresUtc { get; set; }
    public DateTimeOffset? RevokedUtc { get; set; }
}

public sealed class QueuedEmail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public MailState State { get; set; } = MailState.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? SentUtc { get; set; }
    public string? LastError { get; set; }
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<ActivationToken> ActivationTokens => Set<ActivationToken>();
    public DbSet<RecoveryCode> RecoveryCodes => Set<RecoveryCode>();
    public DbSet<UserSession> Sessions => Set<UserSession>();
    public DbSet<QueuedEmail> EmailOutbox => Set<QueuedEmail>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<Diagnosis> Diagnoses => Set<Diagnosis>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<UserAccount>().HasIndex(x => x.Email).IsUnique();
        model.Entity<UserSession>().HasIndex(x => x.TokenHash).IsUnique();
        model.Entity<ActivationToken>().HasIndex(x => x.TokenHash).IsUnique();
        model.Entity<RecoveryCode>().HasIndex(x => x.CodeHash).IsUnique();
        model.Entity<UserAccount>().Property(x => x.Role).HasConversion<string>();
        model.Entity<QueuedEmail>().Property(x => x.State).HasConversion<string>();
        model.Entity<WorkOrder>().Property(x => x.Status).HasConversion<string>();
        model.Entity<Vehicle>().HasOne(x => x.Customer).WithMany(x => x.Vehicles).HasForeignKey(x => x.CustomerId);
        model.Entity<WorkOrder>().HasOne(x => x.Vehicle).WithMany(x => x.WorkOrders).HasForeignKey(x => x.VehicleId);
        model.Entity<Diagnosis>().HasOne(x => x.WorkOrder).WithMany(x => x.Diagnoses).HasForeignKey(x => x.WorkOrderId);
    }
}
