using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Telemetry
{
    public static class ServiceCollectionEx
    {
        public static IServiceCollection AddFluentdLogging(this IServiceCollection services, IConfiguration configuration, string applicationName)
        {

            return services;
        }

        public static IServiceCollection AddOpenCensus(this IServiceCollection services, IConfiguration configuration, string applicationName)
        {

            return services;
        }

        public static IServiceCollection AddPromethesusMetrics(this IServiceCollection services, IConfiguration configuration, string applicationName)
        {

            return services;
        }
    }
}
