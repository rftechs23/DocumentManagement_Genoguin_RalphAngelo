using DocumentManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.Infrastructure.Data;

public sealed class DocumentDbContext(DbContextOptions<DocumentDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var document = modelBuilder.Entity<Document>();

        document.ToTable("Documents");
        document.HasKey(x => x.Id);

        document.Property(x => x.OriginalFileName)
            .HasMaxLength(255)
            .IsRequired();

        document.Property(x => x.StorageFileName)
            .HasMaxLength(128)
            .IsRequired();

        document.HasIndex(x => x.StorageFileName)
            .IsUnique();

        document.Property(x => x.ContentType)
            .HasMaxLength(255)
            .IsRequired();

        document.Property(x => x.FileSize)
            .IsRequired();

        document.Property(x => x.UploadedAtUtc)
            .IsRequired();
    }
}
