using DryIoc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.Database;
using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Sdk.Cache;
using ShadowViewer.Sdk.Database;
using ShadowViewer.Sdk.Models;

var testDirectory = Path.Combine(Path.GetTempPath(), "ShadowViewer-Database-Tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testDirectory);
var tests = new (string Name, Func<Task> Run)[]
{
    ("Fresh database migrates both contexts and repeated startup is idempotent", FreshDatabase),
    ("DryIoc factories create independent contexts during concurrent operations", FactoriesAndConcurrency),
    ("Graph inserts, tags, authors, timestamps, JSON edits and cascades work", GraphOperations),
    ("Actual SqlSugar schema preserves IDs, relationships, progress, JSON and passwords", LegacyDatabase),
    ("Legacy unknown columns fail without changing owned tables", UnknownColumnRollback),
    ("Legacy duplicate mapping keys fail without dropping rows", DuplicateMappingRollback),
    ("Legacy missing nullable column is added safely", MissingNullableColumn),
    ("Invalid legacy relationships fail and retain the original rows", InvalidForeignKey),
    ("An older component refuses a database with newer migration history", NewerDatabase),
    ("Migration backup includes committed WAL data", WalBackup),
    ("Versioned migrations upgrade an existing library once", IncrementalUpgrade),
    ("Failed version upgrade rolls back DDL and keeps a readable backup", FailedUpgrade)
};
var failed = 0;
try
{
    foreach (var (name, run) in tests)
    {
        try { await run(); Console.WriteLine("PASS " + name); }
        catch (Exception exception) { failed++; Console.Error.WriteLine("FAIL " + name + "\n" + exception); }
    }
}
finally
{
    SqliteConnection.ClearAllPools();
    Directory.Delete(testDirectory, recursive: true);
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} database checks passed.");
return failed == 0 ? 0 : 1;

string DatabasePath(string name) => Path.Combine(testDirectory, name + ".sqlite");
SqliteContextFactory<ShadowDbContext> CoreFactory(string path) => new(path, "__EFMigrationsHistory_Sdk", options => new ShadowDbContext(options));
SqliteContextFactory<LocalDbContext> LocalFactory(string path) => new(path, "__EFMigrationsHistory_Local", options => new LocalDbContext(options));
void Initialize(string path)
{
    using var core = CoreFactory(path).CreateDbContext();
    DatabaseUpgrade.Initialize(core, "__EFMigrationsHistory_Sdk");
    using var local = LocalFactory(path).CreateDbContext();
    DatabaseUpgrade.Initialize(local, "__EFMigrationsHistory_Local");
}
void CreateLegacy(string path)
{
    Execute(path, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sqlsugar-1.6.6.sql")));
    Execute(path, "CREATE TABLE OtherPlugin (Id TEXT PRIMARY KEY, Content TEXT); INSERT INTO OtherPlugin VALUES ('keep', 'unchanged');");
}
void Execute(string path, string sql)
{
    using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = false }.ToString());
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    command.ExecuteNonQuery();
}
object? Scalar(string path, string sql)
{
    using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = false }.ToString());
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    return command.ExecuteScalar();
}
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
void ExpectFailure(Action action)
{
    try { action(); }
    catch (Exception exception) when (exception is InvalidOperationException or SqliteException or DbUpdateException) { return; }
    throw new InvalidOperationException("Expected an upgrade failure.");
}

Task FreshDatabase()
{
    var path = DatabasePath("fresh");
    Initialize(path);
    using var local = LocalFactory(path).CreateDbContext();
    Check(local.Database.GetAppliedMigrations().Count() == 1, "Missing local migration.");
    using var core = CoreFactory(path).CreateDbContext();
    Check(core.Database.GetAppliedMigrations().Count() == 1, "Missing SDK migration.");
    Check(!local.Database.HasPendingModelChanges() && !core.Database.HasPendingModelChanges(), "Model snapshots do not match production models.");
    var backups = Directory.GetFiles(testDirectory, "fresh.sqlite.*.bak").Length;
    Initialize(path);
    Check(Directory.GetFiles(testDirectory, "fresh.sqlite.*.bak").Length == backups, "Unchanged startup made another backup.");
    return Task.CompletedTask;
}

