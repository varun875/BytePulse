using System.Reflection;

namespace BytePulse.Cli;

/// <summary>The version of the tool.</summary>
internal static class VersionInformation
{
    public static string Text
    {
        get
        {
            Version? version = typeof(VersionInformation).Assembly.GetName().Version;
            return version is null ? "unknown" : version.ToString(3);
        }
    }
}
