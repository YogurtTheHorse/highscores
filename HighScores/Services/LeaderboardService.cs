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

    public async Task<FullLeaderBoard> CreateLeaderboard(
        ScoresDirection scoresDirection = ScoresDirection.Descending,
        ScoresOrderBy orderBy = ScoresOrderBy.Value
    )
    {
        _logger.LogInformation("Creating new leaderboard...");

        var id = await _database.StringIncrementAsync("lb-counter");
        var secret = Guid.NewGuid().ToString().Replace("-", string.Empty);
        var privateSecret = Guid.NewGuid().ToString().Replace("-", string.Empty);

        await _database.HashSetAsync(
            $"lb-info:{id}",
            [
                new("id", id),
                new("secret", secret),
                new("private-secret", privateSecret),
                new("direction", scoresDirection.ToString()),
                new("order-by", orderBy.ToString())
            ]);

        _logger.LogInformation("Created leaderboard with ID {id}...", id);

        return new FullLeaderBoard
        {
            Id = id,
            Secret = secret,
            PrivateSecret = privateSecret,
            Direction = scoresDirection,
            OrderBy = orderBy
        };
    }

    public async Task<BaseLeaderBoard?> GetBaseLeaderboard(long leaderboard)
    {
        _logger.LogInformation("Getting leaderboard {lid}...", leaderboard);

        var values = await _database.HashGetAsync(
            $"lb-info:{leaderboard}",
            [
                "id", "direction", "order-by"
            ]
        );


        return new BaseLeaderBoard
        {
            Id = leaderboard,
            Direction = values[1].HasValue
                ? Enum.Parse<ScoresDirection>(values[1].ToString())
                : ScoresDirection.Descending,
            OrderBy = values[2].HasValue
                ? Enum.Parse<ScoresOrderBy>(values[2].ToString())
                : ScoresOrderBy.Value
        };
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

    public async Task<string?> GetWebhook(long leaderboard) =>
        await _database.HashGetAsync($"lb-info:{leaderboard}", "webhook") switch
        {
            { IsNull: false } webhookValue => webhookValue.ToString(),
            _ => null
        };

    public async Task<string> GetAppendSecret(long leaderboard) =>
        (await _database.HashGetAsync($"lb-info:{leaderboard}", "secret")).ToString();

    public async Task<string> GetModifySecret(long leaderboard) =>
        await _database.HashExistsAsync($"lb-info:{leaderboard}", "private-secret")
            ? (await _database.HashGetAsync($"lb-info:{leaderboard}", "private-secret")).ToString()
            : await GetAppendSecret(leaderboard);

    public async Task<bool> CheckSecret(long leaderboard, string secret, bool forModify)
    {
        var token = await (forModify
            ? GetModifySecret(leaderboard)
            : GetAppendSecret(leaderboard));

        return token == secret;
    }

    public async Task<long> AddScore(long leaderboard, string name, long value, double time)
    {
        var leaderboardInfo = await GetBaseLeaderboard(leaderboard) ??
                              throw new KeyNotFoundException("Leaderboard not found");

        var scoreId = $"score:{leaderboard}:{name}";
        var rank = leaderboardInfo.OrderBy switch
        {
            ScoresOrderBy.Value => value,
            ScoresOrderBy.Time => time,
            _ => value
        };
        _logger.LogInformation(
            "Updating score of leaderboard {lid} for {name} to {rank}...",
            leaderboard,
            name,
            rank
        );

        var updatedScore = await _database.SortedSetUpdateAsync(
            $"lb:{leaderboard}",
            scoreId,
            rank,
            leaderboardInfo.Direction switch
            {
                ScoresDirection.Ascending => SortedSetWhen.LessThan,
                _ => SortedSetWhen.GreaterThan
            }
        );

        if (updatedScore)
        {
            _logger.LogInformation(
                "Updating player info of leaderboard {lid} for {name}...",
                leaderboard,
                name
            );

            await _database.HashSetAsync(scoreId,
            [
                new("name", name),
                new("value", value),
                new("time", time)
            ]);
        }

        var place = await _database.SortedSetRankAsync(
            $"lb:{leaderboard}",
            scoreId,
            leaderboardInfo.Direction switch
            {
                ScoresDirection.Descending => Order.Descending,
                _ => Order.Ascending
            }
        );

        return place.HasValue
            ? place.Value + 1
            : 0;
    }

    public async Task<Score[]?> GetScores(long leaderboard, int count, int skip, bool reverse = false)
    {
        var leaderboardInfo = await GetBaseLeaderboard(leaderboard);
        if (leaderboardInfo is null)
            return null;

        var scoresIds = await _database.SortedSetRangeByScoreAsync(
            $"lb:{leaderboard}",
            skip: skip,
            take: count,
            order: (leaderboardInfo.Direction, reverse) switch
            {
                (ScoresDirection.Descending, false) => Order.Descending,
                (ScoresDirection.Ascending, false) => Order.Ascending,
                (ScoresDirection.Descending, true) => Order.Ascending,
                (ScoresDirection.Ascending, true) => Order.Descending,
                _ => Order.Descending
            }
        );

        List<Score> scores = [];

        for (var i = 0; i < scoresIds.Length; i++)
        {
            var scoreId = scoresIds[i];
            var score = await GetScore(scoreId.ToString());

            if (score is null) continue;

            scores.Add(score with
            {
                Place = skip + i + 1
            });
        }

        return scores.ToArray();
    }

    public async Task<Score?> GetScore(long leaderboard, string name) =>
        await GetScore($"score:{leaderboard}:{name}") switch
        {
            { } score => score with
            {
                Place = 1 + await _database.SortedSetRankAsync($"lb:{leaderboard}", $"score:{leaderboard}:{name}")
            },
            null => null
        };

    private async Task<Score?> GetScore(string key) =>
        await _database.HashGetAsync(key, ["name", "value", "time"]) switch
        {
            [{ IsNull: true }, ..] => null,
            [var name, var value, var time] => new Score(name.ToString(), (long)value, (double)time, null),
            _ => null
        };

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