async Task FactoriesAndConcurrency()
{
    var path = DatabasePath("factories");
    Initialize(path);
    using var container = new Container();
    DatabaseRegistration.Register<LocalDbContext>(container, path, "__EFMigrationsHistory_Local", options => new LocalDbContext(options));
    DiFactory.Services = container;
    var factory = container.Resolve<IDbContextFactory<LocalDbContext>>();
    Check(ReferenceEquals(factory, container.Resolve<IDbContextFactory<LocalDbContext>>()), "Factory is not a singleton.");
    using var first = factory.CreateDbContext();
    using var second = factory.CreateDbContext();
    Check(!ReferenceEquals(first, second), "Factory reused a DbContext.");
    ComicNode.CreateFolder("folder");
    ComicNode.CreateFolder("folder");
    Check(first.Set<ComicNode>().Count() == 2 && first.Set<ComicNode>().Any(x => x.Name == "folder(1)"), "Folder creation changed.");
    await Task.WhenAll(Enumerable.Range(0, 12).Select(index => Task.Run(async () =>
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Add(new ComicNode { Name = "parallel " + index, ReadingRecord = new LocalReadingRecord() });
        await db.SaveChangesAsync();
        Check(await db.Set<ComicNode>().AnyAsync(), "Concurrent query failed.");
    })));
    Check(first.Set<ComicNode>().Count() == 14, "Concurrent inserts lost rows.");
}

async Task GraphOperations()
{
    var path = DatabasePath("graph");
    Initialize(path);
    var factory = LocalFactory(path);
    long comicId;
    await using (var db = factory.CreateDbContext())
    {
        var source = new SourcePluginData("local", "2.0.0", "Reader", "#fff", "#000") { ExtraData = new() { ["revision"] = 1 } };
        var tag = new ShadowTag("test tag", "#fff", "#000", null, "local", tagType: 1);
        var comic = new ComicNode
        {
            Name = "book", NodeType = "Comic", SourcePluginData = source, ReadingRecord = new LocalReadingRecord(),
            ComicDetail = new ComicDetail { PageCount = 1, ChapterCount = 1, Tags = [tag], Authors = [new LocalAuthor { Name = "author" }] }
        };
        db.Add(comic);
        comicId = comic.Id;
        var chapter = new ComicChapter { ComicId = comicId, Name = "chapter", PageCount = 1 };
        db.Add(chapter);
        db.Add(new ComicPicture { ComicId = comicId, ChapterId = chapter.Id, Name = "page", StoragePath = "C:/page.png" });
        await db.SaveChangesAsync();
        Check(comicId > 0 && comic.ReadingRecord.Id == comicId && comic.ComicDetail.ComicId == comicId, "Generated graph keys did not propagate.");
        Check(comic.CreatedDateTime > DateTime.MinValue && comic.ReadingRecord.UpdatedDateTime > DateTime.MinValue, "Timestamps were not set.");
    }
    await using (var db = factory.CreateDbContext())
    {
        var comic = await db.Set<ComicNode>().Include(x => x.ReadingRecord).Include(x => x.ComicDetail)!.ThenInclude(x => x!.Tags)
            .Include(x => x.ComicDetail)!.ThenInclude(x => x!.Authors).Include(x => x.SourcePluginData).SingleAsync();
        Check(comic.ComicDetail!.Tags!.Count == 1 && comic.ComicDetail.Authors!.Count == 1, "Many-to-many includes failed.");
        comic.SourcePluginData!.ExtraData!["revision"] = 2;
        await db.SaveChangesAsync();
        await db.Set<LocalReadingRecord>().Where(x => x.Id == comicId).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Percent, 75.5m));
        await db.Set<ComicNode>().Where(x => x.Id == comicId).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Name, "renamed"));
        using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Set<ComicNode>().Where(x => x.Id == comicId).ExecuteDeleteAsync();
            await transaction.RollbackAsync();
        }
    }
    await using (var db = factory.CreateDbContext())
    {
        Check((await db.Set<SourcePluginData>().SingleAsync()).ExtraData!["revision"].ToString() == "2", "Mutable JSON was not persisted.");
        Check((await db.Set<LocalReadingRecord>().SingleAsync()).Percent == 75.5m, "Decimal progress changed.");
        Check((await db.Set<ComicNode>().SingleAsync()).Name == "renamed", "Rename or rollback failed.");
        await db.Set<ComicNode>().Where(x => x.Id == comicId).ExecuteDeleteAsync();
        Check(!await db.Set<ComicPicture>().AnyAsync() && !await db.Set<ComicChapter>().AnyAsync() && !await db.Set<LocalReadingRecord>().AnyAsync()
              && !await db.Set<LocalComicTagMapping>().AnyAsync() && !await db.Set<ComicDetail>().AnyAsync(), "Cascade deletion left dependents.");
        Check(await db.ShadowTags.AnyAsync() && await db.Set<LocalAuthor>().AnyAsync(), "Cascade removed shared metadata.");
    }
}

