namespace JobWatcher.App;

public partial class AppShell : Shell
{
	public AppShell(MainPage mainPage, SourceProfilesPage sourceProfilesPage, ClassificationPage classificationPage, ResultsPage resultsPage)
	{
		InitializeComponent();
		Routing.RegisterRoute("glassdoor-session", typeof(GlassdoorSessionPage));
		Items.Add(new ShellContent
		{
			Title = "Overview",
			ContentTemplate = new DataTemplate(() => mainPage),
			Route = "overview"
		});
		Items.Add(new ShellContent
		{
			Title = "Results",
			ContentTemplate = new DataTemplate(() => resultsPage),
			Route = "results"
		});
		Items.Add(new ShellContent
		{
			Title = "Classification",
			ContentTemplate = new DataTemplate(() => classificationPage),
			Route = "classification"
		});
		Items.Add(new ShellContent
		{
			Title = "Profiles",
			ContentTemplate = new DataTemplate(() => sourceProfilesPage),
			Route = "profiles"
		});
	}
}
