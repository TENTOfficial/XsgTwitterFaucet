using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using LiteDB;
using Serilog;
using Tweetinvi;
using Tweetinvi.Core.Extensions;
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
        private readonly LiteCollection<FriendTagMap> _friendTagMapCollection;
        private readonly LiteCollection<UserTweetMap> _userTweetMapCollection;

        public BotEngine(AppSettings appSettings,
            IMessageParser messageParser, 
            IWithdrawalService withdrawalService,
            IStatService statService,
            LiteCollection<Reward> rewardCollection, 
            LiteCollection<FriendTagMap> friendTagMapCollection,
            LiteCollection<UserTweetMap> userTweetMapCollection)
        {
            _appSettings = appSettings;
            _messageParser = messageParser;
            _withdrawalService = withdrawalService;
            _statService = statService;
            _rewardCollection = rewardCollection;
            _friendTagMapCollection = friendTagMapCollection;
            _userTweetMapCollection = userTweetMapCollection;
        }
        
        protected override void RunLoop()
        {
            var sleepMultiplier = 1;

            SetUserCredentials();
            
            while (!CancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var folowerIds = User.GetFollowerIds("GiveawayXsg").ToList();
                    var friendIds = User.GetFriendIds("GiveawayXsg").ToList();
                    folowerIds.Except(friendIds).ForEach(u => User.FollowUser(u));
                   
                    var messages = Message.GetLatestMessages(new GetMessagesParameters
                    {
                        Count = 50
                    });

                    if (messages == null)
                    {
                        _logger.Information("Rate limit reached.");
                        CancellationTokenSource.Token.WaitHandle.WaitOne(_appSettings.ProcessingFrequency * 5);
                        continue;
                    }
                        

                    foreach (var message in messages)
                    {
                        var url = message?.Entities?.Urls.Select(u => u.ExpandedURL).FirstOrDefault();

                        var strTweetId = url?.Split("/").LastOrDefault();
                        if (strTweetId != null)
                        {
                            if (long.TryParse(strTweetId, out var tweetId))
                            {
                                try
                                {
                                    var tweet = Tweet.GetTweet(tweetId);

                                    var isProcessed = _userTweetMapCollection.FindById($"{tweet.CreatedBy.Id}@{tweet.Id}");
                                    if (isProcessed != null)
                                    {
                                        continue;
                                    }
                                    
                                    _logger.Information("Received tweet ({Id}) '{Text}' from {Name} ", tweet.Id, tweet.FullText, tweet.CreatedBy.Name);
                                    
                                    // tweet can not be a reply
                                    if (!string.IsNullOrWhiteSpace(tweet.InReplyToScreenName))
                                    {
                                        _logger.Information("Ignoring tweet from user {@User}", tweet.CreatedBy);
                                        Message.PublishMessage($"Response to tweet ({tweet.Id}) - replies are not supported.", tweet.CreatedBy.Id);
                                        continue;
                                    }

                                    // tweet must contain hashtags
                                    var requiredHashTags = string.Join(" ", _appSettings.BotSettings.TrackKeywords);
                                    var hasValidHashTags = tweet.Hashtags.Select(x => x.Text).All(x => requiredHashTags.Contains(x));
                                    if(!hasValidHashTags)
                                    {
                                        _logger.Information("Ignoring tweet from user {@User}", tweet.CreatedBy);
                                        Message.PublishMessage($"Response to tweet ({tweet.Id}) - Tweet should contain the following hashtags: {_appSettings.BotSettings.TrackKeywords}", tweet.CreatedBy.Id);
                                        continue;
                                    }
                                    
                                    // tweet must contain valid xsg address
                                    var text = Regex.Replace(tweet.FullText, @"\r\n?|\n", " ");
                                    var targetAddress = _messageParser.GetValidAddressAsync(text).GetAwaiter().GetResult();
                                    if (string.IsNullOrWhiteSpace(targetAddress))
                                    {
                                        _logger.Information("Ignoring tweet from user {@User}", tweet.CreatedBy);
                                        Message.PublishMessage($"Response to tweet ({tweet.Id}) - Tweet should contain valid xsg transparent address", tweet.CreatedBy.Id);
                                        continue;
                                    }

                                    // user can not be a scammer
                                    //var isUserLegit = ValidateUser(tweet.CreatedBy);
                                    //if (!isUserLegit)
                                    //{
//                                        _logger.Information("Ignoring tweet from user {@User}", tweet.CreatedBy);
//                                        Message.PublishMessage($"Response to tweet ({tweet.Id}) - Is your account a fake one? You you have to little followers.", tweet.CreatedBy.Id);
//                                        continue;
//                                    }
                                    
                                    // tweet can not be too short
                                    var isTweetTextValid = ValidateTweetText(tweet.Text);
                                    if (!isTweetTextValid)
                                    {
                                        _logger.Information("Ignoring tweet from user {@User}", tweet.CreatedBy);
                                        Message.PublishMessage($"Response to tweet ({tweet.Id}) - Your tweet is too short", tweet.CreatedBy.Id);
                                        continue;
                                    }

                                    // --- processing payout
                                    var rewardType = GetRewardType(tweet);
                                    var reward = _rewardCollection.FindOne(x => x.Id == tweet.CreatedBy.Id);
                                    var replyMessage = reward != null
                                        ? HandleExistingUser(tweet, targetAddress, reward, rewardType)
                                        : HandleNewUser(tweet, targetAddress, rewardType);

                                    _userTweetMapCollection.Insert(new UserTweetMap
                                    {
                                        Id = $"{tweet.CreatedBy.Id}@{tweet.Id}"
                                    });
                                    
                                    Tweet.PublishTweet(replyMessage, new PublishTweetOptionalParameters
                                    {
                                        InReplyToTweet = tweet
                                    });
                                    
                                    Message.PublishMessage($"Response to tweet ({tweet.Id}) - {replyMessage}", tweet.CreatedBy.Id);

                                    _logger.Information("Replied with message '{ReplyMessage}'", replyMessage);
                                    _logger.Information("Faucet balance: {balance} XSG", _withdrawalService.GetBalanceAsync().GetAwaiter().GetResult());
                                }
                                catch(Exception exception)
                                {
                                    _logger.Error(exception, "Processing tweet messages failed");
                                }
                            }
                        }
                    }
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
 
        private IUser GetFriendMentioned(ITweet tweet)
        {
            var user = tweet.UserMentions.FirstOrDefault();
            if (user != null)
            {
                var friends = User.GetFriends(tweet.CreatedBy);
                return friends.FirstOrDefault(x => x.Id == user.Id);    
            }

            return null;
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

        private RewardType GetRewardType(ITweet tweet)
        {
            var friend = GetFriendMentioned(tweet);
            var rewardType = friend != null ? RewardType.FriendMention : RewardType.Tag;
            
            if (friend != null)
            {
                _friendTagMapCollection.Insert(new FriendTagMap
                {
                    Id = $"{tweet.CreatedBy.Id}@{friend.Id}"
                });
            }

            return rewardType;
        }
        
        private string HandleNewUser(ITweet tweet, string targetAddress, RewardType rewardType)
        {
            var canWithdraw = _withdrawalService.CanExecuteAsync(rewardType).GetAwaiter().GetResult();
            if (!canWithdraw)
            {
                _logger.Warning("Not enough funds for withdrawal.");
                return string.Format(_appSettings.BotSettings.MessageFaucetDrained, tweet.CreatedBy.Name);
            }

            _withdrawalService.ExecuteAsync(rewardType, targetAddress).GetAwaiter().GetResult();
            _statService.AddStat(DateTime.UtcNow, AmountHelper.GetAmount(_appSettings, rewardType), true);

            var reward = new Reward
            {
                Id = tweet.CreatedBy.Id,
                Followers = tweet.CreatedBy.FollowersCount,
                LastRewardDate = DateTime.UtcNow,
                Withdrawals = 1
            };

            _rewardCollection.Insert(reward);
 
            return string.Format(_appSettings.BotSettings.MessageRewarded, tweet.CreatedBy.ScreenName,
                AmountHelper.GetAmount(_appSettings, rewardType));
        }

        private string HandleExistingUser(ITweet tweet, string targetAddress, Reward reward, RewardType rewardType)
        {
            var canWithdraw = _withdrawalService.CanExecuteAsync(rewardType).GetAwaiter().GetResult();
            if (!canWithdraw)
            {
                _logger.Warning("Not enough funds for withdrawal.");
                return string.Format(_appSettings.BotSettings.MessageFaucetDrained, tweet.CreatedBy.ScreenName);
            }

            reward.Followers = tweet.CreatedBy.FollowersCount;

            if (reward.Withdrawals >= reward.Followers)
            {
                return string.Format(_appSettings.BotSettings.MessageReachedLimit, tweet.CreatedBy.ScreenName);
            }
            
            if (reward.LastRewardDate.Date.Equals(DateTime.UtcNow.Date))
            {
                return  GenerateMessageDailyLimitReached(tweet.CreatedBy.ScreenName);
            }
            
            _withdrawalService.ExecuteAsync(rewardType, targetAddress).GetAwaiter().GetResult();
            _statService.AddStat(DateTime.UtcNow, AmountHelper.GetAmount(_appSettings, rewardType), false);

            reward.LastRewardDate = DateTime.UtcNow;
            reward.Withdrawals++;
            _rewardCollection.Update(reward);
                
            return string.Format(_appSettings.BotSettings.MessageRewarded, tweet.CreatedBy.ScreenName, AmountHelper.GetAmount(_appSettings, rewardType));
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

        private ITwitterCredentials SetUserCredentials()
        {
            return Auth.SetUserCredentials(
                _appSettings.TwitterSettings.ConsumerKey,
                _appSettings.TwitterSettings.ConsumerSecret,
                _appSettings.TwitterSettings.AccessToken,
                _appSettings.TwitterSettings.AccessTokenSecret);
        }
    }
}