using System.ComponentModel.DataAnnotations;

namespace HighScores.Models;

public record WebhookModel([Url] string Url);