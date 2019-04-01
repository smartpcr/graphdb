using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Common.Telemetry
{
    public static class LoggingBuilderEx
    {
        public static ILoggingBuilder AddFluentd(this ILoggingBuilder builder, IConfiguration configuration)
        {
            return builder;
        }
    }
}
