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
}