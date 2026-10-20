# Third-party notices

BytePulse itself is licensed under the MIT licence in [LICENSE](LICENSE).

The engine (`src/BytePulse.Core`) and the command line tool
(`src/BytePulse.Cli`) have no third-party dependencies beyond the .NET base class
library and the Win32 API.

The application (`src/BytePulse.App`) uses the following packages. They are not
part of the engine and are not redistributed as part of it.

| Package | Licence | Used for |
| --- | --- | --- |
| Microsoft.WindowsAppSDK | MIT | WinUI 3, the application framework |
| Microsoft.Windows.SDK.BuildTools | Microsoft | MSIX packaging tooling |
| Microsoft.Windows.SDK.BuildTools.WinApp | Microsoft | running the packaged application |
| CommunityToolkit.Mvvm | MIT | observable properties and commands |

The command line tool uses:

| Package | Licence | Used for |
| --- | --- | --- |
| System.CommandLine | MIT | argument parsing |

The tests and benchmarks use:

| Package | Licence | Used for |
| --- | --- | --- |
| xunit, xunit.runner.visualstudio | Apache-2.0 | unit tests |
| Microsoft.NET.Test.Sdk | MIT | running the tests |
| coverlet.collector | MIT | coverage |
| BenchmarkDotNet | MIT | benchmarks |

## Note on assertions

BytePulse uses plain `xunit` assertions. It deliberately does not use
FluentAssertions, whose current versions are licensed for commercial use only.
