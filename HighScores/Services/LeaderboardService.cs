using StackExchange.Redis;

namespace HighScores.Services;

public record Score(string Name, long Value, double Time);

public record LeaderBoard(long Id, string Secret);

public class LeaderboardService
{
    private readonly IDatabase _database;

    public LeaderboardService(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public async Task<LeaderBoard> CreateLeaderboard()
    {
        var id = await _database.StringIncrementAsync("lb-counter");
        var secret = Guid.NewGuid().ToString().Replace("-", string.Empty);

        await _database.HashSetAsync(
            $"lb-info:{id}",
            new HashEntry[] {
                new("id", id), new("secret", secret)
            });

        return new LeaderBoard(id, secret);
    }

    public async Task<bool> CheckSecret(long leaderboard, string secret)
    {
        var saved = await _database.HashGetAsync($"lb-info:{leaderboard}", "secret");

        return saved.ToString() == secret;
    }

    public async Task AddScore(long leaderboard, string name, long value, double time)
    {
        var scoreId = $"score:{leaderboard}:{name}";

        var updatedScore = await _database.SortedSetUpdateAsync($"lb:{leaderboard}", scoreId, value, SortedSetWhen.GreaterThan);

        if (updatedScore)
        {
            await _database.HashSetAsync(scoreId,
                new HashEntry[] {
                    new("name", name), new("value", value), new("time", time)
                });
        }
    }

    public async Task<Score[]> GetScores(long leaderboard, int count, int offset)
    {
        var scoresIds = await _database.SortedSetRangeByScoreAsync(
            $"lb:{leaderboard}",
            skip: offset,
            take: count,
            order: Order.Descending
        );

        List<Score> scores = new();

        foreach (var scoreId in scoresIds)
        {
            scores.Add(await GetScore(scoreId.ToString()));
        }

        return scores.ToArray();
    }

    public async Task<Score> GetScore(long leaderboard, string name) => await GetScore($"score:{leaderboard}:{name}");

    private async Task<Score> GetScore(string key)
    {
        var values = await _database.HashGetAsync(
            key,
            new RedisValue[] {
                "name", "value", "time"
            }
        );

        return new Score(values[0].ToString(), (long)values[1], (double)values[2]);
    }

    public async Task Clear(long leaderboard)
    {
        await _database.KeyDeleteAsync($"lb:{leaderboard}");
    }

    public async Task Delete(long leaderboard, string name)
    {
        await _database.SortedSetRemoveAsync($"lb:{leaderboard}", $"score:{leaderboard}:{name}");
    }
}