using System.Linq;
using System.Threading.Tasks;
using XsgTwitterBot.Configuration;
using XsgTwitterBot.Node;

namespace XsgTwitterBot.Services.Impl
{
    public class WithdrawalService  : IWithdrawalService
    {
        private readonly AppSettings _appSettings;
        private readonly INodeApi _nodeApi;

        public WithdrawalService(AppSettings appSettings, INodeApi nodeApi)
        {
            _appSettings = appSettings;
            _nodeApi = nodeApi;
        }

        public async Task<string[]> GetDepositAddressesAsync()
        {
            var result = await _nodeApi.ListAddressGroupingsAsync();
            return result.Result.SelectMany(x => x, (objects, objects1) => objects1[0].ToString()).ToArray();
        }

        public async Task<bool> CanExecuteAsync()
        {
            var response = await _nodeApi.GetInfoAsync();

            if (response.Result.Balance > _appSettings.BotSettings.AmountForTweet)
            {
                return true;
            }

            return false;
        }

        public async Task ExecuteAsync(string targetAddress)
        {
            await _nodeApi.SendToAddressAsync(targetAddress, _appSettings.BotSettings.AmountForTweet);
        }
    }
}
