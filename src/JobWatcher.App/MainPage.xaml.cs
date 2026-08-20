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
        await viewModel.InitializeAsync();
    }

    private async void OnRunClicked(object? sender, EventArgs e)
    {
        var task = viewModel.ToggleRunAsync();
        RunButton.Text = viewModel.RunButtonText;
        await task;
        RunButton.Text = viewModel.RunButtonText;
    }
}
