namespace ircica.Pages;

public partial class Download : IDisposable
{
    readonly PeriodicTimer _periodicTimer = new(TimeSpan.FromSeconds(2));
    protected override void OnInitialized()
    {
        RunTimer();
    }

    async void RunTimer()
    {
        while (await _periodicTimer.WaitForNextTickAsync())
            StateHasChanged();
    }

    public void Dispose()
    {
        _periodicTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}