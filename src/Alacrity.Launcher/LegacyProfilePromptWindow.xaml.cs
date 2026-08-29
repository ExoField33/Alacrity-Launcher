using System.Windows;

namespace Alacrity.Launcher;

public partial class LegacyProfilePromptWindow : Window
{
    private LegacyProfilePromptWindow()
    {
        InitializeComponent();
    }

    public bool IsolateFiles { get; private set; }

    public static bool? Show(Window owner)
    {
        var window = new LegacyProfilePromptWindow {
            Owner = owner
        };
        return window.ShowDialog() == true ? window.IsolateFiles : null;
    }

    private void UseCurrentFiles_Click(object sender, RoutedEventArgs eventArgs)
    {
        IsolateFiles = false;
        DialogResult = true;
    }

    private void IsolateFiles_Click(object sender, RoutedEventArgs eventArgs)
    {
        IsolateFiles = true;
        DialogResult = true;
    }
}
