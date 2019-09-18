using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LiteDB;
using Serilog;
using Tweetinvi;
using Tweetinvi.Logic.Model;
using Tweetinvi.Models;
using Tweetinvi.Parameters;
using XsgTwitterBot.Configuration;
using XsgTwitterBot.Models;

namespace XsgTwitterBot.Services.Impl
{
    public class BotEngine : BotEngineBase
    {
        private readonly ILogger _logger = Log.ForContext<BotEngine>();
        private readonly AppSettings _appSettings;
        private readonly IMessageParser _messageParser;
        private readonly IWithdrawalService _withdrawalService;
        private readonly IStatService _statService;
        private readonly LiteCollection<Reward> _rewardCollection;

        private string _cursorId = "current";
        private readonly LiteCollection<Cursor> _cursor;

        public BotEngine(AppSettings appSettings,
            IMessageParser messageParser, 
            IWithdrawalService withdrawalService,
            IStatService statService,
            LiteCollection<Reward> rewardCollection, 
            LiteCollection<Cursor> cursor)
        {
            _appSettings = appSettings;
            _messageParser = messageParser;
            _withdrawalService = withdrawalService;
            _statService = statService;
            _rewardCollection = rewardCollection;
            _cursor = cursor;
        }
        
        protected override void RunLoop()
        {
            var sleepMultiplier = 1;

            SetUserCredentials();
            
            while (!CancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var query = string.Join(" ", _appSettings.BotSettings.TrackKeywords);
                    var cursor = _cursor.FindById(_cursorId);
                    
                    var searchParameter = new SearchTweetsParameters(query)
                    {
                        TweetSearchType =  TweetSearchType.OriginalTweetsOnly,
                        SearchType = SearchResultType.Recent
                    };

                    if (cursor != null)
                    {
                        searchParameter.SinceId = cursor.TweetId;
                        searchParameter.MaximumNumberOfResults = 25;
                    }
                    else
                    {
                        searchParameter.MaximumNumberOfResults = 1;
                    }

                    ProcessTweets(Search.SearchTweets(searchParameter).OrderBy(x => x.Id).ToList());
                }
                catch (Exception ex)
                {
                    _logger.Error(ex,"Failed to process tweets");
                    sleepMultiplier = 10;
                    SetUserCredentials();
                }
                finally
                {
                    CancellationTokenSource.Token.WaitHandle.WaitOne(_appSettings.ProcessingFrequency * sleepMultiplier);
                    sleepMultiplier = 1;
                }

                if (CancellationTokenSource.Token.WaitHandle.WaitOne(_appSettings.ProcessingFrequency))
                {
                    break;
                }
            }
        }

        private void ProcessTweets(List<ITweet> tweets)
        {
            foreach (var tweet in tweets)
            {
                 _logger.Information("Received tweet ({Id}) '{Text}' from {Name} ", tweet.Id, tweet.FullText, tweet.CreatedBy.Name);
                
                if (string.IsNullOrWhiteSpace(tweet.InReplyToScreenName))
                {
                    var text = Regex.Replace(tweet.FullText, @"\r\n?|\n", " ");
                    var targetAddress = _messageParser.GetValidAddressAsync(text).GetAwaiter().GetResult();
                    if (string.IsNullOrWhiteSpace(targetAddress))
                    {
                        UpsertCursor(tweet.Id);
                        continue;
                    }
                        
                    var isUserLegit = ValidateUser(tweet.CreatedBy);
                    if (!isUserLegit)
                    {
                        _logger.Information("Ignoring tweet from user {@User}", tweet.CreatedBy);
                        UpsertCursor(tweet.Id);
                        continue;
                    }

                    var isTweetTextValid = ValidateTweetText(tweet.Text);
                    if (!isTweetTextValid)
                    {
                        _logger.Information("Tweet is invalid");
                        Tweet.PublishTweet(string.Format(_appSettings.BotSettings.MessageTweetInvalid, tweet.CreatedBy.ScreenName, _appSettings.BotSettings.MinTweetLenght) , new PublishTweetOptionalParameters
                        {
                            InReplyToTweet = tweet
                        });
                        
                        UpsertCursor(tweet.Id);
                        continue;
                    }
                    
                    var reward = _rewardCollection.FindOne(x => x.Id == tweet.CreatedBy.Id);
                    var replyMessage = reward != null ? HandleExistingUser(tweet, targetAddress, reward) : HandleNewUser(tweet, targetAddress);

                    _logger.Information("Replied with message '{ReplyMessage}'", replyMessage);

                    Tweet.PublishTweet(replyMessage, new PublishTweetOptionalParameters
                    {
                        InReplyToTweet = tweet
                    });

                    _logger.Information("Faucet balance: {balance} XSG", _withdrawalService.GetBalanceAsync().GetAwaiter().GetResult());
                }
                else
                {
                    _logger.Information("Received tweet does not match required criteria.");
                }

                UpsertCursor(tweet.Id);
            }
        }

