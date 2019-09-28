using System;

namespace XsgTwitterBot.Models
{
    public class Reward
    {
        public long Id { get; set; } 
        public int Followers { get; set; }
        public int Withdrawals { get; set; }
        public DateTime LastRewardDate { get; set; }
    }

    public class FriendTagMap
    {
        public string Id { get; set; }
    }
    
    public class UserTweetMap
    {
        public string Id { get; set; }
    }
    
    public class MessageCursor
    {
        public string Id { get; set; }
        
        public long Value { get; set; }
    }
    
    
}