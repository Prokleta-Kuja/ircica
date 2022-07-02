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

        app.Run();
    }
    static void InitializeDirectories()
    {
        var config = new DirectoryInfo(C.Paths.Config);
        config.Create();
        var data = new DirectoryInfo(C.Paths.Data);
        data.Create();
    }
}