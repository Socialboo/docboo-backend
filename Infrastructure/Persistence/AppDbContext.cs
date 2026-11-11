using System.Text.Json;
using Domain.Assets;
using Domain.Documents;
using Domain.Exports;
using Domain.Works;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Work> Works => Set<Work>();
    public DbSet<ExportJob> ExportJobs => Set<ExportJob>();
    public DbSet<Asset> Assets => Set<Asset>();

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
        MapDocument(work.Property(w => w.Document));
        work.HasIndex(w => new { w.UserId, w.UpdatedAt }).HasDatabaseName("ix_works_user_id_updated_at");

        var job = modelBuilder.Entity<ExportJob>();
        job.ToTable("export_jobs");
        job.HasKey(j => j.Id);
        job.Property(j => j.Id).HasColumnName("id").ValueGeneratedNever();
        job.Property(j => j.WorkId).HasColumnName("work_id");
        job.Property(j => j.UserId).HasColumnName("user_id");
        job.Property(j => j.Format).HasColumnName("format").HasConversion<string>().HasMaxLength(16);
        job.Property(j => j.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        job.Property(j => j.DocumentVersion).HasColumnName("document_version");
        MapDocument(job.Property(j => j.Document)).HasColumnName("document_snapshot");
        job.Property(j => j.FileName).HasColumnName("file_name").HasMaxLength(128);
        job.Property(j => j.FileKey).HasColumnName("file_key").HasMaxLength(128);
        job.Property(j => j.FileSize).HasColumnName("file_size");
        job.Property(j => j.Error).HasColumnName("error").HasMaxLength(1000);
        job.Property(j => j.Warnings)
            .HasColumnName("warnings")
            .HasColumnType("jsonb")
            .HasConversion(
                w => JsonSerializer.Serialize(w, (JsonSerializerOptions?)null),
                s => JsonSerializer.Deserialize<List<string>>(s, (JsonSerializerOptions?)null) ?? new List<string>(),
                new ValueComparer<IReadOnlyList<string>>(
                    (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
                    w => w.Aggregate(0, (h, s) => HashCode.Combine(h, s)),
                    w => w.ToList()));
        job.Property(j => j.Attempts).HasColumnName("attempts");
        job.Property(j => j.CreatedAt).HasColumnName("created_at");
        job.Property(j => j.StartedAt).HasColumnName("started_at");
        job.Property(j => j.CompletedAt).HasColumnName("completed_at");
        job.Property(j => j.ExpiresAt).HasColumnName("expires_at");
        job.Ignore(j => j.ContentType);
        job.HasOne<Work>().WithMany().HasForeignKey(j => j.WorkId).OnDelete(DeleteBehavior.Cascade);
        job.HasIndex(j => new { j.UserId, j.WorkId, j.CreatedAt }).HasDatabaseName("ix_export_jobs_user_work_created");
        job.HasIndex(j => new { j.Status, j.ExpiresAt }).HasDatabaseName("ix_export_jobs_status_expires");

        var asset = modelBuilder.Entity<Asset>();
        asset.ToTable("assets");
        asset.HasKey(a => a.Id);
        asset.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        asset.Property(a => a.WorkId).HasColumnName("work_id");
        asset.Property(a => a.UserId).HasColumnName("user_id");
        asset.Property(a => a.ContentType).HasColumnName("content_type").HasMaxLength(32);
        asset.Property(a => a.Width).HasColumnName("width");
        asset.Property(a => a.Height).HasColumnName("height");
        asset.Property(a => a.SizeBytes).HasColumnName("size_bytes");
        asset.Property(a => a.OriginalName).HasColumnName("original_name").HasMaxLength(120);
        asset.Property(a => a.StorageKey).HasColumnName("storage_key").HasMaxLength(128);
        asset.Property(a => a.CreatedAt).HasColumnName("created_at");
        asset.Ignore(a => a.DocumentId);
        asset.HasOne<Work>().WithMany().HasForeignKey(a => a.WorkId).OnDelete(DeleteBehavior.Cascade);
        asset.HasIndex(a => new { a.UserId, a.WorkId }).HasDatabaseName("ix_assets_user_work");
    }

    private static PropertyBuilder<DocumentModel> MapDocument(PropertyBuilder<DocumentModel> property) =>
        property
            .HasColumnName("document")
            .HasColumnType("jsonb")
            .HasConversion(
                d => DocumentJson.Serialize(d),
                s => DocumentJson.Deserialize(s),
                new ValueComparer<DocumentModel>(
                    (a, b) => ReferenceEquals(a, b) || (a != null && b != null && DocumentJson.Serialize(a) == DocumentJson.Serialize(b)),
                    d => DocumentJson.Serialize(d).GetHashCode(),
                    d => DocumentJson.Deserialize(DocumentJson.Serialize(d))));
}
