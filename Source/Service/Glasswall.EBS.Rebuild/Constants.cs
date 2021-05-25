namespace Glasswall.EBS.Rebuild
{
    public static class Constants
    {
        public const string InputFolder = "input";
        public const string OutputFolder = "output";
        public const string ErrorFolder = "error";
        public const string LogFolder = "log";
        public const string ProcessingFolder = "gw-processing";
        public const string ZipSearchPattern = "*.zip";
        public const string AllFilesSearchPattern = "*.*";
        public const string FileKey = "file";
        public const string ZipFileApiPath = "/api/rebuild/zipfile";
        public const string MediaType = "application/octet-stream";
        public const string LogFile = "log.txt";
        public const string SLASH = "/";

        public static class EnvironmentVariables
        {
            public const string RebuildApiBaseUrl = "REBUILD_API_BASE_URL";
            public const string CronjobPeriod = "CRONJOB_PERIOD";
            public const string ForldersPath = "FORLDERS_PATH";
        }
    }
}
