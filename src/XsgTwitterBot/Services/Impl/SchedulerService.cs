using System;
using System.Collections.Generic;
using System.Threading;

namespace XsgTwitterBot.Services.Impl
{
    public class SchedulerService
    {
        private static SchedulerService _instance;
        private readonly List<Timer> _timers = new List<Timer>();
        private SchedulerService() { }
        public static SchedulerService Instance => _instance ?? (_instance = new SchedulerService());
        public void ScheduleTask(int hour, int min, double intervalInHour, Action task)
        {
            var now = DateTime.Now;
            var firstRun = new DateTime(now.Year, now.Month, now.Day, hour, min, 0, 0);
            if (now > firstRun)
            {
                firstRun = firstRun.AddDays(1);
            }
            var timeToGo = firstRun - now;
            if (timeToGo <= TimeSpan.Zero)
            {
                timeToGo = TimeSpan.Zero;
            }
            var timer = new Timer(x =>
            {
                task.Invoke();
            }, null, timeToGo, TimeSpan.FromHours(intervalInHour));
            _timers.Add(timer);
        }
    }
    
    public static class SchedulerServiceHelper
    {
        public static void IntervalInSeconds(int hour, int sec, double interval, Action task)
        {
            interval = interval/3600;
            SchedulerService.Instance.ScheduleTask(hour, sec, interval, task);
        }
        public static void IntervalInMinutes(int hour, int min, double interval, Action task)
        {
            interval = interval/60;
            SchedulerService.Instance.ScheduleTask(hour, min, interval, task);
        }
        public static void IntervalInHours(int hour, int min, double interval, Action task)
        {
            SchedulerService.Instance.ScheduleTask(hour, min, interval, task);
        }
        public static void IntervalInDays(int hour, int min, double interval, Action task)
        {
            interval = interval * 24;
            SchedulerService.Instance.ScheduleTask(hour, min, interval, task);
        }
    }
}