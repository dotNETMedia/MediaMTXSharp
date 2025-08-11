using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseHttpMetrics();

app.MapGet("/", (ILogger<Program> logger) =>
{
    logger.LogInformation("Root endpoint called");
    return Results.Ok(new { message = "MediaMTX.Net API" });
});

app.MapHealthChecks("/healthz");
app.MapMetrics();

app.Run();

public partial class Program;
