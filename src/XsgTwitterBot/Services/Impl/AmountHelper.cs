using XsgTwitterBot.Configuration;

namespace XsgTwitterBot.Services.Impl
{
    public interface IAmountHelper
    {
        decimal GetAmount(RewardType rewardType);
    }

    public class AmountHelper : IAmountHelper
    {
        private readonly AppSettings _appSettings;
        private readonly IStatService _statService;

        public AmountHelper(AppSettings appSettings, IStatService statService)
        {
            _appSettings = appSettings;
            _statService = statService;
        }
        
        public decimal GetAmount(RewardType rewardType)
        {
            var stat = _statService.GetPreviousDayStat();

            if (stat == null)
            {
                return rewardType == RewardType.FriendMention
                    ? _appSettings.BotSettings.AmountForTweetWithFriendMention
                    : _appSettings.BotSettings.AmountForTweetWithTag;
            }
            
            var dailyLimit = _appSettings.BotSettings.DailyWithdrawalLimit;

            var dynamicFriendMentionAmount = (int) dailyLimit / stat.TotalWithdrawals;
            var dynamicTagAmount = dynamicFriendMentionAmount / 4;

            if (rewardType == RewardType.Tag)
                return dynamicTagAmount < _appSettings.BotSettings.AmountForTweetWithTag
                    ? dynamicTagAmount
                    : _appSettings.BotSettings.AmountForTweetWithTag;

            return dynamicFriendMentionAmount < _appSettings.BotSettings.AmountForTweetWithFriendMention
                ? dynamicFriendMentionAmount
                : _appSettings.BotSettings.AmountForTweetWithFriendMention;
        }
    }
}