        private void UpsertCursor(long tweetId)
        {
            _cursor.Upsert(_cursorId, new Cursor { Id = _cursorId, TweetId = tweetId, UpdateAt = DateTime.UtcNow});
        }
        
         private bool ValidateUser(IUser user)
        {
            if (user.FriendsCount >= user.FollowersCount && user.FriendsCount > 10)
            {
                return true;
            }

            var ratio = user.FollowersCount / user.FriendsCount;
            if (ratio < _appSettings.BotSettings.UserRatio)
            {
                if (user.FollowersCount > _appSettings.BotSettings.FollowersCountThreshold)
                    return false;
            }

            return true;
        }

        private bool ValidateTweetText(string text)
        {
            return text.Length >= _appSettings.BotSettings.MinTweetLenght;
        }

        private string HandleNewUser(ITweet tweet, string targetAddress)
        {
            var canWithdraw = _withdrawalService.CanExecuteAsync().GetAwaiter().GetResult();
            if (!canWithdraw)
            {
                _logger.Warning("Not enough funds for withdrawal.");
                return string.Format(_appSettings.BotSettings.MessageFaucetDrained, tweet.CreatedBy.ScreenName);
            }

            _withdrawalService.ExecuteAsync(targetAddress).GetAwaiter().GetResult();
            _statService.AddStat(DateTime.UtcNow, _appSettings.BotSettings.AmountForTweet, true);

            var reward = new Reward
            {
                Id = tweet.CreatedBy.Id,
                Followers = tweet.CreatedBy.FollowersCount,
                LastRewardDate = DateTime.UtcNow,
                Withdrawals = 1
            };

            _rewardCollection.Insert(reward);

            return string.Format(_appSettings.BotSettings.MessageRewarded, tweet.CreatedBy.ScreenName, _appSettings.BotSettings.AmountForTweet);
        }

        private string HandleExistingUser(ITweet tweet, string targetAddress, Reward reward)
        {
            var canWithdraw = _withdrawalService.CanExecuteAsync().GetAwaiter().GetResult();
            if (!canWithdraw)
            {
                _logger.Warning("Not enough funds for withdrawal.");
                return string.Format(_appSettings.BotSettings.MessageFaucetDrained, tweet.CreatedBy.ScreenName);
            }

            string replyMessage;
            reward.Followers = tweet.CreatedBy.FollowersCount;

            if (reward.Withdrawals >= reward.Followers)
            {
                replyMessage = string.Format(_appSettings.BotSettings.MessageReachedLimit, tweet.CreatedBy.ScreenName);
            }
            else if (reward.LastRewardDate.Date.Equals(DateTime.UtcNow.Date))
            {
                replyMessage = GenerateMessageDailyLimitReached(tweet.CreatedBy.ScreenName);
            }
            else
            {
                replyMessage = string.Format(_appSettings.BotSettings.MessageRewarded, tweet.CreatedBy.ScreenName, _appSettings.BotSettings.AmountForTweet);

                _withdrawalService.ExecuteAsync(targetAddress).GetAwaiter().GetResult();
                _statService.AddStat(DateTime.UtcNow, _appSettings.BotSettings.AmountForTweet, false);

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

            return string.Format(_appSettings.BotSettings.MessageDailyLimitReached, screenName, tryAgainIn);
        }

        private void SetUserCredentials()
        {
            Auth.SetUserCredentials(
                _appSettings.TwitterSettings.ConsumerKey,
                _appSettings.TwitterSettings.ConsumerSecret,
                _appSettings.TwitterSettings.AccessToken,
                _appSettings.TwitterSettings.AccessTokenSecret);
        }
    }
}