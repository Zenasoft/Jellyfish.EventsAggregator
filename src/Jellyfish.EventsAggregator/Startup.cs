using Microsoft.AspNet.Builder;
using Microsoft.Framework.DependencyInjection;

namespace Jellyfish.EventsAggregator
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseJellyfishEventsAggregator();
            app.UseStaticFiles();
        }
    }
}