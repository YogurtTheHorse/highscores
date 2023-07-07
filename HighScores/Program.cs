using System.Threading.RateLimiting;
using HighScores.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;

const string corsPolicyName = "_allowAllOrigins";
const string newLeaderboardRateLimitPolicyName = "new-leaderboards-rate-limit";

var builder = WebApplication.CreateBuilder(args);
var multiplexer = ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? string.Empty);

builder.Services.AddScoped<NameValidator>();
builder.Services.AddScoped<LeaderboardService>();
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
builder.Services.AddLogging();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        name: corsPolicyName,
        b => b
            .WithOrigins("*")
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
    );
});

builder.Services
    .AddRateLimiter(_ => _
        .AddFixedWindowLimiter(
            policyName: newLeaderboardRateLimitPolicyName,
            options =>
            {
                options.PermitLimit = 10;
                options.Window = TimeSpan.FromSeconds(1);
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                options.QueueLimit = 2;
            })
    );


var app = builder.Build();

app.UseCors(corsPolicyName);

app.MapGet(
    "/api/v1/leaderboards/new",
    async (LeaderboardService lb) => Results.Ok(await lb.CreateLeaderboard())
).RequireRateLimiting(newLeaderboardRateLimitPolicyName);

app.MapPost(
    "/api/v1/scores/{leaderboard:long}/{secret}/add/{name}/{value:long}/{time:double=0}",
    async (
        LeaderboardService lb,
        NameValidator nameValidator,
        long leaderboard,
        string secret,
        string name,
        long value,
        double time
    ) =>
    {
        if (!await lb.CheckSecret(leaderboard, secret))
        {
            return Results.StatusCode(403);
        }

        if (!nameValidator.IsValidName(name))
        {
            return Results.BadRequest();
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
            return Results.StatusCode(403);
        }

        await lb.Clear(leaderboard);

        return Results.Ok();
    }
);

app.MapDelete(
    "/api/v1/scores/{leaderboard:long}/{secret}/by/{name}",
    async (LeaderboardService lb, NameValidator nameValidator, long leaderboard, string secret, string name) =>
    {
        if (!await lb.CheckSecret(leaderboard, secret))
        {
            return Results.StatusCode(403);
        }

        if (!nameValidator.IsValidName(name))
        {
            return Results.BadRequest();
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

        return scores is null
            ? Results.NotFound()
            : Results.Ok(new {
                Scores = scores
            });
    }
);

app.MapGet(
    "/api/v1/scores/{leaderboard:long}/by/{name}",
    async (LeaderboardService lb, NameValidator nameValidator, long leaderboard, string name, [FromQuery] int? offset) =>
    {
        var score = await lb.GetScore(leaderboard, name);

        if (!nameValidator.IsValidName(name))
        {
            return Results.BadRequest();
        }

        return score is null
            ? Results.NotFound()
            : Results.Ok(score);
    }
);

app.MapGet("/", () => Results.Ok("im alive"));

app.Run();