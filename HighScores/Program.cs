using HighScores.Services;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var multiplexer = ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? string.Empty);

builder.Services.AddScoped<LeaderboardService>();
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

var app = builder.Build();

app.MapPost(
    "/api/v1/scores/{leaderboard}/add/{name}/{value:long}/{time:double=0}",
    async (LeaderboardService lb, string leaderboard, string name, long value, double? time) =>
    {
        await lb.AddScore(leaderboard, name, value, time ?? 0);

        return Results.Ok();
    }
);

app.MapPost(
    "/api/v1/scores/{leaderboard}/clear",
    async (LeaderboardService lb, string leaderboard) =>
    {
        await lb.Clear(leaderboard);

        return Results.Ok();
    }
);

app.MapDelete(
    "/api/v1/scores/{leaderboard}/delete/{name}",
    async (LeaderboardService lb, string leaderboard, string name) =>
    {
        await lb.Delete(leaderboard, name);

        return Results.Ok();
    }
);

app.MapGet(
    "/api/v1/scores/{leaderboard}/{count:int?}",
    async (LeaderboardService lb, string leaderboard, int? count, [FromQuery] int? offset) =>
    {
        var scores = await lb.GetScores(leaderboard, count ?? -1 , offset ?? 0);

        return Results.Ok(new {
            Scores = scores
        });
    }
);

app.MapGet(
    "/api/v1/scores/{leaderboard}/by/{name}",
    async (LeaderboardService lb, string leaderboard, string name, [FromQuery] int? offset) =>
    {
        var score = await lb.GetScore(leaderboard, name);

        return Results.Ok(score);
    }
);

app.MapGet("/", () => Results.Ok("im alive"));

app.Run();