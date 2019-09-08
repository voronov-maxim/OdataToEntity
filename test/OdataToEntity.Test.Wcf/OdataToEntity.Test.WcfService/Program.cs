using CoreWCF;
using CoreWCF.Configuration;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace OdataToEntity.Test.WcfService
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<OrderService>();
                builder.AddServiceEndpoint<OrderService, IOdataWcf>(new NetTcpBinding(), "/OdataWcfService");
            });
        }
    }

    class Program
    {
        static void Main(String[] args)
        {
            using (IWebHost host = WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseNetTcp(5000)
                .Build())
            {
                host.Start();
                do
                    Console.WriteLine("Close server press escape to exit");
                while (Console.ReadKey().Key != ConsoleKey.Escape);
            }
        }
    }
}