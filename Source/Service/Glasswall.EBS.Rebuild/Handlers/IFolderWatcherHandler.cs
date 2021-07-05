using Microsoft.Extensions.Hosting;
using System;

namespace Glasswall.EBS.Rebuild.Handlers
{
    public interface IFolderWatcherHandler : IHostedService, IDisposable
    {
    }
}
