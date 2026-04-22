using CommunityToolkit.Mvvm.ComponentModel;

namespace V0idedDecomp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _selectedTabIndex;

    public GodotViewModel Godot { get; } = new();
    public Love2DViewModel Love2D { get; } = new();
    public GameMakerViewModel GameMaker { get; } = new();
    public RenPyViewModel RenPy { get; } = new();
    public UnityViewModel Unity { get; } = new();
    public RPGMakerViewModel RPGMaker { get; } = new();

    public string AppTitle { get; } = "v0ided-decomp";
}