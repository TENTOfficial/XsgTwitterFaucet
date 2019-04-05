using System;
using System.Threading;
using System.Threading.Tasks;
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

        public void RunPublisher(CancellationToken cancellationToken)
        {
            var task = new Task(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var nextDay = DateTime.UtcNow.Date.AddDays(1);
                    var endOfDay = nextDay.AddSeconds(-1);
                
                    var diff = DateTime.UtcNow - endOfDay;
                    
                    _logger.Information("Waiting {@Diff} for stat publishing", diff);
                    
                    await Task.Delay((int)diff.TotalMilliseconds/4, cancellationToken);
                    await Task.Delay((int)diff.TotalMilliseconds/4, cancellationToken);
                    await Task.Delay((int)diff.TotalMilliseconds/4, cancellationToken);
                    await Task.Delay((int)diff.TotalMilliseconds/4, cancellationToken);
                    
                
                    var dailyStatId = long.Parse($"{endOfDay.Year}{endOfDay.Month}{endOfDay.Day}");
                    var monthlyStatId = long.Parse($"{endOfDay.Year}{endOfDay.Month}{endOfDay.Day}");
                    var yearlyStatId = long.Parse($"{endOfDay.Year}{endOfDay.Month}{endOfDay.Day}");
                
                    var dailyStat =  _stats.FindOne(x => x.Id == dailyStatId);
                    var dailyStatTweetMessage = string.Format(_appSettings.StatSettings.DailyMessage, $"{endOfDay.Date.Day}/{endOfDay.Date.Month}/{endOfDay.Date.Year}", dailyStat.NewUsers, dailyStat.WithdrawalAmount, dailyStat.TotalWithdrawals);
                    Tweet.PublishTweet(dailyStatTweetMessage);

                    _logger.Information("Published stats for {@DailyStatTweetMessage}", dailyStatTweetMessage);

                    if (nextDay.Month > endOfDay.Month)
                    {
                        var monthlyStat =  _stats.FindOne(x => x.Id == monthlyStatId);
                        var monthlyStatMessage = string.Format(_appSettings.StatSettings.MonthlyMessage, $"{endOfDay.Date.Month}/{endOfDay.Date.Year}", monthlyStat.NewUsers, monthlyStat.WithdrawalAmount, monthlyStat.TotalWithdrawals); 
                        Tweet.PublishTweet(monthlyStatMessage);
                        
                        _logger.Information("Published stats for {@MonthlyStatMessage}", monthlyStatMessage);
                    }
                
                    if (nextDay.Year > endOfDay.Year)
                    {
                        var yearlyStat =  _stats.FindOne(x => x.Id == yearlyStatId);
                        var yearlyStatMessage = string.Format(_appSettings.StatSettings.MonthlyMessage, endOfDay.Date.Year, yearlyStat.NewUsers, yearlyStat.WithdrawalAmount, yearlyStat.TotalWithdrawals); 
                        Tweet.PublishTweet(yearlyStatMessage);
                        
                        _logger.Information("Published stats for {@YearlyStatMessage}", yearlyStatMessage);
                    }
                }
            });
            
            task.Start();
        }
        
        public void AddStat(DateTime date, decimal amount, bool isNewUser)
        {
            AddStatInternal(long.Parse($"{date.Year}{date.Month}{date.Day}"), amount, isNewUser);
            AddStatInternal(long.Parse($"{date.Year}{date.Month}"), amount, isNewUser);
            AddStatInternal(long.Parse($"{date.Year}"), amount, isNewUser);
        }

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