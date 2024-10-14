using HighScores.Models;
using StackExchange.Redis;

namespace HighScores.Services;

public class LeaderboardService
{
    private readonly ILogger<LeaderboardService> _logger;
    private readonly IDatabase _database;

    public LeaderboardService(IConnectionMultiplexer redis, ILogger<LeaderboardService> logger)
    {
        _logger = logger;
        _database = redis.GetDatabase();
    }

    public async Task<LeaderBoard> CreateLeaderboard()
    {
        _logger.LogInformation("Creating new leaderboard...");

        var id = await _database.StringIncrementAsync("lb-counter");
        var secret = Guid.NewGuid().ToString().Replace("-", string.Empty);
        var privateSecret = Guid.NewGuid().ToString().Replace("-", string.Empty);

        await _database.HashSetAsync(
            $"lb-info:{id}",
            new HashEntry[] {
                new("id", id), new("secret", secret), new("private-secret", privateSecret),
            });

        _logger.LogInformation("Created leaderboard with ID {id}...", id);

        return new LeaderBoard(id, secret, privateSecret);
    }

    public async Task SetWebhook(long leaderboard, string? webhook)
    {
        _logger.LogInformation("Setting webhook for leaderboard {lid}...", leaderboard);

        await _database.HashSetAsync(
            $"lb-info:{leaderboard}",
            "webhook",
            webhook
        );
    }
    
    public async Task<string> GetWebhook(long leaderboard) =>
        (await _database.HashGetAsync($"lb-info:{leaderboard}", "webhook")).ToString();

    public async Task<string> GetAppendSecret(long leaderboard) =>
        (await _database.HashGetAsync($"lb-info:{leaderboard}", "secret")).ToString();

    public async Task<string> GetModifySecret(long leaderboard)
    {
        if (await _database.HashExistsAsync($"lb-info:{leaderboard}", "private-secret"))
        {
            return (
                await _database.HashGetAsync($"lb-info:{leaderboard}", "private-secret")
            ).ToString();
        }

        return await GetAppendSecret(leaderboard);
    }

    public async Task<bool> CheckSecret(long leaderboard, string secret, bool forModify)
    {
        var token = await (forModify
            ? GetModifySecret(leaderboard)
            : GetAppendSecret(leaderboard));

        return token == secret;
    }

    public async Task<long> AddScore(long leaderboard, string name, long value, double time)
    {
        _logger.LogInformation(
            "Updating score of leaderboard {lid} for {name} to {value}...",
            leaderboard,
            name,
            value
        );

        var scoreId = $"score:{leaderboard}:{name}";

        var updatedScore = await _database.SortedSetUpdateAsync(
            $"lb:{leaderboard}",
            scoreId,
            value,
            SortedSetWhen.GreaterThan
        );

        if (updatedScore)
        {
            _logger.LogInformation(
                "Updating player info of leaderboard {lid} for {name}...",
                leaderboard,
                name
            );

            await _database.HashSetAsync(scoreId,
                new HashEntry[] {
                    new("name", name), new("value", value), new("time", time)
                });
        }

        var place = await _database.SortedSetRankAsync($"lb:{leaderboard}", scoreId, Order.Descending);

        return place.HasValue
            ? place.Value + 1
            : 0;
    }

    public async Task<Score[]?> GetScores(long leaderboard, int count, int offset)
    {
        if (!await _database.KeyExistsAsync($"lb-info:{leaderboard}"))
            return null;

        var scoresIds = await _database.SortedSetRangeByScoreAsync(
            $"lb:{leaderboard}",
            skip: offset,
            take: count,
            order: Order.Descending
        );

        List<Score> scores = new();

        foreach (var scoreId in scoresIds)
        {
            var score = await GetScore(scoreId.ToString());

            if (score is null)
            {
                continue;
            }

            scores.Add(score);
        }

        return scores.ToArray();
    }

    public async Task<Score?> GetScore(long leaderboard, string name) => await GetScore($"score:{leaderboard}:{name}");

    private async Task<Score?> GetScore(string key)
    {
        var values = await _database.HashGetAsync(
            key,
            new RedisValue[] {
                "name", "value", "time"
            }
        );

        if (values[0].IsNull) return null;

        return new Score(values[0].ToString(), (long)values[1], (double)values[2]);
    }

    public async Task Clear(long leaderboard)
    {
        _logger.LogInformation("Clearing {lid} leaderboard", leaderboard);

        await _database.KeyDeleteAsync($"lb:{leaderboard}");
    }

    public async Task Delete(long leaderboard, string name)
    {
        _logger.LogInformation("Deleting score of {name} in {lid} leaderboard", name, leaderboard);

        await _database.SortedSetRemoveAsync($"lb:{leaderboard}", $"score:{leaderboard}:{name}");
    }
}