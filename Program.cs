using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PIX.Models;

namespace PIX
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();

        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>               
            WebHost.CreateDefaultBuilder(args)
                        .ConfigureLogging(log =>
                        {
                            log.AddProvider(new CustomLoggerProvider(new CustomLoggerProviderConfiguration
                            {
                                LogLevel = LogLevel.Information
                            }));
                        })
                .UseIISIntegration()
                .UseStartup<Startup>();
    }
}
