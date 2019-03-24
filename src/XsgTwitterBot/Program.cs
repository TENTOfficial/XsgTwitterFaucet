using System;
using System.Threading;
using Autofac;
using XsgTwitterBot.Services.Impl;
using Microsoft.Extensions.Configuration;
using Serilog;
using Tweetinvi.Core.Extensions;
using Tweetinvi.Streaming;
using XsgTwitterBot.Configuration;

namespace XsgTwitterBot
{
    public class Program
    {
        private static readonly AutoResetEvent WaitHandle = new AutoResetEvent(false);
        private static readonly AppSettings AppSettings = new AppSettings();
        private static IContainer _container;
        private static IFilteredStream _stream;

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
            _stream = _container.Resolve<BotEngine>().Start();

            Console.CancelKeyPress += (o, e) =>
            {
                _stream.StopStream();
                _container.Dispose();
                WaitHandle.Set();
            };

            WaitHandle.WaitOne();
        }
    }
}


/*
 * - create ssh deploy script (pubkey needed)
 * - spawn testnet /reg test node
 * http://chainquery.com/bitcoin-api/validateaddress
 */
