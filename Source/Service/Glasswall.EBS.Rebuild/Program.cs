using Glasswall.EBS.Rebuild.ConfigLoaders;
using Glasswall.EBS.Rebuild.Configuration;
using Glasswall.EBS.Rebuild.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System.IO;

namespace Glasswall.EBS.Rebuild
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                IEbsConfiguration ebsConfiguration = EbsConfigLoader.SetDefaults(new EbsConfiguration());
                hostContext.Configuration.Bind(ebsConfiguration);
                services.AddSingleton<IEbsConfiguration>(x => ebsConfiguration);

                Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                .WriteTo.Console()
                .WriteTo.File(Path.Combine(ebsConfiguration.FORLDERS_PATH, Constants.LogFolder, Constants.LogFile), rollingInterval: RollingInterval.Day)
                .CreateLogger();

                services.AddSingleton<IHttpHandler, HttpHandler>();
                services.AddSingleton<IZipHandler, ZipHandler>();
                services.AddSingleton<IFolderWatcherHandler>(x => new FolderWatcherHandler(x.GetRequiredService<ILogger<FolderWatcherHandler>>(), x.GetRequiredService<IHttpHandler>(), x.GetRequiredService<IZipHandler>(), x.GetRequiredService<IEbsConfiguration>()));
                services.AddHostedService(x => x.GetRequiredService<IFolderWatcherHandler>());
            }).UseSerilog();
        }
    }
}
