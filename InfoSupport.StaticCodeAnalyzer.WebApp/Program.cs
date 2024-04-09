using InfoSupport.StaticCodeAnalyzer.WebApp;
using InfoSupport.StaticCodeAnalyzer.WebApp.Services;

using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5173") });
builder.Services.AddSingleton<NavBarStateService>();

await builder.Build().RunAsync();
