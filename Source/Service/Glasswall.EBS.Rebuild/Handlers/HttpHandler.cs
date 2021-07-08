using Glasswall.EBS.Rebuild.Response;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Glasswall.EBS.Rebuild.Handlers
{
    public class HttpHandler : IHttpHandler
    {
        private readonly HttpClient _client;
        private readonly ILogger<HttpHandler> _logger;
        private readonly IHostEnvironment _hostingEnvironment;

        public HttpHandler(ILogger<HttpHandler> logger, IHostEnvironment hostingEnvironment)
        {
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
            if (_hostingEnvironment.IsDevelopment())
            {
                HttpClientHandler httpClientHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
                _client = new HttpClient(httpClientHandler);
            }
            else
            {
                _client = new HttpClient();
            }
            _client.Timeout = TimeSpan.FromMilliseconds(System.Threading.Timeout.Infinite);
        }

        public async Task<IApiResponse> PostAsync(string url, HttpContent data)
        {
            IApiResponse apiResponse = new ApiResponse();
            try
            {
                HttpResponseMessage response = await _client.PostAsync(url, data);
                apiResponse.Success = response.IsSuccessStatusCode;
                apiResponse.Message = !apiResponse.Success ? await response.Content.ReadAsStringAsync() : string.Empty;
                if (response.Content?.Headers?.ContentType?.MediaType == Constants.MediaType)
                {
                    apiResponse.Content = response.Content;
                }
            }
            catch (Exception ex)
            {
                apiResponse.Exception = ex;
            }
            return apiResponse;
        }
    }
}
