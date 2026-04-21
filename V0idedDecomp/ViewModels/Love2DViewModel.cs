using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace V0idedDecomp.ViewModels;

public partial class Love2DViewModel : ObservableObject
{
    [ObservableProperty]
    private string _inputFilePath = "";

    [ObservableProperty]
    private string _outputDirectory = "";

    [ObservableProperty]
    private bool _fixGoto = true;

    [ObservableProperty]
    private bool _portShaders = true;

    [ObservableProperty]
    private bool _minifyLua = true;

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
            "love2d"
        );
    }

    public Love2DViewModel()
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

        if (!File.Exists(InputFilePath))
        {
            StatusText = "Error: Input file does not exist";
            return;
        }

        IsProcessing = true;
        StatusText = "Processing...";
        LogLines.Clear();

        try
        {
            var scriptDir = FindLove2DScripts();
            if (string.IsNullOrEmpty(scriptDir))
            {
                StatusText = "Error: Could not find love2d scripts";
                IsProcessing = false;
                return;
            }

            var gameName = Path.GetFileNameWithoutExtension(InputFilePath);
            var outputPath = Path.Combine(OutputDirectory, gameName);
            var tempSrc = Path.Combine(scriptDir, "slop", "src");

            Directory.CreateDirectory(outputPath);
            if (Directory.Exists(tempSrc))
            {
                Directory.Delete(tempSrc, true);
            }
            Directory.CreateDirectory(tempSrc);

            // Extract the .love file
            LogLines.Add($"Extracting {InputFilePath}...");
            await ExtractLoveFile(InputFilePath, tempSrc);

            // Run the Python scripts in sequence
            if (FixGoto)
            {
                LogLines.Add("Running fix_goto.py...");
                await RunPythonScriptAsync(Path.Combine(scriptDir, "slop", "fix_goto.py"));
            }

            if (PortShaders)
            {
                LogLines.Add("Running port_shaders.py...");
                await RunPythonScriptAsync(Path.Combine(scriptDir, "slop", "port_shaders.py"));
            }

            if (MinifyLua)
            {
                LogLines.Add("Running minify_lua.py...");
                await RunPythonScriptAsync(Path.Combine(scriptDir, "slop", "minify_lua.py"), tempSrc);
            }

            // Copy to output
            if (Directory.Exists(tempSrc))
            {
                CopyDirectory(tempSrc, outputPath);
                Directory.Delete(tempSrc, true);
            }

            StatusText = "Success! Output saved to Documents";
            LogLines.Add("Done!");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            LogLines.Add($"Exception: {ex}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private string? FindLove2DScripts()
    {
        var possiblePaths = new[]
        {
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "love2d")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "love2d")),
            "/Volumes/Seagate/v0ided-decomp/love2d"
        };

        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "repack.py")))
            {
                return path;
            }
        }

        return null;
    }

    private async Task ExtractLoveFile(string loveFile, string destDir)
    {
        // .love files are just zip files
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "unzip",
            Arguments = $"\"{loveFile}\" -d \"{destDir}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process != null)
        {
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                // Try alternative: use System.IO.Compression
                // Fallback to manual extraction
                await ExtractWithDotNet(loveFile, destDir);
            }
        }
    }

    private async Task ExtractWithDotNet(string loveFile, string destDir)
    {
        await Task.Run(() =>
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(loveFile, destDir, true);
        });
    }

    private async Task RunPythonScriptAsync(string scriptPath, string? args = null)
    {
        var scriptArgs = args != null ? $"\"{args}\"" : "";

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "python3",
            Arguments = $"\"{scriptPath}\" {scriptArgs}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };

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
                Avalonia.Threading.Dispatcher.UIThread.Post(() => LogLines.Add($"[ERR] {e.Data}"));
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            LogLines.Add($"Script exited with code {process.ExitCode}");
        }
    }

    private void CopyDirectory(string source, string dest)
    {
        if (!Directory.Exists(dest))
        {
            Directory.CreateDirectory(dest);
        }

        foreach (var file in Directory.GetFiles(source))
        {
            var destFile = Path.Combine(dest, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            var destDir = Path.Combine(dest, Path.GetFileName(dir));
            CopyDirectory(dir, destDir);
        }
    }

    public void HandleFileDrop(string[] files)
    {
        if (files.Length > 0)
        {
            var file = files[0];
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".love")
            {
                InputFilePath = file;
                StatusText = $"Selected: {Path.GetFileName(file)}";
            }
            else
            {
                StatusText = "Error: Unsupported file type. Use .love";
            }
        }
    }
}
