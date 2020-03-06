using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nlog.Sinks.Datadog.Logs
{
    public interface IDatadogClient
    {
        /// <summary>
        /// Send payload to Datadog logs-backend.
        /// </summary>
        /// <param name="events">Serilog events to send.</param>
        Task WriteAsync(IEnumerable<LogEventInfo> events);

        /// <summary>
        /// Cleanup existing resources.
        /// </summary>
        void Close();
    }
}
