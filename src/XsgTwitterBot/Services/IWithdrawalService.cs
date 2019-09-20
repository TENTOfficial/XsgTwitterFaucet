using System.Threading.Tasks;

namespace XsgTwitterBot.Services
{
    public interface IWithdrawalService
    {
        Task<bool> CanExecuteAsync(RewardType rewardType);
        Task ExecuteAsync(RewardType rewardType, string targetAddress);
        Task<decimal> GetBalanceAsync();
    }

    public enum RewardType
    {
        Tag,
        FriendMention
    }
}