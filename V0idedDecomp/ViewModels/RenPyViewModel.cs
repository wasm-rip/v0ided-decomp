using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

    [ObservableProperty]
    private bool _isFolderMode;

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
            StatusText = "Error: No input selected";
            return;
        }

        var fullPath = InputFilePath.Trim();
        IsProcessing = true;
        StatusText = "Processing...";
        LogLines.Clear();

        try
        {
            if (IsFolderMode || Directory.Exists(fullPath))
            {
                await DecompileFolder(fullPath);
            }
            else
            {
                await DecompileFile(fullPath);
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

    private async Task DecompileFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            StatusText = "Error: Folder does not exist";
            return;
        }

        LogLines.Add("Scanning folder: " + folderPath);
        
        var rpaFiles = Directory.GetFiles(folderPath, "*.rpa", SearchOption.AllDirectories);
        var rpycFiles = Directory.GetFiles(folderPath, "*.rpyc", SearchOption.AllDirectories);
        var rpyFiles = Directory.GetFiles(folderPath, "*.rpy", SearchOption.AllDirectories);

        var totalFiles = rpaFiles.Length + rpycFiles.Length;
        
        if (totalFiles == 0)
        {
            StatusText = "No RPA/rpyc files found";
            LogLines.Add("No Ren'Py archive files found in folder");
            return;
        }

        LogLines.Add($"Found {rpaFiles.Length} RPA, {rpycFiles.Length} rpyc, {rpyFiles.Length} rpy files");

        var gameName = Path.GetFileName(folderPath);
        var outputBase = Path.Combine(OutputDirectory, gameName);
        var outputPath = Path.Combine(outputBase, "ExportedProject");
        Directory.CreateDirectory(outputPath);

        var processed = 0;

        foreach (var rpa in rpaFiles)
        {
            processed++;
            StatusText = $"Extracting RPA ({processed}/{totalFiles})";
            LogLines.Add($"[{processed}/{totalFiles}] {rpa}");
            
            await ExtractRpa(rpa, outputPath);
        }

        foreach (var rpyc in rpycFiles)
        {
            processed++;
            StatusText = $"Decompiling rpyc ({processed}/{totalFiles})";
            LogLines.Add($"[{processed}/{totalFiles}] {rpyc}");
            
            await DecompileRpyc(rpyc, outputPath);
        }

        foreach (var rpy in rpyFiles)
        {
            var relativePath = Path.GetRelativePath(folderPath, rpy);
            var destPath = Path.Combine(outputPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            
            try
            {
                File.Copy(rpy, destPath, true);
            }
            catch { }
        }

        StatusText = $"Done! Processed {totalFiles} files";
    }

    private async Task ExtractRpa(string rpaPath, string outputBase)
    {
        var rpatoolPath = FindRpatool();
        if (string.IsNullOrEmpty(rpatoolPath))
        {
            LogLines.Add("ERROR: rpatool.py not found");
            return;
        }

        var rpaName = Path.GetFileNameWithoutExtension(rpaPath);
        var rpaOutput = Path.Combine(outputBase, rpaName);
        Directory.CreateDirectory(rpaOutput);

        var args = $"-x \"{rpaPath}\" -o \"{rpaOutput}\"";
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments = $"\"{rpatoolPath}\" {args}",
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
    }

    private async Task DecompileRpyc(string rpycPath, string outputBase)
    {
        var rpycdecDir = FindRpycdecDir();
        if (string.IsNullOrEmpty(rpycdecDir))
        {
            LogLines.Add("ERROR: rpycdec not found");
            return;
        }

        var rpycDir = Path.GetDirectoryName(rpycPath) ?? "";
        var rpycDirOutput = Path.Combine(outputBase, rpycDir);
        Directory.CreateDirectory(rpycDirOutput);

        var wrapperPath = Path.Combine(rpycdecDir, "run_rpycdec.py");
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments = $"\"{wrapperPath}\" \"{rpycPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = outputBase,
            Environment = 
            {
                { "RPYCDEC_NO_WARNING", "1" }
            }
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
    }

    private async Task DecompileFile(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            StatusText = "Error: File does not exist";
            return;
        }

        var ext = Path.GetExtension(fullPath).ToLower();
        var gameName = Path.GetFileNameWithoutExtension(fullPath);
        var outputBase = Path.Combine(OutputDirectory, gameName);
        var outputPath = Path.Combine(outputBase, "ExportedProject");
        Directory.CreateDirectory(outputPath);

        if (ext == ".rpa")
        {
            await ExtractRpa(fullPath, outputPath);
        }
        else if (ext == ".rpyc")
        {
            await DecompileRpyc(fullPath, outputPath);
        }
        else if (ext == ".rpy")
        {
            var destPath = Path.Combine(outputPath, Path.GetFileName(fullPath));
            File.Copy(fullPath, destPath, true);
            StatusText = "Copied rpy file";
        }
        else
        {
            StatusText = "Error: Unsupported file type";
            return;
        }

        StatusText = "Done!";
    }

    private string? FindRpatool()
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
            var rpatoolPath = Path.Combine(baseDir, "renpy", "rpatool.py");
            if (File.Exists(rpatoolPath))
                return rpatoolPath;
            
            rpatoolPath = Path.Combine(baseDir, "Tools", "rpatool.py");
            if (File.Exists(rpatoolPath))
                return rpatoolPath;
        }
        return null;
    }

    private string? FindRpycdecDir()
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
            var rpycdecPath = Path.Combine(baseDir, "Tools", "rpycdec_lib");
            if (Directory.Exists(rpycdecPath))
                return rpycdecPath;
        }
        return null;
    }
}