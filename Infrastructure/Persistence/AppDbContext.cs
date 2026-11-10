using Domain.Documents;
using Domain.Works;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Work> Works => Set<Work>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var work = modelBuilder.Entity<Work>();
        work.ToTable("works");
        work.HasKey(w => w.Id);
        work.Property(w => w.Id).HasColumnName("id").ValueGeneratedNever();
        work.Property(w => w.UserId).HasColumnName("user_id");
        work.Property(w => w.Title).HasColumnName("title").HasMaxLength(Work.MaxTitleLength);
        work.Property(w => w.ProfileId).HasColumnName("profile_id").HasMaxLength(64);
        work.Property(w => w.Version).HasColumnName("version").IsConcurrencyToken();
        work.Property(w => w.CreatedAt).HasColumnName("created_at");
        work.Property(w => w.UpdatedAt).HasColumnName("updated_at");

        // The document is stored as JSONB using the canonical document serializer, so the database
        // holds exactly what the API returns (polymorphic blocks included).
        work.Property(w => w.Document)
            .HasColumnName("document")
            .HasColumnType("jsonb")
            .HasConversion(
                d => DocumentJson.Serialize(d),
                s => DocumentJson.Deserialize(s),
                new ValueComparer<DocumentModel>(
                    (a, b) => ReferenceEquals(a, b) || (a != null && b != null && DocumentJson.Serialize(a) == DocumentJson.Serialize(b)),
                    d => DocumentJson.Serialize(d).GetHashCode(),
                    d => DocumentJson.Deserialize(DocumentJson.Serialize(d))));

        work.HasIndex(w => new { w.UserId, w.UpdatedAt }).HasDatabaseName("ix_works_user_id_updated_at");
    }
}
