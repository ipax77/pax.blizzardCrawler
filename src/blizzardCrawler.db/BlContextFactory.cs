using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace blizzardCrawler.db;

public class ReplayContextFactory : IDesignTimeDbContextFactory<BlContext>
{
    public BlContext CreateDbContext(string[] args)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText("/data/localserverconfig.json"));
        var config = json.GetProperty("ServerConfig");
        var connectionString = config.GetProperty("BlConnectionString").GetString();
        var serverVersion = new MySqlServerVersion(new System.Version(5, 7, 42));

        var optionsBuilder = new DbContextOptionsBuilder<BlContext>();
        optionsBuilder.UseMySql(connectionString, serverVersion, x =>
        {
            x.EnableRetryOnFailure();
            x.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
            x.MigrationsAssembly("blizzardCrawler.db");
        });

        return new BlContext(optionsBuilder.Options);
    }
}