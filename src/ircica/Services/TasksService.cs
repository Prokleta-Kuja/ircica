namespace ircica.Services;

public static class TasksService
{
    static readonly PeriodicTimer s_buildIndexTimer;
    static readonly PeriodicTimer s_expireDownloadRequestTimer;
    static readonly PeriodicTimer s_lastActiveReconnectTimer;
    static TasksService()
    {
        s_buildIndexTimer = new(TimeSpan.FromHours(C.Settings.BuildIndexEveryHours));
        s_expireDownloadRequestTimer = new(TimeSpan.FromMinutes(1));
        s_lastActiveReconnectTimer = new(TimeSpan.FromMinutes(C.Settings.RestartAfterInactivityMinutes));
    }

    static async void BuildIndexRun()
    {
        while (await s_buildIndexTimer.WaitForNextTickAsync())
            IrcService.BuildIndex();
    }
    static async void ExpireDownloadRun()
    {
        while (await s_expireDownloadRequestTimer.WaitForNextTickAsync())
            IrcService.ExpireDownloads(DateTime.UtcNow.AddMinutes(-C.Settings.ExpireDownloadsOlderThanMinutes));
    }
    static async void LastActiveReconnectRun()
    {
        while (await s_lastActiveReconnectTimer.WaitForNextTickAsync())
            IrcService.LastActiveReconnect(DateTime.UtcNow.AddMinutes(-C.Settings.RestartAfterInactivityMinutes));
    }
    public static void Start()
    {
        BuildIndexRun();
        ExpireDownloadRun();
        LastActiveReconnectRun();
    }
}