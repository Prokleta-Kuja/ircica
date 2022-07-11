namespace ircica.Services;

public static class TasksService
{
    static readonly PeriodicTimer s_cleanUpTimer;
    static TasksService()
    {
        s_cleanUpTimer = new(TimeSpan.FromHours(1));
        CleanUpRun();
    }

    static async void CleanUpRun()
    {
        while (await s_cleanUpTimer.WaitForNextTickAsync())
            IrcService.CleanLogs();
    }
}