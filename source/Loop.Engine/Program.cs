using Loop.Engine.GitHub;
using Loop.Engine.Worker.Pipeline;

var builder = Host.CreateApplicationBuilder(args);

// User-secrets keeps the GitHub token out of the repo during local development.
builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Services.AddLoopEngineGitHub(builder.Configuration);
builder.Services.AddHostedService<IssuePollingService>();

var host = builder.Build();
await host.RunAsync();

/// <summary>Named so <c>AddUserSecrets&lt;Program&gt;</c> has a type to anchor to.</summary>
public partial class Program;
