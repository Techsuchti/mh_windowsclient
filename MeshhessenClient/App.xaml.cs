using System.Windows;
using System.Windows.Controls;

namespace MeshhessenClient;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ToolTipService.InitialShowDelayProperty.OverrideMetadata(
            typeof(UIElement),
            new FrameworkPropertyMetadata(150));
        ToolTipService.ShowDurationProperty.OverrideMetadata(
            typeof(UIElement),
            new FrameworkPropertyMetadata(20000));

        var window = new MeshCoreMainWindow();
        MainWindow = window;
        window.Show();
    }
}
