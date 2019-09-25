using System.IO;
using Autofac;
using LiteDB;
using XsgTwitterBot.Models;
using XsgTwitterBot.Node.Impl;
using XsgTwitterBot.Services.Impl;

namespace XsgTwitterBot
{
    public class AppModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<MessageParser>().AsImplementedInterfaces().InstancePerDependency();
            builder.RegisterType<WithdrawalService>().AsImplementedInterfaces().InstancePerDependency();
            builder.RegisterType<StatsService>().AsImplementedInterfaces().InstancePerDependency();
            builder.RegisterType<NodeApi>().AsImplementedInterfaces().InstancePerDependency();

            builder.RegisterType<ExplorerApi>().AsImplementedInterfaces().InstancePerDependency();
            builder.RegisterType<SyncCheckService>().AsImplementedInterfaces().InstancePerDependency();
            
            var dbPath = Path.Combine("db", "data.db");
            builder.Register(container => new LiteDatabase(dbPath)).SingleInstance();
            
            
            builder.Register(container => container.Resolve<LiteDatabase>().GetCollection<UserTweetMap>("userTweetMap")).SingleInstance();
            builder.Register(container => container.Resolve<LiteDatabase>().GetCollection<FriendTagMap>("friendMap")).SingleInstance();
            builder.Register(container => container.Resolve<LiteDatabase>().GetCollection<Reward>("rewards")).SingleInstance();
            builder.Register(container => container.Resolve<LiteDatabase>().GetCollection<Cursor>("cursor")).SingleInstance();
            builder.Register(container => container.Resolve<LiteDatabase>().GetCollection<Stat>("stats")).SingleInstance();
            builder.RegisterType<BotEngine>().InstancePerDependency();
        }
    }
}