using Loop.Engine.Core.Abstractions;
using Loop.Engine.GitHub.Issues;
using Loop.Engine.GitHub.Publishing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Octokit;

namespace Loop.Engine.GitHub;

public static class DependencyInjection
{
    public static IServiceCollection AddLoopEngineGitHub(
        this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<GitHubOptions>()
            .Bind(configuration.GetSection(GitHubOptions.SectionName))
            .ValidateDataAnnotations()
            // Fail at startup rather than on the first poll — a missing token should not
            // look like "no issues found".
            .ValidateOnStart();

        services.AddSingleton<IGitHubClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GitHubOptions>>().Value;
            return new GitHubClient(new ProductHeaderValue(options.ProductHeader))
            {
                Credentials = new Credentials(options.Token)
            };
        });

        services
            .AddOptions<PublishingOptions>()
            .Bind(configuration.GetSection(PublishingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IIssueSource, GitHubIssueSource>();
        services.AddSingleton<IInFlightFixes, GitHubInFlightFixes>();
        services.AddSingleton<IGitPublisher, GitPublisher>();
        services.AddSingleton<IPullRequestPublisher, PullRequestPublisher>();

        return services;
    }
}
