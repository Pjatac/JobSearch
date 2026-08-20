using JobWatcher.Http;

namespace JobWatcher.App;

public partial class GlassdoorSessionPage : ContentPage
{
    private readonly string sessionPath = Path.Combine(FileSystem.AppDataDirectory, "data", "secrets", "glassdoor-session.txt");

    public GlassdoorSessionPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        CookieEditor.Text = string.Empty;
        UserAgentEditor.Text = string.Empty;
        AcceptLanguageEditor.Text = string.Empty;
        StatusLabel.Text = File.Exists(sessionPath) ? "A session is saved. Paste new values to replace it." : "No session is saved.";
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var cookie = CookieEditor.Text?.Trim() ?? string.Empty;
        var userAgent = UserAgentEditor.Text?.Trim() ?? string.Empty;
        var acceptLanguage = AcceptLanguageEditor.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cookie) || string.IsNullOrWhiteSpace(userAgent) || string.IsNullOrWhiteSpace(acceptLanguage))
        {
            await DisplayAlertAsync("Check the session", "Cookie, User-Agent, and Accept-Language are all required.", "OK");
            return;
        }

        var rawHeaders = $"cookie: {cookie}{Environment.NewLine}user-agent: {userAgent}{Environment.NewLine}accept-language: {acceptLanguage}";
        if (BrowserSessionFile.Parse(rawHeaders) is null)
        {
            await DisplayAlertAsync("Check the session", "The Cookie value could not be read.", "OK");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        var temporaryPath = $"{sessionPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, rawHeaders);
            File.Move(temporaryPath, sessionPath, overwrite: true);
            CookieEditor.Text = string.Empty;
            UserAgentEditor.Text = string.Empty;
            AcceptLanguageEditor.Text = string.Empty;
            StatusLabel.Text = "Session saved.";
            await DisplayAlertAsync("Session saved", "Glassdoor session was saved. The values are not shown again.", "OK");
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private async void OnClearClicked(object? sender, EventArgs e)
    {
        if (!File.Exists(sessionPath) || !await DisplayAlertAsync("Clear session", "Remove the saved Glassdoor session?", "Clear", "Cancel"))
        {
            return;
        }

        File.Delete(sessionPath);
        CookieEditor.Text = string.Empty;
        UserAgentEditor.Text = string.Empty;
        AcceptLanguageEditor.Text = string.Empty;
        StatusLabel.Text = "No session is saved.";
    }
}
