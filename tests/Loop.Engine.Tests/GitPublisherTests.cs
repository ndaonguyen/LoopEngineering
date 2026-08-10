using System.Diagnostics;
using AwesomeAssertions;
using Loop.Engine.GitHub.Publishing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loop.Engine.Tests;

/// <summary>
/// Real git against a bare repo on disk used as the remote — no network, no token, no
/// GitHub. Push is genuinely exercised rather than faked, which is the only way to know the
/// refuse-to-force-push guard actually holds.
/// </summary>
public class GitPublisherTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("loop-engine-publish").FullName;
    private readonly string _work;

    public GitPublisherTests()
    {
        var remote = Path.Combine(_root, "remote.git");
        _work = Path.Combine(_root, "work");

        Directory.CreateDirectory(_work);
        Git(_root, "init", "-q", "--bare", "remote.git");
        Git(_work, "init", "-q", "-b", "main");
        Git(_work, "config", "user.email", "test@example.com");
        Git(_work, "config", "user.name", "Test");
        File.WriteAllText(Path.Combine(_work, "A.cs"), "class A { }\n");
        Git(_work, "add", ".");
        Git(_work, "commit", "-q", "-m", "initial");
        Git(_work, "remote", "add", "origin", remote);
        Git(_work, "push", "-q", "-u", "origin", "main");
    }

    private static void Git(string cwd, params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info)!;
        process.WaitForExit();
    }

    private static GitPublisher Publisher() => new(
        Options.Create(new PublishingOptions()),
        NullLogger<GitPublisher>.Instance);

    [Fact]
    public async Task PublishBranchAsync_pushes_a_new_branch()
    {
        File.WriteAllText(Path.Combine(_work, "A.cs"), "class A { int x; }\n");

        await Publisher().PublishBranchAsync(_work, "fix/8-something", "fix: something");

        var branches = RunCapture(_work, "ls-remote", "--heads", "origin");
        branches.Should().Contain("fix/8-something");
    }

    [Fact]
    public async Task PublishBranchAsync_refuses_a_branch_that_already_exists_on_the_remote()
    {
        File.WriteAllText(Path.Combine(_work, "A.cs"), "class A { int x; }\n");
        await Publisher().PublishBranchAsync(_work, "fix/8-taken", "fix: first run");

        Git(_work, "checkout", "-q", "main");
        File.WriteAllText(Path.Combine(_work, "A.cs"), "class A { int y; }\n");

        var act = () => Publisher().PublishBranchAsync(_work, "fix/8-taken", "fix: second run");

        // Force-pushing would silently replace a pull request someone may be mid-review on.
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("already exists");
    }

    [Theory]
    [InlineData("main")]
    [InlineData("master")]
    [InlineData("MAIN")]
    public async Task PublishBranchAsync_refuses_to_publish_to_a_protected_branch(string branch)
    {
        var act = () => Publisher().PublishBranchAsync(_work, branch, "fix: nope");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("never writes to a protected branch");
    }

    [Fact]
    public async Task PublishBranchAsync_refuses_an_empty_branch_name()
    {
        var act = () => Publisher().PublishBranchAsync(_work, "  ", "fix: nope");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PublishBranchAsync_commits_the_working_changes()
    {
        File.WriteAllText(Path.Combine(_work, "A.cs"), "class A { int committed; }\n");

        await Publisher().PublishBranchAsync(_work, "fix/8-commits", "fix: commit everything");

        RunCapture(_work, "status", "--porcelain").Trim().Should().BeEmpty();
        RunCapture(_work, "log", "-1", "--pretty=%s").Should().Contain("fix: commit everything");
    }

    private static string RunCapture(string cwd, params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }
}
