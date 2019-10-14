using System.Threading.Tasks;
using XsgTwitterBot.Configuration;
using XsgTwitterBot.Node;

namespace XsgTwitterBot.Services.Impl
{
    public class WithdrawalService  : IWithdrawalService
    {
        private readonly INodeApi _nodeApi;
        private readonly IAmountHelper _amountHelper;

        public WithdrawalService(INodeApi nodeApi, IAmountHelper amountHelper)
        {
            _nodeApi = nodeApi;
            _amountHelper = amountHelper;
        }

        public async Task<bool> CanExecuteAsync(RewardType rewardType)
        {
            var amount = _amountHelper.GetAmount(rewardType);
            var response = await _nodeApi.GetInfoAsync();
            if (response.Result.Balance > amount)
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
            await _nodeApi.SendToAddressAsync(targetAddress, _amountHelper.GetAmount(rewardType));
        }
    }
}
