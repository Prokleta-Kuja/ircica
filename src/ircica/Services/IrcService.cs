namespace ircica;

public static class IrcService
{
    static CancellationTokenSource? _cts;
    public static List<IrcIndexer> Indexers { get; private set; } = new();
    public static void LoadIndexers()
    {
        foreach (var server in C.Settings.Servers)
        {
            var opt = new IrcOptions(C.Settings.UserName, C.Settings.RealName, C.Settings.NickName, server);
            var indexer = new IrcIndexer(opt);
            Indexers.Add(indexer);
        }
    }
    public static void StartAll()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts = null;
        }

        _cts = new();

        foreach (var indexer in Indexers)
            _ = indexer.Start(_cts.Token);
    }
    public static void StopAll() => _cts?.Cancel();
}