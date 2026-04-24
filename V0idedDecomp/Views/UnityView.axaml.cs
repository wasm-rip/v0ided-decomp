using System;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using V0idedDecomp.ViewModels;

namespace V0idedDecomp.Views;

public partial class UnityView : UserControl
{
    public UnityView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private async void BrowseInput_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not UnityViewModel vm) return;
        
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        
        var storageProvider = topLevel.StorageProvider;
        
        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Unity Game Folder",
            AllowMultiple = false
        });
        
        if (result.Count > 0)
        {
            vm.InputFilePath = result[0].Path.LocalPath;
            vm.StatusText = "Folder selected: " + System.IO.Path.GetFileName(vm.InputFilePath);
        }
    }

    private async void Extract_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not UnityViewModel vm) return;
        
        await vm.DecompileAsync();
    }

    private async void CopyLog_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not UnityViewModel vm) return;
        
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