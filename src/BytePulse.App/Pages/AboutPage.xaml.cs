using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;

namespace BytePulse.Pages;

/// <summary>What BytePulse is, and the licence it is under.</summary>
public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = DescribeVersion();
    }

    private static string DescribeVersion()
    {
        Assembly assembly = typeof(AboutPage).Assembly;

        string version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? assembly.GetName().Version?.ToString(3)
            ?? "unknown";

        string architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

        return $"Version {version} ({architecture}, {RuntimeInformation.FrameworkDescription})";
    }
}
