using Loop.Engine.Agents;
using Loop.Engine.GitHub;
using Loop.Engine.Worker.Pipeline;

var builder = Host.CreateApplicationBuilder(args);

// User-secrets keeps the GitHub token and Anthropic key out of the repo during local
// development. The Anthropic key may also come from ANTHROPIC_API_KEY in the environment.
builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Services.AddLoopEngineGitHub(builder.Configuration);
builder.Services.AddLoopEngineAgents(builder.Configuration);
builder.Services.AddHostedService<IssuePollingService>();

var host = builder.Build();
await host.RunAsync();

/// <summary>Named so <c>AddUserSecrets&lt;Program&gt;</c> has a type to anchor to.</summary>
public partial class Program;
