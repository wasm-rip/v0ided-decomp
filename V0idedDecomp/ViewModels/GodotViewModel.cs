using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace V0idedDecomp.ViewModels;

public partial class GodotViewModel : ObservableObject
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
            "godot"
        );
    }

    public GodotViewModel()
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
            var gdrePath = FindGdreTools();
            if (string.IsNullOrEmpty(gdrePath))
            {
                StatusText = "Error: GDRE Tools not found";
                LogLines.Add("ERROR: Could not find Godot RE Tools");
                IsProcessing = false;
                return;
            }

            LogLines.Add("GDRE: " + gdrePath);

            var gameName = Path.GetFileNameWithoutExtension(fullPath);
            var outputPath = Path.Combine(OutputDirectory, gameName);

            Directory.CreateDirectory(outputPath);

            var args = $"--headless --recover=\"{fullPath}\" --output=\"{outputPath}\"";

            await RunProcessAsync(gdrePath, args);
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

    private string? FindGdreTools()
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
            Path.GetFullPath("../../../../../"),
            Path.GetFullPath("../../../../../../"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "v0ided-decomp")
        };

        LogLines.Add("Looking for GDRE in:");
        foreach (var p in possibleBasePaths)
        {
            LogLines.Add("  " + p);
        }

        foreach (var baseDir in possibleBasePaths)
        {
            if (string.IsNullOrEmpty(baseDir)) continue;
            
            var macAppPath = Path.Combine(baseDir, "godot", "gdre_tools", "Godot RE Tools.app", "Contents", "MacOS", "Godot RE Tools");
            if (File.Exists(macAppPath))
            {
                return macAppPath;
            }

            var winExe = Path.Combine(baseDir, "godot", "gdre_tools", "gdre_tools.exe");
            if (File.Exists(winExe))
            {
                return winExe;
            }

            var linuxExe = Path.Combine(baseDir, "godot", "gdre_tools", "gdre_tools");
            if (File.Exists(linuxExe))
            {
                return linuxExe;
            }
        }

        return null;
    }

    private async Task RunProcessAsync(string fileName, string arguments)
    {
        LogLines.Add("Running: " + fileName);
        LogLines.Add("Args: " + arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
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
}