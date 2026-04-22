using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace V0idedDecomp.ViewModels;

public partial class RPGMakerViewModel : ObservableObject
{
    [ObservableProperty]
    private string _inputFilePath = "";

    [ObservableProperty]
    private string _outputDirectory = "";

    [ObservableProperty]
    private bool _reconstructProject = true;

    [ObservableProperty]
    private bool _overwrite = true;

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
            "rpgmaker"
        );
    }

    public RPGMakerViewModel()
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
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            StatusText = "Error: File does not exist";
            return;
        }

        IsProcessing = true;
        StatusText = "Processing...";
        LogLines.Clear();

        try
        {
            var toolPath = FindDecrypter();
            if (string.IsNullOrEmpty(toolPath))
            {
                StatusText = "Error: RPGMakerDecrypter not found";
                LogLines.Add("ERROR: Could not find RPGMakerDecrypter");
                IsProcessing = false;
                return;
            }

            var gameName = GetGameName(fullPath);
            var outputPath = Path.Combine(OutputDirectory, gameName, "ExportedProject");
            Directory.CreateDirectory(outputPath);

            var args = $"\"{fullPath}\" -o \"{outputPath}\"";
            if (ReconstructProject) args += " -p";
            if (Overwrite) args += " -w";

            await RunDecrypterAsync(toolPath, args);
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

    private string GetGameName(string inputPath)
    {
        if (Directory.Exists(inputPath))
        {
            return Path.GetFileName(inputPath);
        }
        return Path.GetFileNameWithoutExtension(inputPath);
    }

    private string? FindDecrypter()
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
            var dllPath = Path.Combine(baseDir, "rpgmaker", "RPGMakerDecrypter-cli.dll");
            if (File.Exists(dllPath))
            {
                return dllPath;
            }
        }

        return null;
    }

    private async Task RunDecrypterAsync(string toolPath, string args)
    {
        LogLines.Add("Running: dotnet " + toolPath);
        LogLines.Add("Args: " + args);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{toolPath}\" {args}",
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
            var validExts = new[] { ".rgssad", ".rgss2a", ".rgss3a" };
            
            if (validExts.Contains(ext) || Directory.Exists(file))
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