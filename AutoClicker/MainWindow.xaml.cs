using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using AutoClicker.Core;

namespace AutoClicker;

public partial class MainWindow : Window
{
    private readonly ClickEngine engine = new(NativeInput.Click);
    private HwndSource? source;
    private bool registered, running, transitioning, closing, canClose;

    public MainWindow()
    {
        InitializeComponent();
        UpdateRate();
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source.AddHook(WindowMessage);
        registered = NativeInput.RegisterHotKey(source.Handle, NativeInput.HotkeyId, 0x4000, 0x75);
        if (!registered)
        {
            ToggleButton.IsEnabled = false;
            StatusLabel.Text = "F6 jest niedostępny. Zwolnij skrót w innej aplikacji i uruchom ten program ponownie.";
        }
    }

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == NativeInput.WmHotkey && wParam == NativeInput.HotkeyId)
        {
            handled = true;
            _ = ToggleAsync();
        }
        return 0;
    }

    private void RateChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateRate();
    private void UpdateRate()
    {
        if (RateSlider is null || RateLabel is null) return;
        engine.Rate = RateSlider.Value;
        RateLabel.Text = $"Tempo docelowe: {RateSlider.Value:0} klik./s";
    }
    private async void ToggleClicked(object sender, RoutedEventArgs e) => await ToggleAsync();

    private async Task ToggleAsync()
    {
        if (closing || transitioning || !registered) return;
        transitioning = true;
        try
        {
            if (running)
            {
                await engine.StopAsync();
                SetRunning(false);
            }
            else
            {
                SetRunning(true);
                _ = ObserveAsync(engine.Start());
            }
        }
        catch (Exception error)
        {
            SetRunning(false);
            StatusLabel.Text = error.Message;
        }
        finally { transitioning = false; }
    }

    private async Task ObserveAsync(Task worker)
    {
        try { await worker; }
        catch (Exception error)
        {
            try { await engine.StopAsync(); } catch { /* Error shown below. */ }
            if (!closing)
            {
                SetRunning(false);
                StatusLabel.Text = error.Message;
            }
        }
    }

    private void SetRunning(bool value)
    {
        running = value;
        ToggleButton.Content = value ? "Stop (F6)" : "Start (F6)";
        StatusLabel.Text = value ? "Działa — zatrzymaj klawiszem F6" : "Zatrzymany";
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (canClose) return;
        e.Cancel = true;
        if (closing) return;
        closing = true;
        ToggleButton.IsEnabled = false;
        if (registered && source is not null)
            NativeInput.UnregisterHotKey(source.Handle, NativeInput.HotkeyId);
        source?.RemoveHook(WindowMessage);
        try { await engine.DisposeAsync(); }
        catch { /* The window is closing; the worker has already stopped. */ }
        canClose = true;
        Close();
    }
}
