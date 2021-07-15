namespace Glasswall.EBS.Rebuild
{
    public static class Constants
    {
        public const string InputFolder = "input";
        public const string OutputFolder = "output";
        public const string ErrorFolder = "error";
        public const string PolicyFolder = "policy";
        public const string LogFolder = "log";
        public const string ProcessingFolder = "gw-processing";
        public const string ZipSearchPattern = "*.zip";
        public const string AllFilesSearchPattern = "*.*";
        public const string FileKey = "file";
        public const string PolicyKey = "contentManagementFlagJson";
        public const string ZipFileApiPath = "/api/rebuild/zipfile";
        public const string MediaType = "application/octet-stream";
        public const string LogFileName = "log.txt";
        public const string PolicyFileName = "policy.json";
        public const string SLASH = "/";
        public const int RetryCount = 3;
        public const double CronJobPeriod = 15;
        public const string ForldersPath = "/data/folder-to-folder";
        public const int CheckCount = 3;
        public const int WaitTimeMiliSec = 3000;
    }
}
