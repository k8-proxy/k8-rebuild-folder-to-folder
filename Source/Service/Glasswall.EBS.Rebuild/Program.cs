using Glasswall.EBS.Rebuild.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace Glasswall.EBS.Rebuild
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                .WriteTo.Console()
                .WriteTo.File(Path.Combine(Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ForldersPath), Constants.LogFolder, Constants.LogFile), rollingInterval: RollingInterval.Day)
                .CreateLogger();
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                string inputFolderPath = Path.Combine(Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ForldersPath), Constants.InputFolder);
                services.AddSingleton<IHttpHandler, HttpHandler>();
                services.AddSingleton<IZipHandler, ZipHandler>();
                services.AddSingleton<IFolderWatcherHandler>(x => new FolderWatcherHandler(inputFolderPath, x.GetRequiredService<ILogger<FolderWatcherHandler>>(), x.GetRequiredService<IHttpHandler>(), x.GetRequiredService<IZipHandler>()));
                services.AddHostedService(x => x.GetRequiredService<IFolderWatcherHandler>());
            }).UseSerilog();
        }
    }
}
