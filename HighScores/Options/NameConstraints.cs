namespace HighScores.Options;

public class NameConstraints
{
    public int MaxLength { get; set; } = 16;

    public int MinLength { get; set; } = 2;

    public string AllowedCharacters { get; set; } = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_ ";
}