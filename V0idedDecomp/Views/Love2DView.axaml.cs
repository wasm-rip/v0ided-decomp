using System;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using V0idedDecomp.ViewModels;

namespace V0idedDecomp.Views;

public partial class Love2DView : UserControl
{
    public Love2DView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private async void BrowseInput_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not Love2DViewModel vm) return;
        
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        
        var storageProvider = topLevel.StorageProvider;
        
        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select LOVE Game File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("LOVE Files") { Patterns = new[] { "*.love" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });
        
        if (result.Count > 0)
        {
            vm.InputFilePath = result[0].Path.LocalPath;
            vm.StatusText = "Selected: " + System.IO.Path.GetFileName(vm.InputFilePath);
        }
    }

    private async void CopyLog_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not Love2DViewModel vm) return;
        
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