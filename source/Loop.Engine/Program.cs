using Loop.Engine.Agents;
using Loop.Engine.Agents.Retrieval;
using Loop.Engine.Agents.Verification;
using Loop.Engine.GitHub;
using Loop.Engine.Worker.Pipeline;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// User-secrets keeps the GitHub token and Anthropic key out of the repo during local
// development. The Anthropic key may also come from ANTHROPIC_API_KEY in the environment.
builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Services.Configure<PipelineOptions>(
    builder.Configuration.GetSection(PipelineOptions.SectionName));

builder.Services.AddLoopEngineGitHub(builder.Configuration);
builder.Services.AddLoopEngineAgents(builder.Configuration);
builder.Services.AddHostedService<IssuePollingService>();

var host = builder.Build();

try
{
    // Before the first model call: a verification stage pointed at the wrong test project
    // cannot fail honestly, so it must not be allowed to start.
    var reason = TestProjectGuard.Check(
        host.Services.GetRequiredService<IOptions<PipelineOptions>>().Value.VerifyFix,
        host.Services.GetRequiredService<IOptions<RepositoryOptions>>().Value.RootPath,
        host.Services.GetRequiredService<IOptions<VerificationOptions>>().Value.TestProject);

    if (reason is not null)
    {
        throw new OptionsValidationException(
            nameof(VerificationOptions), typeof(VerificationOptions), [reason]);
    }

    await host.RunAsync();
}
catch (OptionsValidationException ex)
{
    // A missing setting is an operator error, not a crash. Dumping forty frames of DI
    // resolver internals buries the one line that says what to do — and trains people to
    // stop reading errors, which is how the next real stack trace gets skimmed past.
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Configuration error in {ex.OptionsType.Name}:");
    foreach (var failure in ex.Failures)
    {
        Console.Error.WriteLine($"  - {failure}");
    }
    Console.Error.WriteLine();

    return 1;
}

return 0;

/// <summary>Named so <c>AddUserSecrets&lt;Program&gt;</c> has a type to anchor to.</summary>
public partial class Program;
