using System.Diagnostics;
using System.Security.Principal;

namespace BytePulse.Core.Hosting;

/// <summary>
/// Whether the process is running with administrator rights, and how to restart with
/// them.
/// </summary>
/// <remarks>
/// A disk usage tool under-reports when it is not allowed to read a folder, and on a
/// system volume that is a meaningful amount of space. Neither front end elevates on its
/// own; they offer the choice when a scan turns up something it could not read.
/// </remarks>
public static class Elevation
{
    /// <summary>Whether this process is running as an administrator.</summary>
    public static bool IsElevated
    {
        get
        {
            try
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Starts the application again with administrator rights.
    /// </summary>
    /// <param name="scanPath">A path for the new instance to scan, or null.</param>
    /// <returns>False when the prompt was declined or the restart failed.</returns>
    public static bool RestartElevated(string? scanPath)
    {
        string? executable = Environment.ProcessPath;

        if (string.IsNullOrEmpty(executable))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory,
        };

        if (!string.IsNullOrWhiteSpace(scanPath))
        {
            startInfo.ArgumentList.Add("--scan");
            startInfo.ArgumentList.Add(scanPath);
        }

        try
        {
            Process.Start(startInfo);
            return true;
        }
        catch (Exception)
        {
            // The common case is the user declining the prompt, which is not an error.
            return false;
        }
    }
}
