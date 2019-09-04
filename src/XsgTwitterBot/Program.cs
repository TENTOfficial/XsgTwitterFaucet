using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using FluentScheduler;
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

        private static IContainer _container;
        private static BotEngine _botEngine;

        private static void Main(string[] args)
        {
            try
            {
                SetupConfiguration();
                SetupLogger();
                SetupContainer();
                WaitForNodeConnectivity();
                WaitUntilNodeSynced();
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

        private static void WaitUntilNodeSynced()
        {
            _container.Resolve<ISyncCheckService>().WaitUntilSyncedAsync().GetAwaiter().GetResult();
        }
        
        private static void RunBotEngine()
        {
            _botEngine = _container.Resolve<BotEngine>();
            _botEngine.Start();

            JobManager.Initialize();
            JobManager.AddJob(() => _container.Resolve<IStatService>().Publish(), (s) => s.ToRunEvery(1).Days().At(00, 00));
            
            Console.CancelKeyPress += (o, e) =>
            {
                _botEngine.Stop();
                _container.Dispose();

                WaitHandle.Set();
            };

            WaitHandle.WaitOne();
        }
    }
}