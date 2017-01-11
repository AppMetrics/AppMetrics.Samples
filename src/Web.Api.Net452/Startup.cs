﻿using System;
using System.Reflection;
using System.Web.Hosting;
using System.Web.Http;
using App.Metrics;
using App.Metrics.Extensions.Owin.WebApi;
using App.Metrics.Extensions.Reporting.InfluxDB;
using App.Metrics.Extensions.Reporting.InfluxDB.Client;
using App.Metrics.Reporting.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Owin;
using Web.Api.Net452;
using Web.Api.Net452.Infrastructure;

[assembly: OwinStartup(typeof(Startup))]


namespace Web.Api.Net452
{
    public class Startup
    {
        public void Configuration(IAppBuilder appBuilder)
        {
            var httpConfiguration = new HttpConfiguration();
            httpConfiguration.MessageHandlers.Add(new MetricsWebApiMessageHandler());
            httpConfiguration.RegisterWebApi();

            var services = new ServiceCollection();
            ConfigureServices(services);
            //DEVNOTE: If already using Autofac for example for DI, you would just build the 
            // servicecollection, resolve IMetrics and register that with your container instead.
            var provider = services.SetDependencyResolver(httpConfiguration);

            appBuilder.UseMetrics(provider);

            HostingEnvironment.QueueBackgroundWorkItem(cancellationToken =>
            {
                var reportFactory = provider.GetRequiredService<IReportFactory>();
                var metrics = provider.GetRequiredService<IMetrics>();
                var reporter = reportFactory.CreateReporter();
                reporter.RunReports(metrics, cancellationToken);
            });

            appBuilder.UseWebApi(httpConfiguration);            
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging();

            services.AddControllersAsServices();

            services
                .AddMetrics(options =>
                {
                    options.DefaultContextLabel = "Api.Sample.Net452";
                    options.ReportingEnabled = true;
                }, Assembly.GetExecutingAssembly().GetName())
                 .AddReporting(factory =>
                 {
                     factory.AddInfluxDb(new InfluxDBReporterSettings
                     {
                         HttpPolicy = new HttpPolicy
                         {
                             FailuresBeforeBackoff = 3,
                             BackoffPeriod = TimeSpan.FromSeconds(30),
                             Timeout = TimeSpan.FromSeconds(3)
                         },
                         InfluxDbSettings = new InfluxDBSettings("appmetricswebapisample452", new Uri("http://127.0.0.1:8086")),
                         ReportInterval = TimeSpan.FromSeconds(5)
                     });
                 })
                .AddHealthChecks(factory =>
                {
                    factory.RegisterProcessPrivateMemorySizeHealthCheck("Private Memory Size", 200);
                    factory.RegisterProcessVirtualMemorySizeHealthCheck("Virtual Memory Size", 200);
                    factory.RegisterProcessPhysicalMemoryHealthCheck("Working Set", 200);
                })
                .AddJsonSerialization()
                .AddMetricsMiddleware(options =>
                {

                });
        }
    }
}