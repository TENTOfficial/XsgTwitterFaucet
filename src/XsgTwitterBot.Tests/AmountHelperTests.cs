using Moq;
using XsgTwitterBot.Configuration;
using XsgTwitterBot.Models;
using XsgTwitterBot.Services;
using XsgTwitterBot.Services.Impl;
using Xunit;

namespace XsgTwitterBot.Tests
{
    public class AmountHelperTests
    {
        private readonly Mock<IStatService> _statServiceMock = new Mock<IStatService>();
        private readonly AppSettings _appSettings;

        public AmountHelperTests()
        {
            _appSettings = new AppSettings
            {
                BotSettings = new BotSettings
                {
                    DailyWithdrawalLimit = 1440,
                    AmountForTweetWithTag = 1.25m,
                    AmountForTweetWithFriendMention = 7.5m
                }
            };
        }
        
        [Fact]
        public void GetAmount_Should_ReturnDynamicRewardForFriendMention()
        {
            _statServiceMock.Setup(x => x.GetPreviousDayStat()).Returns(new Stat
            {
                Id = 1,
                TotalWithdrawals = 333,
                NewUsers = 190,
                WithdrawalAmount = 2233.5m
            });

            var amountHelper = new AmountHelper(_appSettings, _statServiceMock.Object);
            var reward = amountHelper.GetAmount(RewardType.FriendMention);
            
            Assert.True(reward < _appSettings.BotSettings.AmountForTweetWithFriendMention);
            
        }
        
        [Fact]
        public void GetAmount_Should_ReturnFixedRewardForFriendMention()
        {
            _statServiceMock.Setup(x => x.GetPreviousDayStat()).Returns(new Stat
            {
                Id = 1,
                TotalWithdrawals = 10,
                NewUsers = 190,
                WithdrawalAmount = 100.5m
            });

            var amountHelper = new AmountHelper(_appSettings, _statServiceMock.Object);

            var reward = amountHelper.GetAmount(RewardType.FriendMention);
            Assert.True(reward == _appSettings.BotSettings.AmountForTweetWithFriendMention);
        }
        
        [Fact]
        public void GetAmount_Should_ReturnDynamicRewardForTag()
        {
            _statServiceMock.Setup(x => x.GetPreviousDayStat()).Returns(new Stat
            {
                Id = 1,
                TotalWithdrawals = 333,
                NewUsers = 190,
                WithdrawalAmount = 2233.5m
            });

            var amountHelper = new AmountHelper(_appSettings, _statServiceMock.Object);
            var reward = amountHelper.GetAmount(RewardType.Tag);
            Assert.True(reward < _appSettings.BotSettings.AmountForTweetWithTag);
        }
        
        [Fact]
        public void GetAmount_Should_ReturnFixedRewardForTag()
        {
            _statServiceMock.Setup(x => x.GetPreviousDayStat()).Returns(new Stat
            {
                Id = 1,
                TotalWithdrawals = 1,
                NewUsers = 190,
                WithdrawalAmount = 12.5m
            });

            var amountHelper = new AmountHelper(_appSettings, _statServiceMock.Object);
            var reward = amountHelper.GetAmount(RewardType.Tag);
            Assert.True(reward == _appSettings.BotSettings.AmountForTweetWithTag);
        }
    }
}