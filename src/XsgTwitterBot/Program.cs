using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using XsgTwitterBot.Services.Impl;
using Microsoft.Extensions.Configuration;
using Serilog;
using XsgTwitterBot.Configuration;
using Timer = System.Timers.Timer;

namespace XsgTwitterBot
{
    public class Program
    {
        private static readonly AutoResetEvent WaitHandle = new AutoResetEvent(false);
        private static readonly AppSettings AppSettings = new AppSettings();

        private static readonly Timer RestartTimer = new Timer
        {
            Enabled = true,
            AutoReset = true,
            Interval = 1000 * 60 * 60 // 1 hour
        };

        private static IContainer _container;
        private static BotEngine _botEngine;

        private static void Main(string[] args)
        {
            try
            {
                SetupConfiguration();
                SetupLogger();
                SetupContainer();
                RunBotEngine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Log.Error(ex, "Application has crashed!");
            }
        }

        private static void SetupConfiguration()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("settings.json", true, false)
                .AddEnvironmentVariables()
                .Build();
            
            config.Bind(AppSettings);
        }

        private static void SetupLogger()
        {
            Console.WriteLine(AppSettings.LogServerUrl);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.Seq(AppSettings.LogServerUrl)
                .CreateLogger();
        }

        private static void SetupContainer()
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterInstance(AppSettings).SingleInstance();
            containerBuilder.RegisterInstance(AppSettings.NodeOptions).SingleInstance();
            containerBuilder.RegisterModule(new AppModule());
            _container = containerBuilder.Build();
        }

        public static void RunBotEngine()
        {
            // waiting for xsg node
            Task.Delay(1000 * 30).GetAwaiter().GetResult();

            _botEngine = _container.Resolve<BotEngine>();
            _botEngine.Start();

            RestartTimer.Elapsed += (sender, args) =>
            {
                Log.Logger.Information($"Restarting the {nameof(BotEngine)}.");

                _botEngine.Start();
            };

            RestartTimer.Start();

            Console.CancelKeyPress += (o, e) =>
            {
                RestartTimer.Enabled = false;
                RestartTimer.Stop();
                RestartTimer.Dispose();

                _botEngine.Dispose();
                _container.Dispose();

                WaitHandle.Set();
            };

            WaitHandle.WaitOne();
        }
    }
}