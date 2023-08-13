using blizzardCrawler.db;
using blizzardCrawler.shared;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace blizzardCrawler.services;

public class MatchRepository
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IOptions<DbImportOptions> importOptions;
    private readonly ILogger<MatchRepository> logger;

    public MatchRepository(IServiceScopeFactory scopeFactory,
                           IOptions<DbImportOptions> importOptions,
                           ILogger<MatchRepository> logger)
    {
        this.scopeFactory = scopeFactory;
        this.importOptions = importOptions;
        this.logger = logger;
    }

    private ConcurrentDictionary<PlayerIndex, List<MatchDto>> matchesStore = new();
    private ConcurrentDictionary<string, int> mapnames = new();

    public void StorePlayerMatches(PlayerIndex player, List<BlMatch> blMatches)
    {
        List<MatchDto> matches = new List<MatchDto>();
        for (int i = 0; i < blMatches.Count; i++)
        {
            mapnames.AddOrUpdate(blMatches[i].Map, 0, (k, v) => ++v);
            matches.Add(blMatches[i].ConvertToMatchDto(player.RegionId));
        }
        matchesStore.AddOrUpdate(player, matches, (k, v) =>
            {
                v.AddRange(matches);
                return v;
            });
    }

    public async Task StoreMatches()
    {
        logger.LogInformation("Storing matches - {date}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        var matchents = matchesStore.ToDictionary();
        matchesStore = new();

        using var scope = scopeFactory.CreateScope();
        var playerRepository = scope.ServiceProvider.GetRequiredService<PlayerRepository>();

        var playerIds = await playerRepository.StorePlayers(matchents.Select(s => s.Key).ToList());

        List<MatchInfo> matchInfos = new();
        foreach (var matchEnt in  matchents)
        {
            if (playerIds.TryGetValue(matchEnt.Key, out int playerId))
            {
                matchEnt.Value.ForEach(f => matchInfos.Add(f.ConvertToMatchInfo(playerId)));
            }
        }

        using var connection = new MySqlConnection(importOptions.Value.ImportConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        var command = connection.CreateCommand();
        command.Transaction = transaction;

        int batchSize = 1000;

        int numberOfBatches = (int)Math.Ceiling((double)matchInfos.Count / batchSize);

        for (int i = 0; i < numberOfBatches; i++)
        {
            var currentBatch = matchInfos.Skip(i * batchSize).Take(batchSize);
            var insertStatement = new StringBuilder();
            insertStatement.AppendLine($"INSERT IGNORE INTO {nameof(BlContext.MatchInfos)} ({nameof(MatchInfo.PlayerId)}, {nameof(MatchInfo.MatchDateUnixTimestamp)}, {nameof(MatchInfo.Decision)}, {nameof(MatchInfo.Region)}) VALUES");
            insertStatement.Append(string.Join($",{Environment.NewLine}", currentBatch.Select(s => $"({s.PlayerId}, {s.MatchDateUnixTimestamp}, {(int)s.Decision}, {(int)s.Region})")));
            command.CommandText = insertStatement.ToString();
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
        logger.LogInformation("Storing matches complete - {date}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        logger.LogInformation("Map names: {maps}", string.Join(Environment.NewLine, mapnames.Select(s => $"{s.Key}:{s.Value}")));
    }
}
