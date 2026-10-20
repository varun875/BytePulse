using System.Reflection;
using System.Runtime.InteropServices;

namespace BytePulse.Cli;

/// <summary>The version of the tool.</summary>
internal static class VersionInformation
{
    /// <summary>
    /// The version, the architecture and the runtime, which is what is useful when
    /// somebody reports a problem.
    /// </summary>
    public static string Text
    {
        get
        {
            Assembly assembly = typeof(VersionInformation).Assembly;

            string version = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
                ?? assembly.GetName().Version?.ToString(3)
                ?? "unknown";

            string runtime = RuntimeInformation.FrameworkDescription;
            string architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

            return $"BytePulse {version} ({architecture}, {runtime})";
        }
    }
}
