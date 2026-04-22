using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using V0idedDecomp.ViewModels;

namespace V0idedDecomp.Views;

public partial class RPGMakerView : UserControl
{
    public RPGMakerView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private async void BrowseInput_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not RPGMakerViewModel vm) return;
        
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        
        var storageProvider = topLevel.StorageProvider;

        var folderResult = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select RPG Maker Game Folder (MV/MZ)",
            AllowMultiple = false
        });
        
        if (folderResult.Count > 0)
        {
            vm.InputFilePath = folderResult[0].Path.LocalPath;
            vm.StatusText = "Folder selected: " + System.IO.Path.GetFileName(vm.InputFilePath);
            return;
        }

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select RPG Maker Game File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("RPG Maker Games") { Patterns = new[] { "*.rgssad", "*.rgss2a", "*.rgss3a" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });
        
        if (result.Count > 0)
        {
            vm.InputFilePath = result[0].Path.LocalPath;
            vm.StatusText = "Selected: " + System.IO.Path.GetFileName(vm.InputFilePath);
        }
    }
}