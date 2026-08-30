using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace STORM_STICKERS;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        _ = IconGenerator.GenerateAppIconAsync();

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            File.WriteAllText(@"E:\STORM STICKERS\crash_log.txt", $"AppDomain UnhandledException: {e.ExceptionObject.ToString()}");
        };

        this.UnhandledException += (sender, e) =>
        {
            File.WriteAllText(@"E:\STORM STICKERS\crash_log.txt", $"WinUI UnhandledException: {e.Message}\n{e.Exception?.ToString()}");
            e.Handled = true;
        };

        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
