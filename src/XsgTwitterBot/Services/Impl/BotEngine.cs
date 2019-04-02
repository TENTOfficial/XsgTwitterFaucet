﻿using System;
using LiteDB;
using Serilog;
using Tweetinvi;
using Tweetinvi.Core.Extensions;
using Tweetinvi.Events;
using Tweetinvi.Models;
using Tweetinvi.Parameters;
using Tweetinvi.Streaming;
using XsgTwitterBot.Configuration;
using XsgTwitterBot.Models;

namespace XsgTwitterBot.Services.Impl
{
    public class BotEngine : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly IMessageParser _messageParser;
        private readonly IWithdrawalService _withdrawalService;
        private readonly ISyncCheckService _syncCheckService;
        private IFilteredStream _stream;
        private readonly LiteCollection<Reward> _rewardCollection;
        private readonly ILogger _logger;

        static readonly object ProcessingLock = new object();

        public BotEngine(AppSettings settings, IMessageParser messageParser, IWithdrawalService withdrawalService, ISyncCheckService syncCheckService, LiteCollection<Reward> rewardCollection)
        {
            _settings = settings;
            _messageParser = messageParser;
            _withdrawalService = withdrawalService;
            _syncCheckService = syncCheckService;
            _rewardCollection = rewardCollection;

            _logger = Log.ForContext<BotEngine>();
        }

        public void Start()
        {
            lock (ProcessingLock)
            {
                _logger.Information("Starting BotEngine...");

                _syncCheckService.WaitUntilSyncedAsync().GetAwaiter().GetResult();
                
                _stream = Stream.CreateFilteredStream(Auth.SetUserCredentials(
                    _settings.TwitterSettings.ConsumerKey,
                    _settings.TwitterSettings.ConsumerSecret,
                    _settings.TwitterSettings.AccessToken,
                    _settings.TwitterSettings.AccessTokenSecret));

                _settings.BotSettings.TrackKeywords.ForEach(keyword => _stream.AddTrack(keyword));

                _stream.MatchingTweetReceived += OnStreamOnMatchingTweetReceived;
                _stream.StreamStopped += OnStreamStreamStopped;
                _stream.StartStreamMatchingAnyConditionAsync();

                _logger.Information("BotEngine has been started.");
            }
        }

        private void OnStreamOnMatchingTweetReceived(object sender, MatchedTweetReceivedEventArgs e)
        {
            lock (ProcessingLock)
            {
                if (string.IsNullOrWhiteSpace(e.Tweet.InReplyToScreenName))
                {
                    var text = e.Tweet.FullText;
                    var targetAddress = _messageParser.GetValidAddressAsync(text).GetAwaiter().GetResult();
                    if (string.IsNullOrWhiteSpace(targetAddress))
                        return;

                    _logger.Information("Received tweet '{Text}' from {Name} ", text, e.Tweet.CreatedBy.Name);

                    var reward = _rewardCollection.FindOne(x => x.Id == e.Tweet.CreatedBy.Id);
                    var replyMessage = reward != null ? HandleExistingUser(e, targetAddress, reward) : HandleNewUser(e, targetAddress);

                    _logger.Information("Replied with message '{ReplyMessage}'", replyMessage);

                    Tweet.PublishTweet(replyMessage, new PublishTweetOptionalParameters
                    {
                        InReplyToTweet = e.Tweet
                    });

                    _logger.Information("Faucet balance: {balance} XSG", _withdrawalService.GetBalanceAsync().GetAwaiter().GetResult());
                }
            }
        }

        private void OnStreamStreamStopped(object sender, StreamExceptionEventArgs e)
        {
            if (e.Exception == null) return;

            _logger.Error(e.Exception, "Failed to process tweet {@StreamExceptionEventArgs}", e);

            Start();
        }

        private string HandleNewUser(MatchedTweetReceivedEventArgs e, string targetAddress)
        {
            var canWithdraw = _withdrawalService.CanExecuteAsync().GetAwaiter().GetResult();
            if (!canWithdraw)
            {
                _logger.Warning("Not enough funds for withdrawal.");
                return string.Format(_settings.BotSettings.MessageFaucetDrained, e.Tweet.CreatedBy.ScreenName);
            }

            _withdrawalService.ExecuteAsync(targetAddress).GetAwaiter().GetResult();

            var reward = new Reward
            {
                Id = e.Tweet.CreatedBy.Id,
                Followers = e.Tweet.CreatedBy.FollowersCount,
                LastRewardDate = DateTime.UtcNow,
                Withdrawals = 1
            };

            _rewardCollection.Insert(reward);

            return string.Format(_settings.BotSettings.MessageRewarded, e.Tweet.CreatedBy.ScreenName, _settings.BotSettings.AmountForTweet);
        }

        private string HandleExistingUser(MatchedTweetReceivedEventArgs e, string targetAddress, Reward reward)
        {
            var canWithdraw = _withdrawalService.CanExecuteAsync().GetAwaiter().GetResult();
            if (!canWithdraw)
            {
                _logger.Warning("Not enough funds for withdrawal.");
                return string.Format(_settings.BotSettings.MessageFaucetDrained, e.Tweet.CreatedBy.ScreenName);
            }

            string replyMessage;
            reward.Followers = e.Tweet.CreatedBy.FollowersCount;

            if (reward.Withdrawals >= reward.Followers)
            {
                replyMessage = string.Format(_settings.BotSettings.MessageReachedLimit, e.Tweet.CreatedBy.ScreenName);
            }
            else if (reward.LastRewardDate.Date.Equals(DateTime.UtcNow.Date))
            {
                replyMessage = string.Format(_settings.BotSettings.MessageDailyLimitReached, e.Tweet.CreatedBy.ScreenName);
            }
            else
            {
                replyMessage = string.Format(_settings.BotSettings.MessageRewarded, e.Tweet.CreatedBy.ScreenName, _settings.BotSettings.AmountForTweet);

                _withdrawalService.ExecuteAsync(targetAddress).GetAwaiter().GetResult();

                reward.LastRewardDate = DateTime.UtcNow;
                reward.Withdrawals++;
            }

            _rewardCollection.Update(reward);

            return replyMessage;
        }

        public void Dispose()
        {
           _stream?.StopStream();
        }
    }
}
