namespace ircica.Services;

public static class TasksService
{
    static readonly PeriodicTimer s_buildIndexTimer;
    static TasksService()
    {
        s_buildIndexTimer = new(TimeSpan.FromHours(12));
        BuildIndexRun();
    }

    static async void BuildIndexRun()
    {
        while (await s_buildIndexTimer.WaitForNextTickAsync())
            IrcService.BuildIndex();
    }
}