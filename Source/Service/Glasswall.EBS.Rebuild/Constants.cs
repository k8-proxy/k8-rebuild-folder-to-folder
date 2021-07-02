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
        public const int RetryCount = 3;
        public const double CronjobPeriod = 15;
        public const string ForldersPath = "/data/folder-to-folder";
    }
}
