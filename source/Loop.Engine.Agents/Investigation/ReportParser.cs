using Loop.Engine.Agents.Json;

namespace Loop.Engine.Agents.Investigation;

/// <summary>
/// Extracts the investigation report from the model's reply. A thin wrapper over
/// <see cref="TolerantJson"/>, which the Planner and Coder share — three agents parsing
/// prompt-carried JSON should not mean three copies of the same brace-matching scanner.
/// </summary>
public static class ReportParser
{
    public static bool TryParse(string? text, out InvestigationReport report) =>
        TolerantJson.TryParse(text, out report);
}
