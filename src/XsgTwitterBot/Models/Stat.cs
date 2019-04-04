namespace XsgTwitterBot.Models
{
    public class Stat
    {
        public long Id { get; set; }
        public decimal WithdrawalAmount { get; set; }
        public int TotalWithdrawals { get; set; }
        public int NewUsers { get; set; }
    }
}