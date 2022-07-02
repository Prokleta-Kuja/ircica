namespace ircica.Services;

public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Error
}
public class ToastSettings
{
    public ToastSettings(ToastLevel level, string message, string? action, Action? onClick)
    {
        Id = Guid.NewGuid();
        TimeStamp = DateTime.UtcNow;
        Level = level;
        Message = message;
        Action = action;
        OnClick = onClick;
    }
    public Guid Id { get; set; }
    public DateTime TimeStamp { get; set; }
    public ToastLevel Level { get; set; }
    public string Message { get; set; }
    public string? Action { get; set; }
    public Action? OnClick { get; set; }
}
public class ToastService
{
    public event Action<ToastSettings>? OnShow;
    public void ShowInfo(string message, string? action = null, Action? onClick = null)
        => ShowToast(ToastLevel.Info, message, action, onClick);
    public void ShowSuccess(string message, string? action = null, Action? onClick = null)
        => ShowToast(ToastLevel.Success, message, action, onClick);
    public void ShowWarning(string message, string? action = null, Action? onClick = null)
        => ShowToast(ToastLevel.Warning, message, action, onClick);
    public void ShowError(string message, string? action = null, Action? onClick = null)
        => ShowToast(ToastLevel.Error, message, action, onClick);
    public void ShowToast(ToastLevel level, string message, string? action = null, Action? onClick = null)
        => OnShow?.Invoke(new(level, message, action, onClick));
}