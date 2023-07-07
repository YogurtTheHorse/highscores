using HighScores.Services;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

const string CorsPolicyName = "_allowAllOrigins";

var builder = WebApplication.CreateBuilder(args);
var multiplexer = ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? string.Empty);

builder.Services.AddScoped<LeaderboardService>();
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        name: CorsPolicyName,
        b => b
            .WithOrigins("*")
            .AllowAnyOrigin() 
            .AllowAnyMethod()
            .AllowAnyHeader()
    );
});
builder.Services.AddAuthentication();


var app = builder.Build();

app.UseCors(CorsPolicyName);

app.MapGet(
    "/api/v1/leaderboards/new",
    async (LeaderboardService lb) => Results.Ok(await lb.CreateLeaderboard())
);

app.MapPost(
    "/api/v1/scores/{leaderboard:long}/{secret}/add/{name}/{value:long}/{time:double=0}",
    async (LeaderboardService lb, long leaderboard, string secret, string name, long value, double time) =>
    {
        if (!await lb.CheckSecret(leaderboard, secret))
        {
            return Results.Forbid();
        }

        await lb.AddScore(leaderboard, name, value, time);

        return Results.Ok();
    }
);

app.MapDelete(
    "/api/v1/scores/{leaderboard:long}/{secret}",
    async (LeaderboardService lb, long leaderboard, string secret) =>
    {
        if (!await lb.CheckSecret(leaderboard, secret))
        {
            return Results.Forbid();
        }

        await lb.Clear(leaderboard);

        return Results.Ok();
    }
);

app.MapDelete(
    "/api/v1/scores/{leaderboard:long}/{secret}/by/{name}",
    async (LeaderboardService lb, long leaderboard, string secret, string name) =>
    {
        if (!await lb.CheckSecret(leaderboard, secret))
        {
            return Results.Forbid();
        }

        await lb.Delete(leaderboard, name);

        return Results.Ok();
    }
);

app.MapGet(
    "/api/v1/scores/{leaderboard:long}/{count:int?}",
    async (LeaderboardService lb, long leaderboard, int? count, [FromQuery] int? offset) =>
    {
        var scores = await lb.GetScores(leaderboard, count ?? -1, offset ?? 0);

        return Results.Ok(new {
            Scores = scores
        });
    }
);

app.MapGet(
    "/api/v1/scores/{leaderboard:long}/by/{name}",
    async (LeaderboardService lb, long leaderboard, string name, [FromQuery] int? offset) =>
    {
        var score = await lb.GetScore(leaderboard, name);

        return score is null
            ? Results.NotFound() 
            : Results.Ok(score);
    }
);

app.MapGet("/", () => Results.Ok("im alive"));

app.Run();