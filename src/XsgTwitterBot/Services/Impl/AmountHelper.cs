using System;
using XsgTwitterBot.Configuration;

namespace XsgTwitterBot.Services.Impl
{
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

            var dynamicFriendMentionAmount =  dailyLimit / stat.TotalWithdrawals;
            var dynamicTagAmount = dynamicFriendMentionAmount / 4m;

            decimal finalAmount;
            if (rewardType == RewardType.Tag)
            {
                finalAmount = dynamicTagAmount < _appSettings.BotSettings.AmountForTweetWithTag
                    ? dynamicTagAmount
                    : _appSettings.BotSettings.AmountForTweetWithTag;
            }
            else
            {
                finalAmount = dynamicFriendMentionAmount < _appSettings.BotSettings.AmountForTweetWithFriendMention
                    ? dynamicFriendMentionAmount
                    : _appSettings.BotSettings.AmountForTweetWithFriendMention;
            }

            return Math.Round(finalAmount, MidpointRounding.ToEven);
        }
    }
}