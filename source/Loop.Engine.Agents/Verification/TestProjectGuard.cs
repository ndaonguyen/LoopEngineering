namespace Loop.Engine.Agents.Verification;

/// <summary>
/// Refuses a verification run whose test project does not exist in the repository being fixed.
///
/// <see cref="VerificationOptions.TestProject"/> defaults to this engine's own suite, which is
/// right while the engine fixes itself and silently wrong the moment it is pointed elsewhere: a
/// fix to another repository would be "verified" by running tests that never touch it, pass
/// regardless, earn a review, and open a pull request carrying the appearance of having been
/// checked.
///
/// That is the one failure mode here that looks like success, so it is worth failing loudly
/// before the first model call rather than discovering it in a merged PR.
/// </summary>
public static class TestProjectGuard
{
    /// <summary>
    /// Returns <see langword="null"/> when the configuration is coherent, otherwise the reason,
    /// written for whoever has to fix it.
    /// </summary>
    /// <param name="verifyFix">Whether the verification stage is enabled at all.</param>
    /// <param name="repositoryRoot">Configured <c>Repository:RootPath</c>.</param>
    /// <param name="testProject">Configured <c>Verification:TestProject</c>.</param>
    public static string? Check(bool verifyFix, string repositoryRoot, string? testProject)
    {
        // Nothing is built or run, so an unusable test project cannot mislead anyone yet.
        if (!verifyFix)
        {
            return null;
        }

        // Empty is a deliberate choice — it means "run the whole solution" and is the escape
        // hatch for a target repository with no single test project.
        if (string.IsNullOrWhiteSpace(testProject))
        {
            return null;
        }

        var root = Path.GetFullPath(repositoryRoot);
        var full = Path.GetFullPath(Path.Combine(root, testProject));

        if (File.Exists(full))
        {
            return null;
        }

        return
            $"Verification:TestProject is '{testProject}', which does not exist under " +
            $"Repository:RootPath ('{root}'). Looked for '{full}'. " +
            "Set it to the target repository's test project — left pointing elsewhere, a fix " +
            "is 'verified' by a suite that never exercises it, which passes regardless and " +
            "produces a pull request that only looks reviewed. " +
            "Set it to empty to run the whole solution instead.";
    }
}
