using MediaMtx.Net.HttpApi;
using Microsoft.Extensions.Options;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection("Api"));

var app = builder.Build();

app.UseHttpMetrics();

app.MapGet("/", (ILogger<Program> logger, IOptions<ApiOptions> options) =>
{
    logger.LogInformation("Root endpoint called");
    return Results.Ok(new { message = options.Value.Greeting });
});

app.MapHealthChecks("/healthz");
app.MapMetrics();

app.Run();

public partial class Program;
