using StackExchange.Redis;

namespace HighScores.Services;

public record Score(string Name, long Value, double Time);

public class LeaderboardService
{
    private readonly IDatabase _database;

    public LeaderboardService(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public async Task AddScore(string leaderboard, string name, long value, double time)
    {
        var scoreId = $"score:{leaderboard}:{name}";

        var updatedScore = await _database.SortedSetAddAsync($"lb:{leaderboard}", scoreId, value);
        
        if (updatedScore)
        {
            await _database.HashSetAsync(scoreId,
                new HashEntry[] {
                    new("name", name), new("value", value), new("time", time)
                });
        }
    }

    public async Task<Score[]> GetScores(string leaderboard, int count, int offset)
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

    public async Task<Score> GetScore(string leaderboard, string name) => await GetScore($"score:{leaderboard}:{name}");

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

    public async Task Clear(string leaderboard)
    {
        await _database.KeyDeleteAsync($"lb:{leaderboard}");
    }

    public async Task Delete(string leaderboard, string name)
    {
        await _database.SortedSetRemoveAsync($"lb:{leaderboard}", $"score:{leaderboard}:{name}");
    }
}