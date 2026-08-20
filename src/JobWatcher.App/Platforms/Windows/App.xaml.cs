using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace JobWatcher.App.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
		UnhandledException += OnWinUiUnhandledException;
		try
		{
			this.InitializeComponent();
		}
		catch (Exception exception)
		{
			WriteStartupError(exception);
			throw;
		}
	}

	protected override MauiApp CreateMauiApp()
	{
		try
		{
			return MauiProgram.CreateMauiApp();
		}
		catch (Exception exception)
		{
			WriteStartupError(exception);
			throw;
		}
	}

	private static void OnUnhandledException(object sender, System.UnhandledExceptionEventArgs eventArgs)
	{
		WriteStartupError(eventArgs.ExceptionObject as Exception ?? new InvalidOperationException(eventArgs.ExceptionObject.ToString()));
	}

	private static void OnWinUiUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs eventArgs)
	{
		WriteStartupError(eventArgs.Exception);
	}

	private static void WriteStartupError(Exception exception)
	{
		try
		{
			var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JobWatcher");
			Directory.CreateDirectory(directory);
			File.WriteAllText(Path.Combine(directory, "startup-error.log"), exception.ToString());
		}
		catch
		{
			// Startup diagnostics must not hide the original application failure.
		}
	}
}

