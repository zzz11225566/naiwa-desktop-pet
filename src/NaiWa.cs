using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        App app = new App();
        app.Run();
    }
}

public class App : Application
{
    private PetWindow _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _window = new PetWindow();
        _window.Show();
    }
}

public class PetSettings
{
    public double Scale = 1.0;
    public bool Topmost = true;
    public double X = double.NaN;
    public double Y = double.NaN;

    private static string FilePath
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NaiWa");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    public static PetSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return Parse(File.ReadAllText(FilePath));
            }
        }
        catch
        {
        }
        return new PetSettings();
    }

    public void Save()
    {
        try
        {
            string json =
                "{\"scale\":" + Scale.ToString("0.###", CultureInfo.InvariantCulture) +
                ",\"topmost\":" + (Topmost ? "true" : "false") +
                ",\"x\":" + (double.IsNaN(X) ? "null" : X.ToString("0.#", CultureInfo.InvariantCulture)) +
                ",\"y\":" + (double.IsNaN(Y) ? "null" : Y.ToString("0.#", CultureInfo.InvariantCulture)) +
                "}";
            File.WriteAllText(FilePath, json);
        }
        catch
        {
        }
    }

    private static PetSettings Parse(string json)
    {
        PetSettings s = new PetSettings();
        json = json.Trim().Trim('{', '}');
        foreach (string part in SplitJson(json))
        {
            string[] kv = part.Split(new char[] { ':' }, 2);
            if (kv.Length != 2) continue;
            string key = kv[0].Trim().Trim('"').ToLowerInvariant();
            string val = kv[1].Trim().Trim('"').ToLowerInvariant();
            double d;
            if (key == "scale" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                s.Scale = d;
            else if (key == "topmost")
                s.Topmost = val != "false";
            else if (key == "x" && val != "null" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                s.X = d;
            else if (key == "y" && val != "null" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                s.Y = d;
        }
        return s;
    }

    private static IEnumerable<string> SplitJson(string body)
    {
        var list = new List<string>();
        int start = 0;
        bool inString = false;
        for (int i = 0; i < body.Length; i++)
        {
            char c = body[i];
            if (c == '"') inString = !inString;
            if (c == ',' && !inString)
            {
                list.Add(body.Substring(start, i - start));
                start = i + 1;
            }
        }
        if (start < body.Length) list.Add(body.Substring(start));
        return list;
    }
}

public class PetWindow : Window
{
    private const double BaseSize = 380.0;

    private readonly PetSettings _settings;
    private readonly Random _rng = new Random();
    private readonly Image _idleImage;
    private readonly Image _actionImage;
    private readonly ScaleTransform _idleScale;
    private readonly ScaleTransform _actionScale;
    private readonly BitmapImage _idleBitmap;
    private readonly List<BitmapImage> _actionBitmaps = new List<BitmapImage>();

    private DateTime _downTime;
    private Point _downPoint;
    private Point _downScreen;
    private bool _mouseDown;
    private bool _dragging;
    private bool _suppressAction;
    private bool _actionActive;
    private DispatcherTimer _clickTimer;
    private DispatcherTimer _returnTimer;
    private PanelWindow _panel;

    private static readonly string IdleResource = "NaiWa.idle.png";
    private static readonly string[] ActionResources =
    {
        "NaiWa.action.0.png",
        "NaiWa.action.1.png",
        "NaiWa.action.2.png",
        "NaiWa.action.3.png",
        "NaiWa.action.4.png",
        "NaiWa.action.5.png",
        "NaiWa.action.6.png",
        "NaiWa.action.7.png"
    };

    public PetWindow()
    {
        _settings = PetSettings.Load();
        _idleBitmap = LoadPng(IdleResource);
        foreach (string name in ActionResources)
        {
            BitmapImage bmp = LoadPng(name);
            if (bmp != null) _actionBitmaps.Add(bmp);
        }

        _idleScale = new ScaleTransform(1, 1);
        _actionScale = new ScaleTransform(1, 1);

        _idleImage = new Image
        {
            Source = _idleBitmap,
            Stretch = Stretch.Uniform,
            RenderTransformOrigin = new Point(0.5, 1),
            RenderTransform = _idleScale,
            IsHitTestVisible = false
        };
        _actionImage = new Image
        {
            Stretch = Stretch.Uniform,
            RenderTransformOrigin = new Point(0.5, 1),
            RenderTransform = _actionScale,
            IsHitTestVisible = false,
            Opacity = 0
        };

        var grid = new Grid();
        grid.Children.Add(_idleImage);
        grid.Children.Add(_actionImage);
        Content = grid;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = _settings.Topmost;

        _clickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(420) };
        _clickTimer.Tick += OnClickTimerTick;

        MouseLeftButtonDown += OnMouseLeftDown;
        MouseLeftButtonUp += OnMouseLeftUp;
        MouseMove += OnMouseMove;
        MouseRightButtonUp += OnRightClick;
        Closing += OnClosing;
        Loaded += OnLoaded;

        ApplySize(_settings.Scale * BaseSize);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var work = SystemParameters.WorkArea;
        double x = _settings.X;
        double y = _settings.Y;
        if (double.IsNaN(x) || double.IsNaN(y))
        {
            x = work.Right - Width - 28;
            y = work.Bottom - Height - 30;
        }
        Left = Clamp(x, work.Left, work.Right - Width);
        Top = Clamp(y, work.Top, work.Bottom - Height);
        StartIdleBreathing();
    }

    private void StartIdleBreathing()
    {
        var sx = new DoubleAnimation(1.0, 1.016, TimeSpan.FromSeconds(2.4))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        var sy = new DoubleAnimation(1.0, 1.012, TimeSpan.FromSeconds(2.4))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        _idleScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
        _idleScale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
    }

    private void OnMouseLeftDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDown = true;
        _dragging = false;
        _downTime = DateTime.UtcNow;
        _downPoint = e.GetPosition(this);
        _downScreen = PointToScreen(e.GetPosition(this));
        Mouse.Capture(this);

        if (e.ClickCount == 2)
        {
            _mouseDown = false;
            _dragging = false;
            _clickTimer.Stop();
            _suppressAction = true;
            if (Mouse.Captured == this) Mouse.Capture(null);
            ShowPanel();
            return;
        }
        _suppressAction = false;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_mouseDown || e.LeftButton != MouseButtonState.Pressed) return;
        Point p = e.GetPosition(this);
        Vector d = p - _downPoint;
        if (!_dragging && (Math.Abs(d.X) > SystemParameters.MinimumHorizontalDragDistance ||
                           Math.Abs(d.Y) > SystemParameters.MinimumVerticalDragDistance))
        {
            _dragging = true;
        }
        if (_dragging)
        {
            Point screen = PointToScreen(p);
            Left += screen.X - _downScreen.X;
            Top += screen.Y - _downScreen.Y;
            _downScreen = screen;
            var work = SystemParameters.WorkArea;
            Left = Clamp(Left, work.Left, work.Right - Width);
            Top = Clamp(Top, work.Top, work.Bottom - Height);
        }
    }

    private void OnMouseLeftUp(object sender, MouseButtonEventArgs e)
    {
        bool wasDown = _mouseDown;
        bool wasDrag = _dragging;
        _mouseDown = false;
        _dragging = false;
        if (Mouse.Captured == this) Mouse.Capture(null);
        if (!wasDown || wasDrag) return;
        if ((DateTime.UtcNow - _downTime).TotalMilliseconds > 650) return;
        _clickTimer.Stop();
        _clickTimer.Start();
    }

    private void OnClickTimerTick(object sender, EventArgs e)
    {
        _clickTimer.Stop();
        if (!_suppressAction)
        {
            TriggerAction();
        }
        _suppressAction = false;
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        ShowPanel();
    }

    private void TriggerAction()
    {
        if (_actionActive || _actionBitmaps.Count == 0) return;
        _actionActive = true;

        BitmapImage frame = _actionBitmaps[_rng.Next(_actionBitmaps.Count)];
        _actionImage.Source = frame;
        _actionImage.Opacity = 0;
        _actionScale.ScaleX = 0.94;
        _actionScale.ScaleY = 0.94;

        var inEase = new CubicEase { EasingMode = EasingMode.EaseOut };
        _idleImage.BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, 0.0, TimeSpan.FromSeconds(0.30))
        {
            EasingFunction = inEase
        });
        _actionImage.BeginAnimation(OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromSeconds(0.30))
        {
            EasingFunction = inEase
        });
        _actionScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.94, 1.0, TimeSpan.FromSeconds(0.34))
        {
            EasingFunction = new BackEase { Amplitude = 0.22, EasingMode = EasingMode.EaseOut }
        });
        _actionScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.94, 1.0, TimeSpan.FromSeconds(0.34))
        {
            EasingFunction = new BackEase { Amplitude = 0.22, EasingMode = EasingMode.EaseOut }
        });

        double hold = 2.5 + _rng.NextDouble() * 1.8;
        _returnTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.30 + hold) };
        _returnTimer.Tick += delegate
        {
            _returnTimer.Stop();
            ReturnToIdle();
        };
        _returnTimer.Start();
    }

    private void ReturnToIdle()
    {
        var outEase = new CubicEase { EasingMode = EasingMode.EaseInOut };
        _idleImage.BeginAnimation(OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromSeconds(0.46))
        {
            EasingFunction = outEase
        });
        _actionImage.BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, 0.0, TimeSpan.FromSeconds(0.46))
        {
            EasingFunction = outEase
        });
        _actionActive = false;
    }

    private void ShowPanel()
    {
        if (_panel != null)
        {
            _panel.Close();
            _panel = null;
        }
        _panel = new PanelWindow(this);
        _panel.Owner = this;
        _panel.Show();
    }

    internal double CurrentSize
    {
        get { return Width; }
    }

    internal void ApplySize(double sizePixels)
    {
        var work = SystemParameters.WorkArea;
        double max = Math.Max(110, work.Height / 2.0);
        double min = 96;
        double size = Math.Max(min, Math.Min(max, sizePixels));
        _settings.Scale = size / BaseSize;
        Width = size;
        Height = size;
    }

    internal void SetTopmost(bool topmost)
    {
        Topmost = topmost;
        _settings.Topmost = topmost;
    }

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _settings.X = Left;
        _settings.Y = Top;
        _settings.Save();
        if (_panel != null)
        {
            _panel.Close();
            _panel = null;
        }
    }

    private static BitmapImage LoadPng(string name)
    {
        Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream == null) return null;
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = stream;
        bmp.EndInit();
        bmp.Freeze();
        stream.Dispose();
        return bmp;
    }

    private static double Clamp(double v, double min, double max)
    {
        if (v < min) return min;
        if (v > max) return max;
        return v;
    }
}

