using Jellyfish.EventsAggregator;
using Microsoft.AspNet.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Framework.DependencyInjection
{
    public static class JellyfishExtensions
    {
        public static IApplicationBuilder UseJellyfishEventsAggregator(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<EventsAggregatorMiddleware>();
            return builder;
        }
    }
}
