using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace V0idedDecomp.ViewModels;

public partial class RenPyViewModel : ObservableObject
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
            "renpy"
        );
    }

    public RenPyViewModel()
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
        var ext = Path.GetExtension(fullPath).ToLower();

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
            var gameName = Path.GetFileNameWithoutExtension(fullPath);
            var outputPath = Path.Combine(OutputDirectory, gameName);

            Directory.CreateDirectory(outputPath);

            if (ext == ".rpa")
            {
                var rpatoolPath = FindRpatool();
                if (string.IsNullOrEmpty(rpatoolPath))
                {
                    StatusText = "Error: rpatool.py not found";
                    LogLines.Add("ERROR: Could not find rpatool.py");
                    IsProcessing = false;
                    return;
                }

                LogLines.Add("Extracting RPA: " + rpatoolPath);
                var args = $"-x \"{fullPath}\" -o \"{outputPath}\"";
                await RunPythonAsync(rpatoolPath, args);
            }
            else if (ext == ".rpyc")
            {
                LogLines.Add("Decompiling rpyc...");
                var inputDir = Path.GetDirectoryName(fullPath) ?? "";
                var args = $"\"{fullPath}\"";
                await RunRpycdecAsync(args, outputPath);
            }
            else
            {
                StatusText = "Error: Unsupported file type";
                LogLines.Add("Supported: .rpa, .rpyc");
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

    private string? FindRpatool()
    {
        var possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "renpy", "rpatool.py"),
            Path.Combine(Directory.GetCurrentDirectory(), "renpy", "rpatool.py"),
            "/Users/pranavbajjuri/Downloads/rpatool.py",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "v0ided-decomp", "renpy", "rpatool.py")
        };

        foreach (var p in possiblePaths)
        {
            if (File.Exists(p))
                return p;
        }
        return null;
    }

    private async Task RunPythonAsync(string scriptPath, string arguments)
    {
        LogLines.Add("Running: python3 " + scriptPath + " " + arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments = $"\"{scriptPath}\" {arguments}",
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

    private async Task RunRpycdecAsync(string arguments, string outputDir)
    {
        LogLines.Add("Running: rpycdec " + arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = "rpycdec",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = outputDir
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
}