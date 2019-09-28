using System;

namespace XsgTwitterBot.Configuration
{
    public class BotSettings
    {
        public string[] TrackKeywords { get; set; }
        public string MessageRewarded { get; set; }
        public string MessageReachedLimit { get; set; }
        public string MessageFaucetDrained { get; set; }
        public string MessageDailyLimitReached { get; set; }
        public decimal AmountForTweetWithTag { get; set; }
        public decimal AmountForTweetWithFriendMention { get; set; }
        public string MessageTweetInvalid { get; set; }
        public int MinTweetLenght { get; set; }
        public decimal UserRatio { get; set; }
        public int FollowersCountThreshold { get; set; }
        public string MessageFriendMentioned { get; set; }
        public long LastMessageId { get; set; }
    }
}