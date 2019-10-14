using System;
using System.Threading;
using XsgTwitterBot.Models;

namespace XsgTwitterBot.Services
{
    public interface IStatService
    {
        void AddStat(DateTime date, decimal amount, bool isNewUser);
        void Publish();
        Stat GetPreviousDayStat();
    }
}