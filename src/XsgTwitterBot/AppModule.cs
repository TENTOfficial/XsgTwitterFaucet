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
            builder.RegisterType<NodeApi>().AsImplementedInterfaces().InstancePerDependency();

            builder.Register(container => new LiteDatabase(@"rewards.db")).SingleInstance();
            builder.Register(container => container.Resolve<LiteDatabase>().GetCollection<Reward>("rewards")).SingleInstance();
            builder.RegisterType<BotEngine>().SingleInstance();
        }
    }
}