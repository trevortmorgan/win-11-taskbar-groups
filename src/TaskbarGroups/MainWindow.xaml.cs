using Microsoft.UI.Xaml;
using TaskbarGroups.Config;

namespace TaskbarGroups;

/// <summary>
/// Milestone 0 placeholder: shows the configured groups in an ordinary window.
/// Milestone 1 replaces this with a window that sits on the taskbar.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(640, 200));
        GroupList.ItemsSource = new ConfigStore(ConfigStore.DefaultPath).Load().Groups;
    }
}
