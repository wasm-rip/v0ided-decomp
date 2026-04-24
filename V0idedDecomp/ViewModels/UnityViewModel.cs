using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace V0idedDecomp.ViewModels;

public partial class UnityViewModel : ObservableObject
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

    [ObservableProperty]
    private string _exportMode = "unity-project";

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private string _progressText = "";

    public UnityViewModel()
    {
        OutputDirectory = GetDocumentsPath();
    }

    private string GetDocumentsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "v0ided-decomp",
            "unity"
        );
    }

    [RelayCommand]
    public async Task DecompileAsync()
    {
        if (string.IsNullOrWhiteSpace(InputFilePath))
        {
            StatusText = "Error: No input selected";
            return;
        }

        var fullPath = InputFilePath.Trim();
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            StatusText = "Error: Path does not exist";
            return;
        }

        IsProcessing = true;
        Progress = 0;
        ProgressText = "Starting...";
        StatusText = "Extracting...";
        LogLines.Clear();

        try
        {
            var gameName = Directory.Exists(fullPath) 
                ? Path.GetFileName(fullPath) 
                : Path.GetFileNameWithoutExtension(fullPath);
            var outputBase = Path.Combine(OutputDirectory, gameName, "ExportedProject");
            
            await RunCliAsync(fullPath, outputBase);
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
            ProgressText = "Failed";
            LogLines.Add("ERROR: " + ex.Message);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task RunCliAsync(string inputPath, string outputPath)
    {
        var cliPath = FindCli();
        if (string.IsNullOrEmpty(cliPath))
        {
            StatusText = "Error: CLI not found";
            ProgressText = "Error";
            LogLines.Add("ERROR: CLI not found");
            return;
        }

        Directory.CreateDirectory(outputPath);

        var formatArg = ExportMode == "primary-content" ? "primary-content" : "unity-project";

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{cliPath}\" --input \"{inputPath}\" --output \"{outputPath}\" --format {formatArg}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = outputPath
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputBuilder.AppendLine(e.Data);
                UpdateProgress(e.Data);
            }
        };

        process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorBuilder.AppendLine(e.Data);
                if (!e.Data.Contains("warning", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateProgress("[ERR] " + e.Data);
                }
            }
        };

        Progress = 10;
        ProgressText = "Loading game...";
        LogLines.Add($"Loading: {Path.GetFileName(inputPath)}");

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        Progress = 90;
        ProgressText = "Finalizing...";

        var output = outputBuilder.ToString();
        var errors = errorBuilder.ToString();

        if (process.ExitCode == 0)
        {
            Progress = 100;
            ProgressText = "Done!";
            StatusText = "Complete!";
            
            var lastLine = output.Split('\n').FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            if (!string.IsNullOrEmpty(lastLine))
            {
                LogLines.Add(lastLine.Length > 100 ? lastLine[..100] + "..." : lastLine);
            }
        }
        else
        {
            ProgressText = "Failed";
            StatusText = "Error: " + process.ExitCode;
            var errLines = errors.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var err in errLines.Take(3))
            {
                var trimmed = err.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    LogLines.Add(trimmed.Length > 100 ? trimmed[..100] + "..." : trimmed);
            }
        }
    }

    private void UpdateProgress(string line)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var lineLower = line.ToLowerInvariant();
            
            if (lineLower.Contains("loading"))
            {
                Progress = 20;
                ProgressText = "Loading...";
            }
            else if (lineLower.Contains("exporting"))
            {
                Progress = 50;
                ProgressText = "Exporting...";
            }
            else if (lineLower.Contains("processing"))
            {
                Progress = 70;
                ProgressText = "Processing...";
            }
            else if (lineLower.Contains("done") || lineLower.Contains("complete") || lineLower.Contains("exported"))
            {
                Progress = 95;
                ProgressText = "Finalizing...";
            }
            else if (lineLower.Contains("[err]"))
            {
                if (LogLines.Count < 5)
                {
                    var trimmed = line.Length > 80 ? line[..80] + "..." : line;
                    LogLines.Add(trimmed);
                }
            }
        });
    }

    private string? FindCli()
    {
        var baseDirs = new[]
        {
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..")),
            AppDomain.CurrentDomain.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "v0ided-decomp")
        };

        foreach (var baseDir in baseDirs)
        {
            var cliPath = Path.Combine(baseDir, "unity", "AssetRipper-CLI", "AssetRipperCLI.dll");
            if (File.Exists(cliPath))
                return cliPath;
            
            cliPath = Path.Combine(baseDir, "Tools", "AssetRipper", "AssetRipperCLI.dll");
            if (File.Exists(cliPath))
                return cliPath;
        }

        return null;
    }
}