using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using V0idedDecomp.ViewModels;

namespace V0idedDecomp.Views;

public partial class RenPyView : UserControl
{
    public RenPyView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private async void BrowseInput_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not RenPyViewModel vm) return;
        
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        
        var storageProvider = topLevel.StorageProvider;
        
        // Try folder first (for folder mode)
        var folderResult = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Ren'Py Game Folder",
            AllowMultiple = false
        });
        
        if (folderResult.Count > 0)
        {
            vm.InputFilePath = folderResult[0].Path.LocalPath;
            vm.IsFolderMode = true;
            vm.StatusText = "Folder selected: " + System.IO.Path.GetFileName(vm.InputFilePath);
            return;
        }
        
        // Fall back to file selection
        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Ren'Py File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Ren'Py Files") { Patterns = new[] { "*.rpa", "*.rpyc", "*.rpy" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });
        
        if (result.Count > 0)
        {
            vm.InputFilePath = result[0].Path.LocalPath;
            vm.IsFolderMode = false;
            vm.StatusText = "Selected: " + System.IO.Path.GetFileName(vm.InputFilePath);
        }
    }

    private async void CopyLog_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not RenPyViewModel vm) return;
        
        var logText = string.Join(Environment.NewLine, vm.LogLines);
        if (string.IsNullOrEmpty(logText))
        {
            vm.StatusText = "Log is empty";
            return;
        }
        
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        
        try
        {
            var clipboard = topLevel.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(logText);
                vm.StatusText = "Log copied to clipboard";
            }
        }
        catch
        {
            vm.StatusText = "Failed to copy to clipboard";
        }
    }
}