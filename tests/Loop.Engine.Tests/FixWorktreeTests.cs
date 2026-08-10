using System.Diagnostics;
using AwesomeAssertions;
using Loop.Engine.Agents.Verification;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Loop.Engine.Tests;

public class FixWorktreeTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("loop-engine-wt-test").FullName;

    public FixWorktreeTests()
    {
        Git("init", "-q");
        Git("config", "user.email", "test@example.com");
        Git("config", "user.name", "Test");
        File.WriteAllText(Path.Combine(_repo, "A.cs"), "class A { }\n");
        Git("add", ".");
        Git("commit", "-q", "-m", "initial");
    }

    private void Git(params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = _repo,
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

    [Fact]
    public void Create_succeeds_on_a_clean_tree()
    {
        using var worktree = FixWorktree.Create(_repo, NullLogger<FixWorktreeTests>.Instance);

        Directory.Exists(worktree.Path).Should().BeTrue();
        File.Exists(Path.Combine(worktree.Path, "A.cs")).Should().BeTrue();
    }

    [Fact]
    public void Create_refuses_when_the_working_tree_is_dirty()
    {
        File.WriteAllText(Path.Combine(_repo, "A.cs"), "class A { int uncommitted; }\n");

        var act = () => FixWorktree.Create(_repo, NullLogger<FixWorktreeTests>.Instance);

        // The failure this guards against is silent and destructive: the Coder reads the
        // working tree, the build judges HEAD, and the repair loop resolves the mismatch
        // by deleting the code the compiler cannot see. It has happened once already.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*uncommitted changes*")
            .And.Message.Should().Contain("A.cs");
    }

    [Fact]
    public void Create_refuses_when_a_file_is_untracked()
    {
        File.WriteAllText(Path.Combine(_repo, "New.cs"), "class New { }\n");

        var act = () => FixWorktree.Create(_repo, NullLogger<FixWorktreeTests>.Instance);

        // An untracked file is the same hazard: present for the Coder, absent at HEAD.
        act.Should().Throw<InvalidOperationException>().WithMessage("*uncommitted changes*");
    }

    [Fact]
    public void Dispose_removes_the_worktree()
    {
        string path;
        using (var worktree = FixWorktree.Create(_repo, NullLogger<FixWorktreeTests>.Instance))
        {
            path = worktree.Path;
        }

        Directory.Exists(path).Should().BeFalse();
    }

    public void Dispose()
    {
        try { Directory.Delete(_repo, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }
}
