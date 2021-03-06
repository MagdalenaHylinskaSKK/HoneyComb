﻿using HoneyComb.CQRS.Commands.Dispatchers;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyComb.CQRS.Commands
{
    public static class Extensions
    {
        public static IHoneyCombBuilder AddCommandHandlers(this IHoneyCombBuilder builder)
        {
            builder.Services.Scan(s =>s
                .FromExecutingAssembly()
                .FromCallingAssembly()
                .FromApplicationDependencies()
                .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<>)))
                .AsImplementedInterfaces()
                .WithTransientLifetime());

            return builder;
        }

        public static IHoneyCombBuilder AddCommandDispatcher(this IHoneyCombBuilder builder)
        {
            builder.Services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
            return builder;
        }
    }
}
