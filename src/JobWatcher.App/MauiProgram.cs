using Microsoft.Extensions.Logging;

using JobWatcher.Configuration;

namespace JobWatcher.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		builder.Services.AddSingleton<JobWatcherSettingsStore>();
		builder.Services.AddSingleton<SourceProfileValidator>();
		builder.Services.AddSingleton<RunStateService>();
		builder.Services.AddSingleton<ManualRunService>();
		builder.Services.AddSingleton<DashboardViewModel>();
		builder.Services.AddSingleton<MainPage>();
		builder.Services.AddSingleton<SourceProfilesPage>();
		builder.Services.AddSingleton<ClassificationPage>();
		builder.Services.AddSingleton<ResultsPage>();
		builder.Services.AddSingleton<AppShell>();

		return builder.Build();
	}
}
