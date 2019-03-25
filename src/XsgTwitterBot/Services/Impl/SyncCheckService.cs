using System.Threading.Tasks;
using Serilog;
using XsgTwitterBot.Node;

namespace XsgTwitterBot.Services.Impl
{
    public class SyncCheckService : ISyncCheckService
    {
        private readonly INodeApi _nodeApi;
        private readonly IExplorerApi _explorerApi;
        private readonly ILogger _logger;

        public SyncCheckService(INodeApi nodeApi, IExplorerApi explorerApi)
        {
            _nodeApi = nodeApi;
            _explorerApi = explorerApi;

            _logger = Log.ForContext<SyncCheckService>();
        }

        public async Task WaitUntilSyncedAsync()
        {
            bool isSynced = false;

            while (!isSynced)
            {
                var explorerBlock =  await _explorerApi.GetLastBlock();
                var getInfo = await _nodeApi.GetInfoAsync();

                if (getInfo.Result.Blocks >= explorerBlock)
                {
                    isSynced = true;
                    _logger.Information("Node is synced.");
                }
                else
                {
                    _logger.Information($"Waiting for node sync: ({getInfo.Result.Blocks} / {explorerBlock})");
                    await Task.Delay(30000);
                }
            }
        }
    }
}