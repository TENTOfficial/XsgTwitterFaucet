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

        public async Task<bool> CanExecuteAsync(RewardType rewardType)
        {
            var response = await _nodeApi.GetInfoAsync();
            
            if (response.Result.Balance >  AmountHelper.GetAmount(_appSettings, rewardType))
            {
                return true;
            }

            return false;
        }

        public async Task<decimal> GetBalanceAsync()
        {
            var response = await _nodeApi.GetInfoAsync();
            return response.Result.Balance;
        }

        public async Task ExecuteAsync(RewardType rewardType, string targetAddress)
        {
            await _nodeApi.SendToAddressAsync(targetAddress, AmountHelper.GetAmount(_appSettings, rewardType));
        }
    }
}
