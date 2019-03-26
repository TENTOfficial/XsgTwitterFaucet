using System.Threading;
using LiteDB;
using Serilog;
using Tweetinvi;
using Tweetinvi.Core.Extensions;
using Tweetinvi.Events;
using Tweetinvi.Parameters;
using Tweetinvi.Streaming;
using XsgTwitterBot.Configuration;
using XsgTwitterBot.Models;

namespace XsgTwitterBot.Services.Impl
{
    public class BotEngine
    {
        private readonly AppSettings _settings;
        private readonly IMessageParser _messageParser;
        private readonly IWithdrawalService _withdrawalService;
        private readonly ISyncCheckService _syncCheckService;
        private readonly LiteCollection<Reward> _rewardCollection;
        private readonly ILogger _logger;

        public BotEngine(AppSettings settings, IMessageParser messageParser, IWithdrawalService withdrawalService, ISyncCheckService syncCheckService, LiteCollection<Reward> rewardCollection)
        {
            _settings = settings;
            _messageParser = messageParser;
            _withdrawalService = withdrawalService;
            _syncCheckService = syncCheckService;
            _rewardCollection = rewardCollection;

            _logger = Log.ForContext<BotEngine>();
        }

        public IFilteredStream Start()
        {
            _logger.Information("Starting BotEngine...");

            Thread.Sleep(30 * 1000);

            _syncCheckService.WaitUntilSyncedAsync().GetAwaiter().GetResult();

            var stream = Stream.CreateFilteredStream(Auth.SetUserCredentials(
                _settings.TwitterSettings.ConsumerKey,
                _settings.TwitterSettings.ConsumerSecret,
                _settings.TwitterSettings.AccessToken,
                _settings.TwitterSettings.AccessTokenSecret));

            _settings.BotSettings.TrackKeywords.ForEach(keyword => stream.AddTrack(keyword));

            stream.MatchingTweetReceived += OnStreamOnMatchingTweetReceived;
            stream.StreamStopped += OnStreamStreamStopped;
            stream.StartStreamMatchingAnyConditionAsync();

            _logger.Information("BotEngine has been started.");
            return stream;
        }

        private void OnStreamOnMatchingTweetReceived(object sender, MatchedTweetReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Tweet.InReplyToScreenName))
            {
                var targetAddress = _messageParser.GetValidAddressAsync(e.Tweet.Text).GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(targetAddress))
                    return;

                _logger.Information("Received tweet '{Text}' from {Name} ", e.Tweet.Text, e.Tweet.CreatedBy.Name);

                var reward = _rewardCollection.FindOne(x => x.Id == e.Tweet.CreatedBy.Id);
                var replyMessage = reward != null ? HandleExistingUser(e, targetAddress, reward) : HandleNewUser(e, targetAddress);

                _logger.Information("Replied with message '{ReplyMessage}'", replyMessage);

                Tweet.PublishTweet(replyMessage, new PublishTweetOptionalParameters
                {
                    InReplyToTweet = e.Tweet
                });
            }
        }

        private void OnStreamStreamStopped(object sender, StreamExceptionEventArgs e)
        {
            if (e.Exception != null)
            {
                _logger.Error(e.Exception, "Failed to process tweet");
            }
        }

        private string HandleNewUser(MatchedTweetReceivedEventArgs e, string targetAddress)
        {
            var canWithdraw = _withdrawalService.CanExecuteAsync().GetAwaiter().GetResult();
            if (!canWithdraw)
            {
                _logger.Warning("Not enough funds for withdrawal.");
                return string.Format(_settings.BotSettings.MessageFaucetDrained);
            }

            _withdrawalService.ExecuteAsync(targetAddress).GetAwaiter().GetResult();

            var reward = new Reward
            {
                Id = e.Tweet.CreatedBy.Id,
                Followers = e.Tweet.CreatedBy.FollowersCount,
                Withdrawals = 1
            };

            _rewardCollection.Insert(reward);

            return string.Format(_settings.BotSettings.MessageRewarded, _settings.BotSettings.AmountForTweet);
        }

        private string HandleExistingUser(MatchedTweetReceivedEventArgs e, string targetAddress, Reward reward)
        {
            var canWithdraw = _withdrawalService.CanExecuteAsync().GetAwaiter().GetResult();
            if (!canWithdraw)
            {
                _logger.Warning("Not enough funds for withdrawal.");
                return string.Format(_settings.BotSettings.MessageFaucetDrained);
            }

            string replyMessage;
            reward.Followers = e.Tweet.CreatedBy.FollowersCount;

            if (reward.Withdrawals >= reward.Followers)
            {
                replyMessage = _settings.BotSettings.MessageReachedLimit;
            }
            else
            {
                _withdrawalService.ExecuteAsync(targetAddress).GetAwaiter().GetResult();
                replyMessage = string.Format(_settings.BotSettings.MessageRewarded, _settings.BotSettings.AmountForTweet);
                reward.Withdrawals++;
            }

            _rewardCollection.Update(reward);

            return replyMessage;
        }
    }
}