using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace V0idedDecomp.ViewModels;

public partial class GameMakerViewModel : ObservableObject
{
    [ObservableProperty]
    private string _inputFilePath = "";

    [ObservableProperty]
    private string _outputDirectory = "";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private ObservableCollection<string> _logLines = new();

    private string GetDocumentsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "v0ided-decomp",
            "gamemaker"
        );
    }

    public GameMakerViewModel()
    {
        OutputDirectory = GetDocumentsPath();
    }

    [RelayCommand]
    private async Task Decompile()
    {
        if (string.IsNullOrWhiteSpace(InputFilePath))
        {
            StatusText = "Error: No input file selected";
            return;
        }

        var fullPath = InputFilePath.Trim();

        if (!File.Exists(fullPath))
        {
            StatusText = "Error: File does not exist";
            return;
        }

        IsProcessing = true;
        StatusText = "Processing...";
        LogLines.Clear();

        try
        {
            var cliPath = FindGameMakerCLI();
            if (string.IsNullOrEmpty(cliPath))
            {
                StatusText = "Error: UnderAnalyzer CLI not found";
                LogLines.Add("ERROR: Could not find UnderAnalyzer CLI");
                IsProcessing = false;
                return;
            }

            LogLines.Add("UnderAnalyzer CLI: " + cliPath);

            var gameName = Path.GetFileNameWithoutExtension(fullPath);
            var outputPath = Path.Combine(OutputDirectory, gameName);

            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }

            Directory.CreateDirectory(outputPath);

            LogLines.Add("Decompiling: " + fullPath);
            LogLines.Add("Output: " + outputPath);

            var args = $"decompile \"{fullPath}\" -o \"{outputPath}\" -f";

            if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                await RunNativeAsync(cliPath, args);
            }
            else
            {
                var winePath = FindWine();
                if (string.IsNullOrEmpty(winePath))
                {
                    StatusText = "Error: wine not found";
                    LogLines.Add("ERROR: wine not installed");
                    IsProcessing = false;
                    return;
                }
                await RunWithWineAsync(winePath, cliPath, args);
            }
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
            LogLines.Add("Exception: " + ex);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private string? FindGameMakerCLI()
    {
        var possibleBasePaths = new[]
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Path.GetFullPath("."),
            Path.GetFullPath("../"),
            Path.GetFullPath("../../"),
            Path.GetFullPath("../../../"),
            Path.GetFullPath("../../../../"),
            "/Volumes/Seagate/v0ided-decomp",
            "/Volumes/Seagate/v0ided-decomp/publish",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "v0ided-decomp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "v0ided-decomp", "publish")
        };

        foreach (var baseDir in possibleBasePaths)
        {
            if (string.IsNullOrEmpty(baseDir)) continue;

            // On macOS/Linux, use the DLL and run with dotnet command
            if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                var cliDll = Path.Combine(baseDir, "gamemaker", "UnderAnalyzerCLI.dll");
                if (File.Exists(cliDll))
                {
                    return cliDll;
                }
            }
            else
            {
                var cliExe = Path.Combine(baseDir, "gamemaker", "UnderAnalyzerCLI.exe");
                if (File.Exists(cliExe))
                {
                    return cliExe;
                }
            }
        }

        return null;
    }

    private async Task RunNativeAsync(string fileName, string arguments)
    {
        string fileToRun;
        string args;

        // On macOS/Linux, use dotnet to run the DLL
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            // Try to find dotnet in common locations
            var dotnetPaths = new[]
            {
                "dotnet",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet"),
                "/usr/local/share/dotnet/dotnet",
                "/opt/dotnet/dotnet"
            };
            
            fileToRun = dotnetPaths.FirstOrDefault(p => File.Exists(p) || FindInPath(p)) ?? "dotnet";
            args = $"\"{fileName}\" {arguments}";
        }
        else
        {
            fileToRun = fileName;
            args = arguments;
        }

        LogLines.Add("Running: " + fileToRun + " " + args);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileToRun,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            Environment = 
            {
                // Set DOTNET_ROOT to help find the runtime
                ["DOTNET_ROOT"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet"),
                ["DOTNET_INSTALL_DIR"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet")
            }
        };

        using var process = new Process { StartInfo = startInfo };

        bool FindInPath(string cmd)
        {
            var whichInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = cmd,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(whichInfo);
            p?.WaitForExit();
            return p?.ExitCode == 0;
        }

        process.OutputDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => LogLines.Add(e.Data));
            }
        };

        process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => LogLines.Add("[ERR] " + e.Data));
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            StatusText = "Success!";
            LogLines.Add("Done!");
        }
        else
        {
            StatusText = "Exit code: " + process.ExitCode;
        }
    }

    private async Task RunWithWineAsync(string winePath, string exePath, string arguments)
    {
        LogLines.Add("Running with wine: " + winePath + " " + exePath + " " + arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = winePath,
            Arguments = $"\"{exePath}\" {arguments}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => LogLines.Add(e.Data));
            }
        };

        process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => LogLines.Add("[ERR] " + e.Data));
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            StatusText = "Success!";
            LogLines.Add("Done!");
        }
        else
        {
            StatusText = "Exit code: " + process.ExitCode;
        }
    }

    private string? FindWine()
    {
        var winePaths = new[]
        {
            "/opt/homebrew/bin/wine",
            "/usr/local/bin/wine",
            "/opt/homebrew/bin/wine64",
            "/usr/local/bin/wine64",
            "wine",
            "wine64"
        };

        foreach (var p in winePaths)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = p,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    process.WaitForExit(2000);
                    if (process.ExitCode == 0)
                        return p;
                }
            }
            catch
            {
            }
        }

        return null;
    }
}