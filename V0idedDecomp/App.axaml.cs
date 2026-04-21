using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using V0idedDecomp.ViewModels;
using V0idedDecomp.Views;

namespace V0idedDecomp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        EnsureDotNetRuntime();
        EnsureGdreTools();
        EnsureGameMakerTools();
        EnsureRenPyTools();
        base.OnFrameworkInitializationCompleted();
    }

    private void EnsureDotNetRuntime()
    {
        try
        {
            // Check if dotnet command exists
            var dotnetCheck = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            using var checkProcess = System.Diagnostics.Process.Start(dotnetCheck);
            if (checkProcess == null)
            {
                Console.WriteLine("dotnet not found - installing .NET 10...");
                InstallDotNetRuntime();
                return;
            }
            
            checkProcess.WaitForExit();
            var version = checkProcess.StandardOutput.ReadToEnd().Trim();
            
            Console.WriteLine($"Found dotnet version: {version}");
            
            // Check for .NET 10 runtime (10.0.x)
            if (!version.StartsWith("10."))
            {
                Console.WriteLine("Installing .NET 10 runtime...");
                InstallDotNetRuntime();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("dotnet check failed: " + ex.Message);
            Console.WriteLine("Installing .NET 10 runtime...");
            InstallDotNetRuntime();
        }
    }

    private void InstallDotNetRuntime()
    {
        try
        {
            string installCommand;
            if (OperatingSystem.IsMacOS())
            {
                installCommand = "bash -c \"$(curl -sSL https://dot.net/vbqd-install.sh)\"";
            }
            else
            {
                installCommand = "powershell -ExecutionPolicy Bypass -Command \"& { Invoke-WebRequest -Uri https://dot.net/vbqd-install.sh -OutFile install-dotnet.sh; bash install-dotnet.sh; del install-dotnet.sh }\"";
            }
            
            var installInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "-c \"" + installCommand + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            using var installProcess = System.Diagnostics.Process.Start(installInfo);
            installProcess?.WaitForExit();
            
            Console.WriteLine(".NET 10 installation complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine(".NET 10 installation failed: " + ex.Message);
            Console.WriteLine("Please manually install .NET 10 from https://dotnet.microsoft.com/download/dotnet");
        }
    }

    private void EnsureGdreTools()
    {
        var baseDir = Directory.GetCurrentDirectory();
        var gdreDir = Path.Combine(baseDir, "godot", "gdre_tools");
        var macApp = Path.Combine(gdreDir, "Godot RE Tools.app", "Contents", "MacOS", "Godot RE Tools");

        if (!File.Exists(macApp))
        {
            try
            {
                Console.WriteLine("Downloading GDRE Tools...");
                var zipPath = Path.Combine(baseDir, "gdre.zip");
                var url = "https://github.com/GDRETools/gdsdecomp/releases/download/v2.5.0-beta.4/GDRE_tools-v2.5.0-beta.4-macos.zip";
                
                using var client = new System.Net.Http.HttpClient();
                var data = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
                File.WriteAllBytes(zipPath, data);
                
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, gdreDir, true);
                File.Delete(zipPath);
                
                Console.WriteLine("GDRE Tools downloaded!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Auto-download failed: " + ex.Message);
            }
        }
    }

    private void EnsureGameMakerTools()
    {
        var baseDir = Directory.GetCurrentDirectory();
        var gmDir = Path.Combine(baseDir, "gamemaker");
        
        if (!Directory.Exists(gmDir))
            Directory.CreateDirectory(gmDir);

        var cliDllPath = Path.Combine(gmDir, "UnderAnalyzerCLI.dll");
        if (!File.Exists(cliDllPath))
        {
            try
            {
                var forkDir = "/Volumes/Seagate/v0ided-decomp/UnderAnalyzer-Decompiler";
                var srcDir = Path.Combine(forkDir, "UndertaleModCli", "bin", "release", "net10.0");
                
                if (Directory.Exists(srcDir))
                {
                    // Copy all files from the build output
                    foreach (var file in Directory.GetFiles(srcDir))
                    {
                        var fileName = Path.GetFileName(file);
                        var destPath = Path.Combine(gmDir, fileName);
                        if (!File.Exists(destPath))
                        {
                            File.Copy(file, destPath, true);
                        }
                    }
                    Console.WriteLine("UnderAnalyzer CLI DLLs copied from fork!");
                }
                else
                {
                    Console.WriteLine("UnderAnalyzer CLI not found - please build the CLI from the fork first");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Auto-copy failed: " + ex.Message);
            }
        }
    }

    private void EnsureRenPyTools()
    {
        var baseDir = Directory.GetCurrentDirectory();
        var renpyDir = Path.Combine(baseDir, "renpy");
        
        if (!Directory.Exists(renpyDir))
            Directory.CreateDirectory(renpyDir);

        var rpatoolPath = Path.Combine(renpyDir, "rpatool.py");
        if (!File.Exists(rpatoolPath))
        {
            try
            {
                Console.WriteLine("Downloading rpatool.py...");
                var url = "https://raw.githubusercontent.com/Andersmholmgren/rpatool/master/rpatool.py";
                
                using var client = new System.Net.Http.HttpClient();
                var data = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
                File.WriteAllBytes(rpatoolPath, data);
                
                Console.WriteLine("rpatool.py downloaded!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("rpatool.py download failed: " + ex.Message);
            }
        }

        try
        {
            Console.WriteLine("Checking rpycdec...");
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "pip3",
                Arguments = "show rpycdec",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            
            using var process = System.Diagnostics.Process.Start(startInfo);
            process?.WaitForExit();
            
            if (process?.ExitCode != 0)
            {
                Console.WriteLine("Installing rpycdec...");
                var pipInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "pip3",
                    Arguments = "install rpycdec",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                
                using var pipProcess = System.Diagnostics.Process.Start(pipInfo);
                pipProcess?.WaitForExit();
                
                Console.WriteLine("rpycdec installed!");
            }
            else
            {
                Console.WriteLine("rpycdec already installed");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("rpycdec check failed: " + ex.Message);
        }
    }
}