namespace HighScores.Models;

public record Score(string Name, long Value, double Time);

public record LeaderBoard(long Id, string Secret);