namespace Glasswall.EBS.Rebuild.Configuration
{
    public class EbsConfiguration : IEbsConfiguration
    {
        public int RETRY_COUNT { get; set; }
        public double CRONJOB_PERIOD { get; set; }
        public string FORLDERS_PATH { get; set; }
        public string REBUILD_API_BASE_URL { get; set; }
    }
}
