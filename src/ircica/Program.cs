using System.Text.Json;
using ircica.Services;
using Microsoft.AspNetCore.HttpOverrides;

namespace ircica;

public class Program
{
    public static void Main(string[] args)
    {
        InitializeDirectories();
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.       
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
       {
           options.ForwardedHeaders = ForwardedHeaders.All;
           options.KnownNetworks.Clear();
           options.KnownProxies.Clear();
       });
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddScoped<ToastService>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseForwardedHeaders();

        app.UseStaticFiles();

        app.UseRouting();

        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");

        IrcService.LoadConnections();

        app.Run();

        IrcService.DisconnectAll();
    }
    static void InitializeDirectories()
    {
        Directory.CreateDirectory(C.Paths.Config);
        Directory.CreateDirectory(C.Paths.Data);

        var settingsJson = C.Paths.ConfigFor("settings.json");
        var settingsJsonExample = C.Paths.ConfigFor("settings.example.json");
        if (!File.Exists(settingsJson))
        {
            File.WriteAllText(settingsJsonExample, JsonSerializer.Serialize(C.Settings, C.JsonOpt));
            throw new FileNotFoundException("Must configure settings.json, see settings.example.json");
        }

        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsJson), C.JsonOpt);
        if (settings == null)
            throw new JsonException("Could not parse settings.json");
        else if (!settings.Validate(out var message))
            throw new Exception($"settings.json: {message}");

        C.Settings = settings;
    }
}