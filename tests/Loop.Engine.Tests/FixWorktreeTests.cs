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
        Git("init", "-q", "-b", "main");
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

    private FixWorktree Create() =>
        FixWorktree.Create(_repo, "main", "origin", NullLogger<FixWorktreeTests>.Instance);

    [Fact]
    public void Create_checks_out_the_base_branch()
    {
        using var worktree = Create();

        Directory.Exists(worktree.Path).Should().BeTrue();
        File.Exists(Path.Combine(worktree.Path, "A.cs")).Should().BeTrue();
    }

    [Fact]
    public void Create_uses_the_base_branch_not_the_current_one()
    {
        // The defect this replaced: branching from HEAD dragged every unmerged commit of
        // the developer's branch into the fix PR — a two-file fix arrived as 24 files.
        Git("checkout", "-q", "-b", "feature/unrelated");
        File.WriteAllText(Path.Combine(_repo, "Unrelated.cs"), "class Unrelated { }\n");
        Git("add", ".");
        Git("commit", "-q", "-m", "work in progress");

        using var worktree = Create();

        File.Exists(Path.Combine(worktree.Path, "Unrelated.cs")).Should().BeFalse();
        File.Exists(Path.Combine(worktree.Path, "A.cs")).Should().BeTrue();
    }

    [Fact]
    public void Create_succeeds_even_when_the_working_tree_is_dirty()
    {
        // Uncommitted work used to be a hard failure, because the Coder read the working
        // tree while the build ran against HEAD. Every stage now shares one worktree, so
        // the developer's local edits are simply irrelevant.
        File.WriteAllText(Path.Combine(_repo, "A.cs"), "class A { int uncommitted; }\n");

        using var worktree = Create();

        File.ReadAllText(Path.Combine(worktree.Path, "A.cs")).Should().NotContain("uncommitted");
    }

    [Fact]
    public void Apply_writes_edits_into_the_worktree_only()
    {
        using var worktree = Create();

        worktree.Apply([new Loop.Engine.Core.Model.CodeEdit("A.cs", "class A { int fixed_; }\n")]);

        File.ReadAllText(Path.Combine(worktree.Path, "A.cs")).Should().Contain("fixed_");
        File.ReadAllText(Path.Combine(_repo, "A.cs")).Should().NotContain("fixed_");
    }

    [Fact]
    public void Dispose_removes_the_worktree()
    {
        string path;
        using (var worktree = Create())
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
