using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ShadowViewer.Plugin.Local.Cache;
using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Sdk.Database;
using ShadowViewer.Sdk.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ShadowViewer.Plugin.Local.Database;

public class LocalDbContext : ShadowDbContext
{
    public LocalDbContext(DbContextOptions<LocalDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureCoreModel(modelBuilder, excludeFromMigrations: true);
        var node = modelBuilder.Entity<ComicNode>();
        node.ToTable("ComicNode");
        node.HasKey(x => x.Id);
        node.Property(x => x.Id).ValueGeneratedOnAdd().HasValueGenerator<DatabaseIdValueGenerator>();
        node.Property(x => x.ParentId).HasDefaultValue(-1L);
        node.Property(x => x.NodeType).HasMaxLength(32).HasDefaultValue("Folder");
        node.Property(x => x.Name).HasMaxLength(500);
        node.Property(x => x.Thumb).HasDefaultValue("mx-appx:///default.png");
        node.Ignore(x => x.IsFolder);
        // SqlSugar persisted these getter-only values. Keep their columns when adopting an existing library.
        node.Property<bool>("StoredIsFolder").HasColumnName("IsFolder").HasDefaultValue(false);
        node.Ignore(x => x.Children);
        node.Ignore(x => x.PreviewChapters);
        node.HasIndex(x => x.ParentId).HasDatabaseName("index_comic_node_parent_id");
        node.HasIndex(x => x.CreatedDateTime).HasDatabaseName("index_comic_node_created_at");
        node.HasIndex(x => x.UpdatedDateTime).HasDatabaseName("index_comic_node_updated_at");
        node.HasIndex(x => x.Name).HasDatabaseName("index_comic_node_name");
        node.HasIndex(x => x.NodeType).HasDatabaseName("index_comic_node_type");
        node.HasOne(x => x.SourcePluginData).WithMany().HasForeignKey(x => x.SourcePluginDataId)
            .OnDelete(DeleteBehavior.Restrict);
        node.HasOne(x => x.ComicDetail).WithOne().HasForeignKey<ComicDetail>(x => x.ComicId)
            .OnDelete(DeleteBehavior.Cascade);
        node.HasOne(x => x.ReadingRecord).WithOne().HasForeignKey<LocalReadingRecord>(x => x.Id)
            .OnDelete(DeleteBehavior.Cascade);

        var detail = modelBuilder.Entity<ComicDetail>();
        detail.ToTable("ComicDetail");
        detail.HasKey(x => x.ComicId);
        detail.Property(x => x.ComicId).ValueGeneratedNever();
        detail.Property(x => x.ProcessMode).HasMaxLength(32).HasDefaultValue("Folder");
        detail.HasMany(x => x.Authors).WithMany().UsingEntity<LocalComicAuthorMapping>(
            right => right.HasOne<LocalAuthor>().WithMany().HasForeignKey(x => x.AuthorId),
            left => left.HasOne<ComicDetail>().WithMany().HasForeignKey(x => x.ComicId),
            join => { join.ToTable("LocalComicAuthorMapping"); join.HasKey(x => new { x.ComicId, x.AuthorId }); });
        detail.HasMany(x => x.Tags).WithMany().UsingEntity<LocalComicTagMapping>(
            right => right.HasOne<ShadowTag>().WithMany().HasForeignKey(x => x.TagId),
            left => left.HasOne<ComicDetail>().WithMany().HasForeignKey(x => x.ComicId),
            join => { join.ToTable("LocalComicTagMapping"); join.HasKey(x => new { x.ComicId, x.TagId }); });

        var chapter = modelBuilder.Entity<ComicChapter>();
        chapter.ToTable("ComicChapter");
        chapter.HasKey(x => x.Id);
        chapter.Property(x => x.Id).ValueGeneratedOnAdd().HasValueGenerator<DatabaseIdValueGenerator>();
        chapter.Property(x => x.Name).HasMaxLength(2048);
        chapter.HasOne<ComicNode>().WithMany().HasForeignKey(x => x.ComicId).OnDelete(DeleteBehavior.Cascade);
        var picture = modelBuilder.Entity<ComicPicture>();
        picture.ToTable("ComicPicture");
        picture.HasKey(x => x.Id);
        picture.Property(x => x.Id).ValueGeneratedOnAdd().HasValueGenerator<DatabaseIdValueGenerator>();
        picture.Property(x => x.Name).HasMaxLength(2048);
        picture.Property(x => x.StoragePath).IsRequired(false);
        picture.HasOne<ComicNode>().WithMany().HasForeignKey(x => x.ComicId).OnDelete(DeleteBehavior.Cascade);
        picture.HasOne<ComicChapter>().WithMany().HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.Cascade);

        var source = modelBuilder.Entity<SourcePluginData>();
        source.ToTable("SourcePluginData");
        source.HasKey(x => x.Id);
        source.Property(x => x.Id).HasMaxLength(255);
        source.Property(x => x.PluginId).HasMaxLength(255);
        source.Property(x => x.Name).HasMaxLength(255);
        source.Property(x => x.BackgroundColorHex).HasMaxLength(20);
        source.Property(x => x.ForegroundColorHex).HasMaxLength(20);
        source.Property(x => x.ExtraData).HasConversion(
            value => Serialize(value), value => Deserialize(value))
            .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, object>?>(
                (left, right) => Serialize(left) == Serialize(right),
                value => Serialize(value) == null ? 0 : Serialize(value)!.GetHashCode(),
                value => Deserialize(Serialize(value))));

        var record = modelBuilder.Entity<LocalReadingRecord>();
        record.ToTable("LocalReadingRecord");
        record.HasKey(x => x.Id);
        record.Property(x => x.Id).ValueGeneratedNever();
        var author = modelBuilder.Entity<LocalAuthor>();
        author.ToTable("LocalAuthor");
        author.HasKey(x => x.Id);
        author.Property(x => x.Id).ValueGeneratedOnAdd().HasValueGenerator<DatabaseIdValueGenerator>();
        var history = modelBuilder.Entity<LocalHistory>();
        history.ToTable("LocalHistory");
        history.HasKey(x => x.Id);
        history.Property(x => x.Id).ValueGeneratedNever();
        var image = modelBuilder.Entity<CacheImg>();
        image.ToTable("CacheImg");
        image.HasKey(x => x.Id);
        image.Property(x => x.Id).ValueGeneratedOnAdd().HasValueGenerator<DatabaseIdValueGenerator>();
        image.Ignore(x => x.Path);
        image.Property<string?>("StoredPath").HasColumnName("Path");
        image.HasOne<ComicNode>().WithMany().HasForeignKey(x => x.ComicId).OnDelete(DeleteBehavior.Cascade);
    }

    private static string? Serialize(Dictionary<string, object>? value) =>
        value == null ? null : JsonSerializer.Serialize(value, (JsonSerializerOptions?)null);

    private static Dictionary<string, object>? Deserialize(string? value) =>
        value == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(value, (JsonSerializerOptions?)null);

    private void UpdateTimestamps()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries().ToArray())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
            if (entry.Entity is ComicNode node)
                entry.Property("StoredIsFolder").CurrentValue = node.IsFolder;
            if (entry.Entity is CacheImg image)
                entry.Property("StoredPath").CurrentValue = image.Path;
            if (entry.Entity is not (ComicNode or LocalReadingRecord)) continue;
            if (entry.State == EntityState.Added)
                entry.Property("CreatedDateTime").CurrentValue = DateTime.Now;
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Property("UpdatedDateTime").CurrentValue = DateTime.Now;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
