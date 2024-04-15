using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.CLI.Utils;

namespace InfoSupport.StaticCodeAnalyzer.CLI.Commands;
public class LaunchCommand : ICommandHandler
{

    // Source: https://stackoverflow.com/questions/4580263/how-to-open-in-default-browser-in-c-sharp
    private void OpenUrl(string url)
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

    private void OpenBrowser()
    {
        Console.WriteLine("Opening default browser @ http://localhost:5000");
        OpenUrl("http://localhost:5000");
    }

    public async Task Run(ArgsUtil args)
    {
        if (await FrontendUtil.IsRunning())
        {
            OpenBrowser();
            return;
        }

        Console.WriteLine("Starting server..");
        FrontendUtil.StartWebApp();

        Thread.Sleep(1000 * 3);
        OpenBrowser();

        // Wait indefinitely without wasting system resources
        Thread.Sleep(Timeout.Infinite);
    }
}