Task LegacyDatabase()
{
    var path = DatabasePath("legacy");
    CreateLegacy(path);
    Initialize(path);
    using var db = LocalFactory(path).CreateDbContext();
    var comic = db.Set<ComicNode>().Include(x => x.ReadingRecord).Include(x => x.ComicDetail)!.ThenInclude(x => x!.Tags)
        .Include(x => x.ComicDetail)!.ThenInclude(x => x!.Authors).Include(x => x.SourcePluginData).Single(x => x.Id == 700000000000000003);
    Check(comic.Name == "legacy comic" && comic.ParentId == -1 && comic.ComicDetail!.PageCount == 10, "Legacy book changed.");
    Check(comic.ReadingRecord.Percent == 42.5m && comic.ReadingRecord.LastPicture == 4, "Legacy progress changed.");
    Check(comic.ComicDetail!.Tags!.Single().Id == 700000000000000001 && comic.ComicDetail.Authors!.Single().Name == "legacy author", "Legacy joins changed.");
    Check(comic.SourcePluginData!.ExtraData!["origin"].ToString() == "legacy", "Legacy JSON changed.");
    Check(db.CacheZips.Single().Password == "legacy-password" && db.Set<ComicPicture>().Single().ChapterId == 700000000000000004, "Legacy cache or chapter changed.");
    Check((string)Scalar(path, "SELECT Content FROM OtherPlugin")! == "unchanged", "Another plugin's table changed.");
    Check((string)Scalar(path, "SELECT Path FROM CacheImg")! == Path.Combine("C:/legacy/thumb", "700000000000000006.png"), "Getter column changed.");
    Check(Directory.GetFiles(testDirectory, "legacy.sqlite.*.bak").Length == 2, "Legacy migration was not backed up per context.");
    Initialize(path);
    Check(db.Set<ComicNode>().Count() == 2, "Second upgrade duplicated books.");
    return Task.CompletedTask;
}

Task UnknownColumnRollback()
{
    var path = DatabasePath("unknown");
    CreateLegacy(path);
    Execute(path, "ALTER TABLE ComicNode ADD COLUMN UnknownPluginData TEXT; UPDATE ComicNode SET UnknownPluginData = 'preserve';");
    using var core = CoreFactory(path).CreateDbContext();
    DatabaseUpgrade.Initialize(core, "__EFMigrationsHistory_Sdk");
    using var local = LocalFactory(path).CreateDbContext();
    ExpectFailure(() => DatabaseUpgrade.Initialize(local, "__EFMigrationsHistory_Local"));
    Check((string)Scalar(path, "SELECT UnknownPluginData FROM ComicNode LIMIT 1")! == "preserve", "Unknown data was discarded.");
    Check((long)Scalar(path, "SELECT COUNT(*) FROM ComicNode")! == 2, "Rollback lost nodes.");
    Check(!local.Database.GetAppliedMigrations().Any(), "Failed adoption recorded a migration.");
    return Task.CompletedTask;
}

Task DuplicateMappingRollback()
{
    var path = DatabasePath("duplicate");
    CreateLegacy(path);
    Execute(path, "INSERT INTO LocalComicTagMapping SELECT * FROM LocalComicTagMapping;");
    using var core = CoreFactory(path).CreateDbContext();
    DatabaseUpgrade.Initialize(core, "__EFMigrationsHistory_Sdk");
    using var local = LocalFactory(path).CreateDbContext();
    ExpectFailure(() => DatabaseUpgrade.Initialize(local, "__EFMigrationsHistory_Local"));
    Check((long)Scalar(path, "SELECT COUNT(*) FROM LocalComicTagMapping")! == 2, "Duplicate rows were silently removed.");
    Check((long)Scalar(path, "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE '__ef_upgrade_%'")! == 0, "Rollback left staging tables.");
    return Task.CompletedTask;
}

Task MissingNullableColumn()
{
    var path = DatabasePath("nullable");
    CreateLegacy(path);
    Execute(path, "ALTER TABLE ComicNode DROP COLUMN BrokenReason;");
    Initialize(path);
    using var local = LocalFactory(path).CreateDbContext();
    Check(local.Set<ComicNode>().All(x => x.BrokenReason == null), "Nullable column was not added.");
    return Task.CompletedTask;
}

Task WalBackup()
{
    var path = DatabasePath("wal");
    CreateLegacy(path);
    using var writer = new SqliteConnection("Data Source=" + path);
    writer.Open();
    using var command = writer.CreateCommand();
    command.CommandText = "PRAGMA journal_mode=WAL; UPDATE CacheZip SET Password='written-in-wal';";
    command.ExecuteNonQuery();
    using var core = CoreFactory(path).CreateDbContext();
    DatabaseUpgrade.Initialize(core, "__EFMigrationsHistory_Sdk");
    var backup = Directory.GetFiles(testDirectory, "wal.sqlite.ShadowDbContext.*.bak").Single();
    Check((string)Scalar(backup, "SELECT Password FROM CacheZip")! == "written-in-wal", "Backup missed WAL records.");
    return Task.CompletedTask;
}

