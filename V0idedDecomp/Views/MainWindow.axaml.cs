using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace V0idedDecomp.Views;

public partial class MainWindow : Window
{
    private readonly List<Snowflake> _snowflakes = new();
    private readonly Random _random = new();
    private DispatcherTimer? _timer;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        StartSnow();
    }

    private void StartSnow()
    {
        var canvas = this.FindControl<Canvas>("SnowCanvas");
        if (canvas == null) return;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _timer.Tick += (s, e) => UpdateSnow(canvas);
        _timer.Start();

        for (int i = 0; i < 50; i++)
        {
            _snowflakes.Add(CreateSnowflake());
        }
    }

    private Snowflake CreateSnowflake()
    {
        return new Snowflake
        {
            X = _random.NextDouble() * 700,
            Y = _random.NextDouble() * 600,
            Size = _random.NextDouble() * 2 + 1,
            Speed = _random.NextDouble() * 1 + 0.5,
            Opacity = _random.NextDouble() * 0.5 + 0.3
        };
    }

    private void UpdateSnow(Canvas canvas)
    {
        canvas.Children.Clear();

        foreach (var snow in _snowflakes)
        {
            snow.Y += snow.Speed;
            snow.X += Math.Sin(snow.Y * 0.02) * 0.5;

            if (snow.Y > 600)
            {
                snow.Y = -5;
                snow.X = _random.NextDouble() * 700;
            }

            var ellipse = new Border
            {
                Width = snow.Size,
                Height = snow.Size,
                Background = new SolidColorBrush(Color.FromArgb((byte)(snow.Opacity * 255), 200, 200, 220)),
                CornerRadius = new CornerRadius(snow.Size / 2)
            };

            Canvas.SetLeft(ellipse, snow.X);
            Canvas.SetTop(ellipse, snow.Y);
            canvas.Children.Add(ellipse);
        }
    }

    private class Snowflake
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Size { get; set; }
        public double Speed { get; set; }
        public double Opacity { get; set; }
    }
}