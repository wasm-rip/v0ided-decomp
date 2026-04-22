using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace V0idedDecomp.ViewModels;

public partial class FusionViewModel : ObservableObject
{
    [ObservableProperty]
    private string _inputFilePath = "";

    [ObservableProperty]
    private string _outputDirectory = "";

    [ObservableProperty]
    private bool _extractImages = true;

    [ObservableProperty]
    private bool _extractAudio = true;

    [ObservableProperty]
    private bool _extractFonts = true;

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
            "fusion"
        );
    }

    public FusionViewModel()
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
            var toolPath = FindNebula();
            if (string.IsNullOrEmpty(toolPath))
            {
                StatusText = "Error: Nebula not found";
                LogLines.Add("ERROR: Could not find Nebula CLI");
                IsProcessing = false;
                return;
            }

            var gameName = Path.GetFileNameWithoutExtension(fullPath);
            var outputPath = Path.Combine(OutputDirectory, gameName, "ExportedProject");
            Directory.CreateDirectory(outputPath);

            var args = $"\"{fullPath}\" -o \"{outputPath}\"";
            if (ExtractImages) args += " -i";
            if (ExtractAudio) args += " -s";

            await RunNebulaAsync(toolPath, args);
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

    private string? FindNebula()
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
            var exePath = Path.Combine(baseDir, "fusion", "Nebula");
            if (File.Exists(exePath))
            {
                return exePath;
            }

            var dllPath = Path.Combine(baseDir, "fusion", "Nebula.dll");
            if (File.Exists(dllPath))
            {
                return dllPath;
            }
        }

        return null;
    }

    private async Task RunNebulaAsync(string toolPath, string args)
    {
        LogLines.Add("Running: " + toolPath);
        LogLines.Add("Args: " + args);

        var isDll = toolPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        var exeName = isDll ? "dotnet" : toolPath;
        var exeArgs = isDll ? $"\"{toolPath}\" {args}" : args;

        var startInfo = new ProcessStartInfo
        {
            FileName = exeName,
            Arguments = exeArgs,
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

    public void HandleFileDrop(string[] files)
    {
        if (files.Length > 0)
        {
            var file = files[0];
            var ext = Path.GetExtension(file).ToLowerInvariant();
            var validExts = new[] { ".exe", ".ccx", ".mfa", ".apk", ".ipa" };
            
            if (validExts.Contains(ext))
            {
                InputFilePath = file;
                StatusText = "Selected: " + Path.GetFileName(file);
            }
            else
            {
                StatusText = "Error: Unsupported file type";
            }
        }
    }
}