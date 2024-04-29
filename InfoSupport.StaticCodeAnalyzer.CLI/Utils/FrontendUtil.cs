using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.WebAPI;
using InfoSupport.StaticCodeAnalyzer.WebApp;

namespace InfoSupport.StaticCodeAnalyzer.CLI.Utils;
internal class FrontendUtil
{
    // Source: https://stackoverflow.com/questions/4580263/how-to-open-in-default-browser-in-c-sharp
    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(url);
        }
        catch
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                throw;
            }
        }
    }

    public static void OpenBrowser(string path)
    {
        Console.WriteLine($"Opening default browser @ http://localhost:5000{path}");
        OpenUrl("http://localhost:5000");
    }

    public static async Task<bool> IsRunning()
    {
        var client = new HttpClient();
        bool online;

        try
        {
            online = await client.GetFromJsonAsync<bool>("http://localhost:5000/api/online");
        } 
        catch
        {
            return false;
        }

        return online;
    }

    public static void StartWebApp()
    {

        //System.Diagnostics.Process.Start(openPath!);
        Task.Run(() => WebApiBuilder.Build([], true).Run());
    }

    public static async Task StartIfNotRunning(string openPath="")
    {
        if (await IsRunning())
        {
            OpenBrowser(openPath);
            return;
        }

        Console.WriteLine("Starting server..");
        StartWebApp();

        // Review: Suggestion to use your health checks to verify if the web server was started
        
        // Arbitrary time to give the web server some time to start up
        // Maybe we could instead read the output stream of the server?
        Thread.Sleep(1000 * 3);
        OpenBrowser(openPath);

        // Wait indefinitely without wasting system resources
        Thread.Sleep(Timeout.Infinite);
    }
}
