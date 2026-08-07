using AwesomeAssertions;
using Loop.Engine.Agents.Coding;
using Loop.Engine.Agents.Retrieval;
using Xunit;

namespace Loop.Engine.Tests;

public class EditWorkspaceTests
{
    private static RetrievedFile File(string path, string contents) => new(path, contents, [path]);

    [Fact]
    public void Create_stages_originals_and_a_copy_to_edit()
    {
        using var workspace = EditWorkspace.Create([File("src/A.cs", "class A { }")]);

        System.IO.File.Exists(Path.Combine(workspace.OriginalDirectory, "src", "A.cs")).Should().BeTrue();
        System.IO.File.Exists(Path.Combine(workspace.EditedDirectory, "src", "A.cs")).Should().BeTrue();
    }

    [Fact]
    public void Write_updates_only_the_edited_copy()
    {
        using var workspace = EditWorkspace.Create([File("src/A.cs", "class A { }")]);

        workspace.Write("src/A.cs", "class A { int x; }");

        System.IO.File.ReadAllText(Path.Combine(workspace.EditedDirectory, "src", "A.cs"))
            .Should().Contain("int x");
        System.IO.File.ReadAllText(Path.Combine(workspace.OriginalDirectory, "src", "A.cs"))
            .Should().NotContain("int x");
    }

    [Fact]
    public void Write_preserves_the_originals_line_endings()
    {
        // A '\n' replacement over a '\r\n' original marks every line changed and the diff
        // becomes worthless. Third time line endings have bitten this project.
        using var workspace = EditWorkspace.Create([File("src/A.cs", "class A\r\n{\r\n}\r\n")]);

        workspace.Write("src/A.cs", "class A\n{\n    int x;\n}\n");

        var written = System.IO.File.ReadAllText(Path.Combine(workspace.EditedDirectory, "src", "A.cs"));
        written.Should().Contain("\r\n");
        written.Should().NotMatchRegex("(?<!\r)\n");
    }

    [Fact]
    public void Write_refuses_a_path_outside_the_allow_list()
    {
        using var workspace = EditWorkspace.Create([File("src/A.cs", "class A { }")]);

        var act = () => workspace.Write("src/Other.cs", "class Other { }");

        act.Should().Throw<InvalidOperationException>().WithMessage("*not among the allow-listed files*");
    }

    [Fact]
    public void Write_refuses_a_traversal_path()
    {
        using var workspace = EditWorkspace.Create([File("src/A.cs", "class A { }")]);

        var act = () => workspace.Write("../../../etc/passwd", "pwned");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Dispose_deletes_the_workspace()
    {
        string root;
        using (var workspace = EditWorkspace.Create([File("src/A.cs", "class A { }")]))
        {
            root = workspace.Root;
            Directory.Exists(root).Should().BeTrue();
        }

        // A failed run must leave nothing behind to clean up by hand.
        Directory.Exists(root).Should().BeFalse();
    }
}