public class PanelWindow : Window
{
    private readonly PetWindow _pet;
    private Slider _size;

    public PanelWindow(PetWindow pet)
    {
        _pet = pet;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(238, 252, 240, 217));
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(238, 252, 240, 217)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(211, 167, 92)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Child = BuildContent()
        };
        Content = border;
        Loaded += OnLoaded;
    }

    private UIElement BuildContent()
    {
        var panel = new StackPanel();

        var title = new TextBlock
        {
            Text = "\u5976\u86d9",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(91, 58, 33)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12)
        };
        panel.Children.Add(title);

        var sizeLabel = new TextBlock
        {
            Text = "\u5927\u5c0f",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(91, 58, 33)),
            Margin = new Thickness(0, 0, 0, 4)
        };
        panel.Children.Add(sizeLabel);

        _size = new Slider
        {
            Minimum = 96,
            Maximum = Math.Max(110, SystemParameters.WorkArea.Height / 2.0),
            Value = Math.Max(96, _pet.CurrentSize),
            Width = 180
        };
        _size.ValueChanged += OnSizeChanged;
        panel.Children.Add(_size);

        var sizeHint = new TextBlock
        {
            Text = "",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(143, 105, 64)),
            Margin = new Thickness(0, 2, 0, 12)
        };
        panel.Children.Add(sizeHint);
        _size.Tag = sizeHint;
        UpdateSizeHint();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        buttons.Children.Add(MakeButton("\u4e0a\u79fb", delegate { _pet.SetTopmost(true); }));
        buttons.Children.Add(MakeButton("\u4e0b\u79fb", delegate { _pet.SetTopmost(false); }));
        panel.Children.Add(buttons);

        var close = MakeButton("\u5173\u95ed", Close);
        close.Margin = new Thickness(0, 10, 0, 0);
        close.HorizontalAlignment = HorizontalAlignment.Center;
        panel.Children.Add(close);

        var quit = MakeButton("\u9000\u51fa", delegate { Application.Current.Shutdown(); });
        quit.Margin = new Thickness(0, 5, 0, 0);
        quit.HorizontalAlignment = HorizontalAlignment.Center;
        quit.Background = new SolidColorBrush(Color.FromRgb(224, 147, 127));
        quit.BorderBrush = new SolidColorBrush(Color.FromRgb(184, 104, 86));
        panel.Children.Add(quit);
        return panel;
    }

    private Button MakeButton(string text, Action action)
    {
        var b = new Button
        {
            Content = text,
            MinWidth = 62,
            Height = 28,
            Margin = new Thickness(3, 0, 3, 0),
            Background = new SolidColorBrush(Color.FromRgb(226, 185, 122)),
            Foreground = new SolidColorBrush(Color.FromRgb(65, 42, 25)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(187, 137, 72)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 12,
            Cursor = Cursors.Hand
        };
        b.Click += delegate { action(); };
        return b;
    }

    private void OnSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _pet.ApplySize(e.NewValue);
        UpdateSizeHint();
    }

    private void UpdateSizeHint()
    {
        if (_size != null && _size.Tag is TextBlock)
        {
            TextBlock hint = (TextBlock)_size.Tag;
            hint.Text = ((int)_size.Value).ToString(CultureInfo.InvariantCulture) + " px";
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var work = SystemParameters.WorkArea;
        double left = _pet.Left + _pet.Width + 12;
        double top = _pet.Top;
        if (left + ActualWidth > work.Right) left = _pet.Left - ActualWidth - 12;
        if (top + ActualHeight > work.Bottom) top = work.Bottom - ActualHeight - 8;
        Left = Math.Max(work.Left + 4, left);
        Top = Math.Max(work.Top + 4, top);
    }
}
