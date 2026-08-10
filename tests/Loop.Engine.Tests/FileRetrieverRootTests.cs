using AwesomeAssertions;
using Loop.Engine.Agents.Retrieval;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loop.Engine.Tests;

/// <summary>
/// Covers the resolution rule from issue #8. The merged fix arrived with no test at all —
/// its own pull request said so — so these are the assertions that were missing.
/// </summary>
public class FileRetrieverRootTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("loop-engine-root").FullName;

    private FileRetriever Retriever(string rootPath) => new(
        Options.Create(new RepositoryOptions { RootPath = rootPath, IncludeGlobs = ["**/*.cs"] }),
        NullLogger<FileRetriever>.Instance);

    [Fact]
    public void An_absolute_root_is_used_exactly_as_given()
    {
        // The pipeline depends on this: it points retrieval at the worktree by absolute
        // path, and Path.Combine returns an already-rooted second argument unchanged.
        File.WriteAllText(Path.Combine(_root, "Widget.cs"), "class Widget { }");

        var files = Retriever(_root).Retrieve(["Widget"]);

        files.Should().ContainSingle().Which.RelativePath.Should().Be("Widget.cs");
    }

    [Fact]
    public void A_relative_root_does_not_follow_the_working_directory()
    {
        // The defect in #8. Resolving against the process working directory meant the same
        // configuration pointed at different trees depending on where the engine was
        // launched from — so the same run could read a tree it was never meant to see.
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(_root);

            var retriever = Retriever("some-relative-path");

            // Anchored to the application directory, so moving the working directory to a
            // place where "some-relative-path" would resolve changes nothing.
            Directory.CreateDirectory(Path.Combine(_root, "some-relative-path"));
            File.WriteAllText(Path.Combine(_root, "some-relative-path", "Widget.cs"), "class Widget { }");

            var act = () => retriever.Retrieve(["Widget"]);

            // It looks beside the application, finds nothing there, and says so — rather
            // than silently reading whatever happens to sit under the caller's cwd.
            act.Should().Throw<DirectoryNotFoundException>();
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public void ReadFiles_resolves_against_the_same_root_as_Retrieve()
    {
        // One rule, two callers. Two copies of a path rule are two rules, and this project
        // has twice paid for code holding separate opinions about which tree it is on.
        File.WriteAllText(Path.Combine(_root, "Widget.cs"), "class Widget { }");

        var retriever = Retriever(_root);

        retriever.Retrieve(["Widget"]).Should().ContainSingle();
        retriever.ReadFiles(["Widget.cs"]).Should().ContainSingle()
            .Which.Excerpt.Should().Contain("class Widget");
    }

    [Fact]
    public void ReadFiles_refuses_a_path_that_escapes_the_root()
    {
        var act = () => Retriever(_root).ReadFiles(["../outside.cs"]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*outside the repository root*");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }
}
