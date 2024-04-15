using InfoSupport.StaticCodeAnalyzer.WebApp.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace InfoSupport.StaticCodeAnalyzer.WebApp;

public class WebAppBuilder
{
    public static WebAssemblyHost Build(string[] args, ushort apiBasePort = 5000)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri($"http://localhost:{apiBasePort}/api/") });
        builder.Services.AddSingleton<NavBarStateService>();

        return builder.Build();
    }
}
