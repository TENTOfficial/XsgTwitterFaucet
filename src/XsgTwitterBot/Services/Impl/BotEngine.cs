using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
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
        private readonly IStatService _statService;
        private IFilteredStream _stream;
        private readonly LiteCollection<Reward> _rewardCollection;
        private readonly ILogger _logger;
        private int _retryCount;
        static readonly object ProcessingLock = new object();

        public BotEngine(AppSettings settings, 
            IMessageParser messageParser, 
            IWithdrawalService withdrawalService,
            ISyncCheckService syncCheckService, 
            IStatService statService,
            LiteCollection<Reward> rewardCollection)
        {
            _settings = settings;
            _messageParser = messageParser;
            _withdrawalService = withdrawalService;
            _syncCheckService = syncCheckService;
            _statService = statService;
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
                RateLimit.RateLimitTrackerMode = RateLimitTrackerMode.TrackAndAwait;
                _stream.StartStreamMatchingAnyConditionAsync();
                
                _logger.Information("BotEngine has been started.");
            }
        }
 
        private void OnStreamOnMatchingTweetReceived(object sender, MatchedTweetReceivedEventArgs e)
        {
            lock (ProcessingLock)
            {
                _retryCount = 0;
                
                _logger.Information("Received tweet '{Text}' from {Name} ", e.Tweet.FullText, e.Tweet.CreatedBy.Name);
                
                if (string.IsNullOrWhiteSpace(e.Tweet.InReplyToScreenName))
                {
                    var text = Regex.Replace(e.Tweet.FullText, @"\r\n?|\n", " ");
                    var targetAddress = _messageParser.GetValidAddressAsync(text).GetAwaiter().GetResult();
                    if (string.IsNullOrWhiteSpace(targetAddress))
                        return;

                    var isUserLegit = ValidateUser(e.Tweet.CreatedBy);
                    if (!isUserLegit)
                    {
                        _logger.Information("Ignoring tweet from user {@User}", e.Tweet.CreatedBy);
                        return;
                    }

                    var isTweetTextValid = ValidateTweetText(e.Tweet.Text);
                    if (!isTweetTextValid)
                    {
                        _logger.Information("Tweet is invalid");
                        Tweet.PublishTweet(string.Format(_settings.BotSettings.MessageTweetInvalid, e.Tweet.CreatedBy.ScreenName, _settings.BotSettings.MinTweetLenght) , new PublishTweetOptionalParameters
                        {
                            InReplyToTweet = e.Tweet
                        });
                        
                        return;
                    }
                    
                    var reward = _rewardCollection.FindOne(x => x.Id == e.Tweet.CreatedBy.Id);
                    var replyMessage = reward != null ? HandleExistingUser(e, targetAddress, reward) : HandleNewUser(e, targetAddress);

                    _logger.Information("Replied with message '{ReplyMessage}'", replyMessage);

                    Tweet.PublishTweet(replyMessage, new PublishTweetOptionalParameters
                    {
                        InReplyToTweet = e.Tweet
                    });

                    _logger.Information("Faucet balance: {balance} XSG", _withdrawalService.GetBalanceAsync().GetAwaiter().GetResult());
                }
                else
                {
                    _logger.Information("Received tweet does not match required criteria.");
                }
            }
        }

        private void OnStreamStreamStopped(object sender, StreamExceptionEventArgs e)
        {
            if (e.Exception == null) return;

            _logger.Error(e.Exception, "Failed to process stream {@StreamExceptionEventArgs}", e);

            if (e.Exception is WebException)
            {
                _retryCount++;

                var minutes = 1000 * _retryCount * 60;
                
                _logger.Warning(e.Exception, "Retry to start bot in {minutes}", minutes);
                
                Thread.Sleep(minutes);
                
                _stream.StartStreamMatchingAnyConditionAsync();
            }
        }

        private bool ValidateUser(IUser user)
        {
            if (user.FriendsCount >= user.FollowersCount && user.FriendsCount > 10)
            {
                return true;
            }

            var ratio = user.FollowersCount / user.FriendsCount;
            if (ratio < _settings.BotSettings.UserRatio)
            {
                if (user.FollowersCount > _settings.BotSettings.FollowersCountThreshold)
                    return false;
            }

            return true;
        }

        private bool ValidateTweetText(string text)
        {
            return text.Length >= _settings.BotSettings.MinTweetLenght;
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
            _statService.AddStat(DateTime.UtcNow, _settings.BotSettings.AmountForTweet, true);

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
                replyMessage = GenerateMessageDailyLimitReached(e.Tweet.CreatedBy.ScreenName);
            }
            else
            {
                replyMessage = string.Format(_settings.BotSettings.MessageRewarded, e.Tweet.CreatedBy.ScreenName, _settings.BotSettings.AmountForTweet);

                _withdrawalService.ExecuteAsync(targetAddress).GetAwaiter().GetResult();
                _statService.AddStat(DateTime.UtcNow, _settings.BotSettings.AmountForTweet, false);

                reward.LastRewardDate = DateTime.UtcNow;
                reward.Withdrawals++;
            }

            _rewardCollection.Update(reward);

            return replyMessage;
        }

        private string GenerateMessageDailyLimitReached(string screenName)
        {
            var diff = DateTime.UtcNow.Date.AddDays(1) -DateTime.UtcNow;
            var hours = (int) diff.TotalHours;
            var minutes = (int) diff.TotalMinutes - hours * 60;

            var tryAgainIn = "Try again in";
            if (hours == 0)
            {
                tryAgainIn += $" {minutes} minute" + (minutes > 1 ? "s" : "");
            }
            else
            {
                tryAgainIn += $" {hours} hour" + (hours > 1 ? "s" : "");
                if (minutes > 0)
                {
                    tryAgainIn += $" {minutes} minute" + (minutes > 1 ? "s" : "");
                }
            }

            return string.Format(_settings.BotSettings.MessageDailyLimitReached, screenName, tryAgainIn);
        }

        public void Dispose()
        {
            _logger.Information("Stopping BotEngine...");
           _stream?.StopStream();
           _stream = null;
        }
    }
}
