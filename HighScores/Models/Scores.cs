namespace HighScores.Models;

public record Score(string Name, long Value, double Time, long? Place);

public record BaseLeaderBoard
{
    public required long Id { get; init; }

    public required ScoresDirection Direction { get; init; }

    public required ScoresOrderBy OrderBy { get; init; }
};

public record FullLeaderBoard : BaseLeaderBoard
{
    public required string Secret { get; init; }

    public required string PrivateSecret { get; init; }

    public string Webhook { get; init; }
};

public enum ScoresDirection
{
    Descending = -1,
    Ascending = 1
}

public enum ScoresOrderBy
{
    Value = 0,
    Time = 1
}