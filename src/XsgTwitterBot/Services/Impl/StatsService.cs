using System;
using LiteDB;
using Serilog;
using Tweetinvi;
using XsgTwitterBot.Configuration;
using XsgTwitterBot.Models;

namespace XsgTwitterBot.Services.Impl
{
    public class StatsService : IStatService
    {
        private readonly AppSettings _appSettings;
        private readonly LiteCollection<Stat> _stats;
        private readonly ILogger _logger;
        
        public StatsService(AppSettings appSettings, LiteCollection<Stat> dailyStats) 
        {
            _appSettings = appSettings;
            _stats = dailyStats;
            _logger = Log.ForContext<StatsService>();
        }
        
        public void Publish()
        {
            var previousDay = DateTime.UtcNow.AddSeconds(-1);

            var dailyStatId = GetDailyStatId(previousDay);
            
            var dailyStat = _stats.FindOne(x => x.Id == dailyStatId);
            if (dailyStat != null)
            {
                var dailyStatTweetMessage = string.Format(_appSettings.StatSettings.DailyMessage,
                    $"{previousDay.Date.Day}/{previousDay.Date.Month}/{previousDay.Date.Year}", dailyStat.NewUsers,
                    dailyStat.WithdrawalAmount, dailyStat.TotalWithdrawals);
            
                Tweet.PublishTweet(dailyStatTweetMessage);
                
                _logger.Information("Published stats for {@DailyStatTweetMessage}", dailyStatTweetMessage);
            }
           
            if (DateTime.UtcNow.Date.Month > previousDay.Month)
            {
                var monthlyStatId = GetMonthlyStatId(previousDay);
                var monthlyStat = _stats.FindOne(x => x.Id == monthlyStatId);
                if (monthlyStat != null)
                {
                    var monthlyStatMessage = string.Format(_appSettings.StatSettings.MonthlyMessage,
                        $"{previousDay.Date.Month}/{previousDay.Date.Year}", monthlyStat.NewUsers, monthlyStat.WithdrawalAmount,
                        monthlyStat.TotalWithdrawals);
                
                    Tweet.PublishTweet(monthlyStatMessage);

                    _logger.Information("Published stats for {@MonthlyStatMessage}", monthlyStatMessage);    
                }
            }

            if (DateTime.UtcNow.Date.Year > previousDay.Year)
            {
                var yearlyStatId = GetYearlyStatId(previousDay);
                var yearlyStat = _stats.FindOne(x => x.Id == yearlyStatId);
                if (yearlyStat != null)
                {
                    var yearlyStatMessage = string.Format(_appSettings.StatSettings.MonthlyMessage, previousDay.Date.Year,
                        yearlyStat.NewUsers, yearlyStat.WithdrawalAmount, yearlyStat.TotalWithdrawals);
                
                    Tweet.PublishTweet(yearlyStatMessage);

                    _logger.Information("Published stats for {@YearlyStatMessage}", yearlyStatMessage);    
                }
            }
        }

        public void AddStat(DateTime date, decimal amount, bool isNewUser)
        {
            AddStatInternal(GetDailyStatId(date), amount, isNewUser);
            AddStatInternal(GetMonthlyStatId(date), amount, isNewUser);
            AddStatInternal(GetYearlyStatId(date), amount, isNewUser);
        }
        
        private long GetDailyStatId(DateTime date) => long.Parse($"{date.Year}{date.Month}{date.Day}");
        private long GetMonthlyStatId(DateTime date) => long.Parse($"{date.Year}{date.Month}");
        private long GetYearlyStatId(DateTime date) => long.Parse($"{date.Year}");

        private void AddStatInternal(long id, decimal amount, bool isNewUser)
        {
            var stat =  _stats.FindOne(x => x.Id == id);
            if (stat != null)
            {
                stat.WithdrawalAmount += amount;
                stat.TotalWithdrawals++;

                if (isNewUser)
                {
                    stat.NewUsers++;
                }
                
                _stats.Update(stat);
                
                _logger.Information("Updated stat {@stat}", stat);
            }
            else
            {
                var newStat = new Stat
                {
                    Id = id,
                    WithdrawalAmount = amount,
                    TotalWithdrawals = 1,
                };
                
                if (isNewUser)
                {
                    newStat.NewUsers = 1;
                }
                
                _stats.Insert(newStat);
                _logger.Information("Inserted new stat {@stat}", newStat);
            }
        }
    }
}