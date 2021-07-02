using Glasswall.EBS.Rebuild.Configuration;

namespace Glasswall.EBS.Rebuild.ConfigLoaders
{
    public static class EbsConfigLoader
    {
        public static IEbsConfiguration SetDefaults(IEbsConfiguration configuration)
        {
            configuration.RETRY_COUNT = Constants.RetryCount;
            configuration.CRONJOB_PERIOD = Constants.CronJobPeriod;
            configuration.FORLDERS_PATH = Constants.ForldersPath;
            return configuration;
        }
    }
}
