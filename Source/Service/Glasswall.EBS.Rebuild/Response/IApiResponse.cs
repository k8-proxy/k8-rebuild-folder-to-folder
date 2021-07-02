using System;
using System.Net.Http;

namespace Glasswall.EBS.Rebuild.Response
{
    public interface IApiResponse
    {
        bool Success { get; set; }
        string Message { get; set; }
        Exception Exception { get; set; }
        HttpContent Content { get; set; }
    }
}
