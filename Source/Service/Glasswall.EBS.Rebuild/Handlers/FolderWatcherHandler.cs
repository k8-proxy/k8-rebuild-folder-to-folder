using Glasswall.EBS.Rebuild.Configuration;
using Glasswall.EBS.Rebuild.Response;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Glasswall.EBS.Rebuild.Handlers
{
    public class FolderWatcherHandler : IFolderWatcherHandler
    {
        private readonly ILogger<FolderWatcherHandler> _logger;
        private readonly IHttpHandler _httpHandler;
        private readonly IZipHandler _zipHandler;
        private readonly IEbsConfiguration _configuration;
        private Timer _timer;

        public FolderWatcherHandler(ILogger<FolderWatcherHandler> logger, IHttpHandler httpHandler, IZipHandler zipHandler, IEbsConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpHandler = httpHandler ?? throw new ArgumentNullException(nameof(httpHandler));
            _zipHandler = zipHandler ?? throw new ArgumentNullException(nameof(zipHandler));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Path = System.IO.Path.Combine(_configuration.FORLDERS_PATH, Constants.InputFolder);
        }

        public string Path { get; private set; }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _timer = new Timer(PullFolder, null, TimeSpan.Zero, TimeSpan.FromSeconds(_configuration.CRONJOB_PERIOD));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        private async void PullFolder(object state)
        {
            ValidateConfiguration();
            await ProcessFolder();
        }

        public async Task ProcessFolder()
        {
            string processingFolderPath = System.IO.Path.Combine(_configuration.FORLDERS_PATH, Constants.ProcessingFolder);
            string tempFolderPath = System.IO.Path.Combine(processingFolderPath, Guid.NewGuid().ToString());
            try
            {
                MoveInputFilesToTempLocation(tempFolderPath);
                foreach (string inputFile in Directory.EnumerateFiles(tempFolderPath, Constants.ZipSearchPattern))
                {
                    bool processed = false;
                    int retryCount = 0;
                    do
                    {
                        processed = await ProcessFile(tempFolderPath, inputFile, retryCount);

                        if (!processed)
                        {
                            retryCount += 1;
                        }
                    } while (!processed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception occured while processing folder errorMessage: {ex.Message} and errorStackTrace: {ex.StackTrace}");
            }
            finally
            {
                if (Directory.Exists(tempFolderPath))
                {
                    Directory.Delete(tempFolderPath, true);
                }
            }
        }

        private async Task<bool> ProcessFile(string tempFolderPath, string inputFile, int retryCount)
        {
            bool success = false;
            string fileName = System.IO.Path.GetFileName(inputFile);
            string rawFilePath = string.Empty;
            string tempOutputFilePath;

            try
            {
                if (retryCount > 0)
                {
                    _logger.LogInformation($"Retry Count: {retryCount} for file: {fileName}");
                }

                MultipartFormDataContent multiFormData = new MultipartFormDataContent();
                FileStream fs = File.OpenRead(inputFile);
                multiFormData.Add(new StreamContent(fs), Constants.FileKey, fileName);
                multiFormData.Add(new StringContent(await GetPolicyJson(fileName)), Constants.PolicyKey);
                string url = $"{_configuration.REBUILD_API_BASE_URL}{Constants.ZipFileApiPath}";
                IApiResponse response = await _httpHandler.PostAsync(url, multiFormData);
                rawFilePath = inputFile.Substring(0, inputFile.Substring(0, inputFile.LastIndexOf(Constants.SLASH)).LastIndexOf(Constants.SLASH));
                rawFilePath = rawFilePath.Substring(0, rawFilePath.LastIndexOf(Constants.SLASH));

                if (response.Success)
                {
                    string outputFilePath = System.IO.Path.Combine(rawFilePath, Constants.OutputFolder, fileName);
                    outputFilePath = NextAvailableFilename(outputFilePath);
                    tempOutputFilePath = System.IO.Path.Combine(tempFolderPath, $"{Guid.NewGuid()}", fileName);

                    if (!Directory.Exists(System.IO.Path.GetDirectoryName(tempOutputFilePath)))
                    {
                        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(tempOutputFilePath));
                    }

                    using (FileStream fileStream = new FileStream(tempOutputFilePath, FileMode.Create, FileAccess.Write))
                    {
                        if (response.Content != null)
                        {
                            await response.Content.CopyToAsync(fileStream);
                            SyncUnSupportedFilesWithSanitizedZipFile(tempFolderPath, inputFile, tempOutputFilePath, outputFilePath);
                        }
                    }

                    _logger.LogInformation($"Successfully processed the file: {fileName}");
                    success = true;
                }
                else
                {
                    if (response.Exception != null)
                    {
                        _logger.LogError($"Error while processing file: {fileName} errorMessage: {response.Exception.Message} and errorStackTrace: {response.Exception.StackTrace}");
                    }
                    else
                    {
                        _logger.LogError($"Error while processing file: {fileName} errorMessage: {response.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception occured while processing file {fileName}, errorMessage: {ex.Message} and errorStackTrace: {ex.StackTrace}");
            }
            finally
            {
                if (retryCount == _configuration.RETRY_COUNT)
                {
                    success = true;
                    tempOutputFilePath = System.IO.Path.Combine(rawFilePath, Constants.ErrorFolder, fileName);
                    tempOutputFilePath = NextAvailableFilename(tempOutputFilePath);
                    File.Move(inputFile, tempOutputFilePath);
                }
            }
            return success;
        }

        private void MoveInputFilesToTempLocation(string tempFolderPath)
        {
            if (!Directory.Exists(tempFolderPath))
            {
                Directory.CreateDirectory(tempFolderPath);
            }

            foreach (string file in Directory.EnumerateFiles(Path, Constants.ZipSearchPattern))
            {
                string destFile = System.IO.Path.Combine(tempFolderPath, System.IO.Path.GetFileName(file));
                if (!File.Exists(destFile) && CanMoveFile(file))
                {
                    File.Move(file, destFile);
                }
            }
        }

        private string NextAvailableFilename(string path)
        {
            string numberPattern = " ({0})";

            if (!File.Exists(path))
            {
                return path;
            }

            if (System.IO.Path.HasExtension(path))
            {
                return GetNextFilename(path.Insert(path.LastIndexOf(System.IO.Path.GetExtension(path)), numberPattern));
            }

            return GetNextFilename(path + numberPattern);
        }

        private string GetNextFilename(string pattern)
        {
            string tmp = string.Format(pattern, 1);
            if (tmp == pattern)
            {
                throw new ArgumentException("The pattern must include an index place-holder", "pattern");
            }

            if (!File.Exists(tmp))
            {
                return tmp;
            }

            int min = 1, max = 2;

            while (File.Exists(string.Format(pattern, max)))
            {
                min = max;
                max *= 2;
            }

            while (max != min + 1)
            {
                int pivot = (max + min) / 2;
                if (File.Exists(string.Format(pattern, pivot)))
                {
                    min = pivot;
                }
                else
                {
                    max = pivot;
                }
            }

            return string.Format(pattern, max);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        private void SyncUnSupportedFilesWithSanitizedZipFile(string tempFolderPath, string inputFile, string tempOutputFilePath, string outputFilePath)
        {
            try
            {
                string input = $"{Guid.NewGuid()}";
                string output = $"{Guid.NewGuid()}";
                string tempSourceFolderPath = System.IO.Path.Combine(tempFolderPath, input);
                string tempDestinationFolderPath = System.IO.Path.Combine(tempFolderPath, output);

                if (!Directory.Exists(tempSourceFolderPath))
                {
                    Directory.CreateDirectory(tempSourceFolderPath);
                }

                if (!Directory.Exists(tempDestinationFolderPath))
                {
                    Directory.CreateDirectory(tempDestinationFolderPath);
                }

                _zipHandler.ExtractZipFile(inputFile, null, tempSourceFolderPath);
                _zipHandler.ExtractZipFile(tempOutputFilePath, null, tempDestinationFolderPath);
                IEnumerable<string> inputZipFiles = Directory.GetFiles(tempSourceFolderPath, Constants.AllFilesSearchPattern, SearchOption.AllDirectories).Select(x => x[(x.IndexOf($"{Constants.SLASH}{input}{Constants.SLASH}") + input.Length + 2)..]);
                IEnumerable<string> outputZipFiles = Directory.GetFiles(tempDestinationFolderPath, Constants.AllFilesSearchPattern, SearchOption.AllDirectories).Select(x => x[(x.IndexOf($"{Constants.SLASH}{output}{Constants.SLASH}") + output.Length + 2)..]);
                List<string> unsupportedFiles = inputZipFiles.Except(outputZipFiles).ToList();

                if (unsupportedFiles.Any())
                {
                    unsupportedFiles.ForEach(x =>
                    {
                        string destinationFilePath = System.IO.Path.Combine(tempDestinationFolderPath, x);
                        string directoryName = System.IO.Path.GetDirectoryName(destinationFilePath);

                        if (!Directory.Exists(directoryName))
                        {
                            Directory.CreateDirectory(directoryName);
                        }

                        File.Copy(System.IO.Path.Combine(tempSourceFolderPath, x), destinationFilePath);
                    });
                }

                _zipHandler.CreateZipFile(outputFilePath, null, tempDestinationFolderPath);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception occured while processing file {System.IO.Path.GetFileName(inputFile)} errorMessage: {ex.Message} and errorStackTrace: {ex.StackTrace}");
            }
        }

        private bool CanMoveFile(string filePath)
        {
            string fileName = string.Empty;
            try
            {
                fileName = System.IO.Path.GetFileName(filePath);
                long length1 = new FileInfo(filePath).Length;
                long length2;
                int count = 0;
                do
                {
                    Thread.Sleep(Constants.WaitTimeMiliSec);
                    length2 = new FileInfo(filePath).Length;
                    count++;
                } while (!length1.Equals(length2) && count != Constants.CheckCount);
                return length1.Equals(length2) && length1 != 0 && length2 != 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception occured while checking the file {fileName} before moving to processing folder, errorMessage: {ex.Message} and errorStackTrace: {ex.StackTrace}");
                return false;
            }
        }

        private async Task<string> GetPolicyJson(string zipFileName)
        {
            try
            {
                string policyFilePath = System.IO.Path.Combine(_configuration.FORLDERS_PATH, Constants.PolicyFolder, Constants.PolicyFileName);
                if (File.Exists(policyFilePath))
                {
                    return await File.ReadAllTextAsync(policyFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception occured while fetching policy for file {zipFileName} ,errorMessage: {ex.Message} and errorStackTrace: {ex.StackTrace}");
            }
            return string.Empty;
        }

        private void ValidateConfiguration()
        {
            try
            {
                string policyFolderPath = System.IO.Path.Combine(_configuration.FORLDERS_PATH, Constants.PolicyFolder);
                if (!Directory.Exists(policyFolderPath))
                {
                    Directory.CreateDirectory(policyFolderPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception occured while creating policy folder ,errorMessage: {ex.Message} and errorStackTrace: {ex.StackTrace}");
            }
        }
    }
}