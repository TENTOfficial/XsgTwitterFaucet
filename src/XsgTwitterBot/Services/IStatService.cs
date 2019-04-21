using System;
using System.Threading;

namespace XsgTwitterBot.Services
{
    public interface IStatService
    {
        void AddStat(DateTime date, decimal amount, bool isNewUser);
        void Publish();
    }
}