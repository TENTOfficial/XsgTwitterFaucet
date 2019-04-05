using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using XsgTwitterBot.Services.Impl;
using Microsoft.Extensions.Configuration;
using Serilog;
using XsgTwitterBot.Configuration;
using XsgTwitterBot.Node;
using XsgTwitterBot.Services;
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
        private static IStatService _statService;

        private static void Main(string[] args)
        {
            try
            {
                SetupConfiguration();
                SetupLogger();
                SetupContainer();
                WaitForNodeConnectivity();
                RunBotEngine();
            }
            catch (Exception ex)
            {
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

        private static void WaitForNodeConnectivity()
        {
            var nodeApi = _container.Resolve<INodeApi>();
            var isNodeOperational = false;

            while (!isNodeOperational)
            {
                try
                {
                    nodeApi.GetInfoAsync().GetAwaiter().GetResult();
                    isNodeOperational = true;
                }
                catch
                {
                    Log.Logger.Information("Waiting for node connectivity...");
                    Task.Delay(1000 * 10).GetAwaiter().GetResult();
                }
            }
        }

        private static void RunBotEngine()
        {
            _botEngine = _container.Resolve<BotEngine>();
            _botEngine.Start();

            RestartTimer.Elapsed += (sender, args) =>
            {
                _botEngine.Restart();
            };

            RestartTimer.Start();

            var cancellationTokenSource = new CancellationTokenSource();
            _statService = _container.Resolve<IStatService>();
            // _statService.RunPublisher(cancellationTokenSource.Token);
            
            Console.CancelKeyPress += (o, e) =>
            {
                RestartTimer.Enabled = false;
                RestartTimer.Stop();
                RestartTimer.Dispose();

                _botEngine.Dispose();
                _container.Dispose();
                
                cancellationTokenSource.Cancel();

                WaitHandle.Set();
            };

            WaitHandle.WaitOne();
        }
    }
}