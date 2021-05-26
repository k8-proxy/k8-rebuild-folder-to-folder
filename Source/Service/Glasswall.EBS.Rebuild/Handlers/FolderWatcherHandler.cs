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
        private Timer _timer;

        public FolderWatcherHandler(string path, ILogger<FolderWatcherHandler> logger, IHttpHandler httpHandler, IZipHandler zipHandler)
        {
            Path = path;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpHandler = httpHandler ?? throw new ArgumentNullException(nameof(httpHandler));
            _zipHandler = zipHandler ?? throw new ArgumentNullException(nameof(zipHandler));
        }

        public string Path { get; private set; }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            double.TryParse(Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.CronjobPeriod), out double cronjobPeriodInSeconds);
            _timer = new Timer(PullFolder, null, TimeSpan.Zero, TimeSpan.FromSeconds(cronjobPeriodInSeconds));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        private async void PullFolder(object state)
        {
            await ProcessFolder();
        }

        public async Task ProcessFolder()
        {
            string processingFolderPath = System.IO.Path.Combine(Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ForldersPath), Constants.ProcessingFolder);
            string tempFolderPath = System.IO.Path.Combine(processingFolderPath, Guid.NewGuid().ToString());
            try
            {
                MoveInputFilesToTempLocation(tempFolderPath);
                foreach (string inputFile in Directory.EnumerateFiles(tempFolderPath, Constants.ZipSearchPattern))
                {
                    try
                    {
                        MultipartFormDataContent multiFormData = new MultipartFormDataContent();
                        FileStream fs = File.OpenRead(inputFile);
                        multiFormData.Add(new StreamContent(fs), Constants.FileKey, System.IO.Path.GetFileName(inputFile));
                        string url = $"{Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.RebuildApiBaseUrl)}{Constants.ZipFileApiPath}";
                        IApiResponse response = await _httpHandler.PostAsync(url, multiFormData);
                        string rawFilePath = inputFile.Substring(0, inputFile.Substring(0, inputFile.LastIndexOf(Constants.SLASH)).LastIndexOf(Constants.SLASH));
                        rawFilePath = rawFilePath.Substring(0, rawFilePath.LastIndexOf(Constants.SLASH));
                        string tempOutputFilePath = string.Empty;
                        string outputFilePath = string.Empty;

                        if (response.Success)
                        {
                            outputFilePath = System.IO.Path.Combine(rawFilePath, Constants.OutputFolder, System.IO.Path.GetFileName(inputFile));
                            outputFilePath = NextAvailableFilename(outputFilePath);
                            tempOutputFilePath = System.IO.Path.Combine(tempFolderPath, $"{Guid.NewGuid()}", System.IO.Path.GetFileName(inputFile));

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

                            _logger.LogInformation($"Successfully processed the file {System.IO.Path.GetFileName(inputFile)}");
                        }
                        else
                        {
                            tempOutputFilePath = System.IO.Path.Combine(rawFilePath, Constants.ErrorFolder, System.IO.Path.GetFileName(inputFile));
                            tempOutputFilePath = NextAvailableFilename(tempOutputFilePath);
                            File.Move(inputFile, tempOutputFilePath);
                            _logger.LogInformation($"Error while processing the file {System.IO.Path.GetFileName(inputFile)} and error is {response.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Exception occured while processing file {System.IO.Path.GetFileName(inputFile)}, errorMessage: {ex.Message} and errorStackTrace: {ex.StackTrace}");
                    }
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

        private void MoveInputFilesToTempLocation(string tempFolderPath)
        {
            if (!Directory.Exists(tempFolderPath))
            {
                Directory.CreateDirectory(tempFolderPath);
            }

            foreach (string file in Directory.EnumerateFiles(Path, Constants.ZipSearchPattern))
            {
                string destFile = System.IO.Path.Combine(tempFolderPath, System.IO.Path.GetFileName(file));
                if (!File.Exists(destFile))
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
    }
}