using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SoundboardLite.App.Controls;

/// <summary>
/// A "click then press a key" hotkey picker, used both for the global default PTT key (Settings) and
/// the optional per-clip override (clip editor). Read-only so normal typing can't edit the text;
/// <see cref="OnPreviewKeyDown"/> captures the next key press instead. Escape clears to null (falls
/// back to the global default where this is used as an override).
/// </summary>
public sealed class KeyCaptureBox : TextBox
{
    public static readonly DependencyProperty VirtualKeyCodeProperty = DependencyProperty.Register(
        nameof(VirtualKeyCode), typeof(int?), typeof(KeyCaptureBox),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnVirtualKeyCodeChanged));

    public int? VirtualKeyCode
    {
        get => (int?)GetValue(VirtualKeyCodeProperty);
        set => SetValue(VirtualKeyCodeProperty, value);
    }

    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText), typeof(string), typeof(KeyCaptureBox),
        new FrameworkPropertyMetadata("(click, then press a key)", OnVirtualKeyCodeChanged));

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public KeyCaptureBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        Cursor = Cursors.Hand;
        UpdateText();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Let Tab keep doing its normal job (move focus away) instead of being captured as the hotkey.
        if (key == Key.Tab)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;
        VirtualKeyCode = key == Key.Escape ? null : KeyInterop.VirtualKeyFromKey(key);
    }

    private static void OnVirtualKeyCodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((KeyCaptureBox)d).UpdateText();

    private void UpdateText() =>
        Text = VirtualKeyCode is { } vk ? KeyInterop.KeyFromVirtualKey(vk).ToString() : PlaceholderText;
}
