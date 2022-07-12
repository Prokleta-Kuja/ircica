using ircica.Shared;

namespace ircica.Pages;

public partial class Download : IDisposable
{
    Modal _logModal = null!;
    List<string> _log = new(0);
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
    async Task OpenLog(IrcDownload download)
    {
        _log = download.Log;
        await _logModal.ToggleOpenAsync();
    }
    static void RemoveDownload(IrcDownload download)
    {
        download.Stop();
        IrcService.Downloads.Remove(download);
    }

    public void Dispose()
    {
        _periodicTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}