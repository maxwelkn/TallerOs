using Microsoft.EntityFrameworkCore;
using TallerOs.Domain.Documents;

namespace TallerOs.Infrastructure.Documents;

public sealed class DocumentDbContext(DbContextOptions<DocumentDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var document = modelBuilder.Entity<Document>();
        document.HasKey(item => item.Id);
        document.Property(item => item.Id).ValueGeneratedNever();
        document.Property(item => item.Name).HasMaxLength(255).IsRequired();
        document.Property(item => item.ContentType).HasMaxLength(127).IsRequired();
        document.Property(item => item.StorageKey).HasMaxLength(32).IsRequired();
        document.Property(item => item.UploadedUtc).HasConversion(value => value.UtcTicks,
            value => new DateTimeOffset(value, TimeSpan.Zero));
        document.Property(item => item.IsDeleted).IsConcurrencyToken();
        document.HasIndex(item => new { item.UploadedBy, item.IsDeleted });
        document.HasIndex(item => item.StorageKey).IsUnique();
    }
}
