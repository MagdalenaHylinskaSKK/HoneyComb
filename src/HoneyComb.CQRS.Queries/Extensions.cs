﻿using HoneyComb.CQRS.Queries.Dispatchers;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyComb.CQRS.Queries
{
    public static class Extensions
    {
        public static IHoneyCombBuilder AddQueryHandlers(this IHoneyCombBuilder builder)
        {
            builder.Services.Scan(s => s
               .FromExecutingAssembly()
               .FromCallingAssembly()
               .FromApplicationDependencies()
               .AddClasses(c => c.AssignableTo(typeof(IQueryHandler<,>)))
               .AsImplementedInterfaces()
               .WithTransientLifetime());

            return builder;
        }

        public static IHoneyCombBuilder AddQueryDispatcher(this IHoneyCombBuilder builder)
        {
            builder.Services.AddSingleton<IQueryDispatcher, QueryDispatcher>();
            return builder;
        }
    }
}
