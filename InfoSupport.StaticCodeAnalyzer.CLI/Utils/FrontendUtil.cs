using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.WebAPI;
using InfoSupport.StaticCodeAnalyzer.WebApp;

namespace InfoSupport.StaticCodeAnalyzer.CLI.Utils;
internal class FrontendUtil
{
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

    public static void StartWebApp(string? openPath="")
    {

        //System.Diagnostics.Process.Start(openPath!);
        Task.Run(() => WebApiBuilder.Build([], true).Run());
    }
}
