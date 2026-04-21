using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using V0idedDecomp.ViewModels;

namespace V0idedDecomp.Views;

public partial class GodotView : UserControl
{
    public GodotView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private async void BrowseInput_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GodotViewModel vm) return;
        
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        
        var storageProvider = topLevel.StorageProvider;
        
        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Godot Game File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Godot Files") { Patterns = new[] { "*.pck", "*.exe", "*.apk" } },
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