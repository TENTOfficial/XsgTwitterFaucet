using XsgTwitterBot.Configuration;

namespace XsgTwitterBot.Services.Impl
{
    public static class AmountHelper
    {
        public static decimal GetAmount(AppSettings appSettings, RewardType rewardType)
        {
            return rewardType == RewardType.FriendMention
                ? appSettings.BotSettings.AmountForTweetWithFriendMention
                : appSettings.BotSettings.AmountForTweetWithTag;
        }
    }
}