Task InvalidForeignKey()
{
    var path = DatabasePath("invalid-fk");
    CreateLegacy(path);
    Execute(path, "UPDATE ComicPicture SET ChapterId = 999;");
    using var core = CoreFactory(path).CreateDbContext();
    DatabaseUpgrade.Initialize(core, "__EFMigrationsHistory_Sdk");
    using var local = LocalFactory(path).CreateDbContext();
    ExpectFailure(() => DatabaseUpgrade.Initialize(local, "__EFMigrationsHistory_Local"));
    Check((long)Scalar(path, "SELECT ChapterId FROM ComicPicture")! == 999, "Invalid relationships were silently deleted.");
    Check(!local.Database.GetAppliedMigrations().Any(), "Invalid relationship advanced migration history.");
    return Task.CompletedTask;
}

Task NewerDatabase()
{
    var path = DatabasePath("newer");
    Initialize(path);
    Execute(path, "INSERT INTO __EFMigrationsHistory_Local VALUES ('20990101000000_Future', '8.0.31');");
    using var local = LocalFactory(path).CreateDbContext();
    ExpectFailure(() => DatabaseUpgrade.Initialize(local, "__EFMigrationsHistory_Local"));
    Check(local.Database.GetAppliedMigrations().Count() == 2, "Newer history changed.");
    return Task.CompletedTask;
}

Task IncrementalUpgrade()
{
    var path = DatabasePath("incremental");
    using var context = UpgradeContext.Create(path);
    context.GetService<IMigrator>().Migrate("20260101000000_Initial");
    Execute(path, "INSERT INTO Versioned (Id, Name) VALUES (1, 'keep');");
    DatabaseUpgrade.Initialize(context, "__EFMigrationsHistory_Test");
    Check((string)Scalar(path, "SELECT Title FROM Versioned")! == "keep", "Rename migration lost data.");
    Check((long)Scalar(path, "SELECT Revision FROM Versioned")! == 2, "New required column did not use its default for existing data.");
    Check(context.Database.GetAppliedMigrations().Count() == 2, "Incremental history did not advance.");
    DatabaseUpgrade.Initialize(context, "__EFMigrationsHistory_Test");
    Check(Directory.GetFiles(testDirectory, "incremental.sqlite.*.bak").Length == 1, "Repeated upgrade reran migration.");
    return Task.CompletedTask;
}

Task FailedUpgrade()
{
    var path = DatabasePath("failed-version");
    using var context = FailedUpgradeContext.Create(path);
    context.GetService<IMigrator>().Migrate("20260101000000_Initial");
    Execute(path, "INSERT INTO Versioned (Id, Name) VALUES (1, 'keep');");
    ExpectFailure(() => DatabaseUpgrade.Initialize(context, "__EFMigrationsHistory_Test"));
    Check((string)Scalar(path, "SELECT Name FROM Versioned")! == "keep", "Failure did not roll back schema changes.");
    Check(context.Database.GetAppliedMigrations().Count() == 1, "Failed migration advanced history.");
    var backup = Directory.GetFiles(testDirectory, "failed-version.sqlite.*.bak").Single();
    Check((string)Scalar(backup, "SELECT Name FROM Versioned")! == "keep", "Failed upgrade backup is unreadable.");
    return Task.CompletedTask;
}

public class UpgradeContext(DbContextOptions<UpgradeContext> options) : DbContext(options)
{
    public static UpgradeContext Create(string path) => new(new DbContextOptionsBuilder<UpgradeContext>()
        .UseSqlite("Data Source=" + path, sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_Test")).Options);
}
public class FailedUpgradeContext(DbContextOptions<FailedUpgradeContext> options) : DbContext(options)
{
    public static FailedUpgradeContext Create(string path) => new(new DbContextOptionsBuilder<FailedUpgradeContext>()
        .UseSqlite("Data Source=" + path, sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_Test")).Options);
}
[DbContext(typeof(UpgradeContext)), Migration("20260101000000_Initial")]
public class UpgradeInitial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("CREATE TABLE Versioned (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);");
}
[DbContext(typeof(UpgradeContext)), Migration("20260102000000_Rename")]
public class UpgradeRename : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn("Name", "Versioned", "Title");
        migrationBuilder.AddColumn<int>("Revision", "Versioned", nullable: false, defaultValue: 2);
    }
}
[DbContext(typeof(FailedUpgradeContext)), Migration("20260101000000_Initial")]
public class FailedUpgradeInitial : UpgradeInitial { }
[DbContext(typeof(FailedUpgradeContext)), Migration("20260102000000_Fail")]
public class UpgradeFail : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn("Name", "Versioned", "Title");
        migrationBuilder.Sql("INSERT INTO MissingTable VALUES (1);");
    }
}
