namespace JobWatcher.App;

public partial class MainPage : ContentPage
{
	private readonly DashboardViewModel viewModel;

	public MainPage(DashboardViewModel viewModel)
	{
		this.viewModel = viewModel;
		InitializeComponent();
		BindingContext = viewModel;
	}

    protected override async void OnAppearing()
	{
		base.OnAppearing();
        await viewModel.RefreshAsync();
    }

    private async void OnRunClicked(object? sender, EventArgs e)
    {
        await viewModel.StartRunAsync();
    }

    private void OnPauseClicked(object? sender, EventArgs e) => viewModel.TogglePause();

    private void OnStopClicked(object? sender, EventArgs e) => viewModel.StopRun();
}
