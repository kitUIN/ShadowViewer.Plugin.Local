using Microsoft.EntityFrameworkCore.Design;
using ShadowViewer.Sdk.Database;

namespace ShadowViewer.Plugin.Local.Database;

public sealed class LocalDbContextDesignFactory : IDesignTimeDbContextFactory<LocalDbContext>
{
    public LocalDbContext CreateDbContext(string[] args) =>
        new SqliteContextFactory<LocalDbContext>(args.Length > 0 ? args[0] : "ShadowViewer.design.sqlite",
            "__EFMigrationsHistory_Local", options => new LocalDbContext(options)).CreateDbContext();
}
