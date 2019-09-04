using System;

namespace XsgTwitterBot.Models
{
    public class Cursor
    {
        public string Id { get; set; }
        
        public long TweetId { get; set; } 
        public DateTime UpdateAt { get; set; }
    }
}