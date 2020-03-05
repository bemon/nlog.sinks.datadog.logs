using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nlog.Sinks.Datadog.Logs
{
    [Target("DatadogSink")]
    public class DatadogSink : TargetWithLayout
    {
        private IDatadogClient _client;

        /// <summary>
        /// The time to wait before emitting a new event batch.
        /// </summary>
        private static readonly TimeSpan Period = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Api key to datadog endpoint
        /// </summary>
        [RequiredParameter]
        public string ApiKey
        {
            get; set;
        }

        public string Source
        {
            get; set;
        }

        public string Host
        {
            get; set;
        }

        public string Service
        {
            get; set;
        }

        public string Tags
        {
            get; set;
        }

        /// <summary>
        /// The Datadog logs-backend URL.
        /// </summary>
        public const string DDUrl = "https://http-intake.logs.datadoghq.com";

        /// <summary>
        /// The Datadog logs-backend TCP SSL port.
        /// </summary>
        public const int DDPort = 10516;

        /// <summary>
        /// The Datadog logs-backend TCP unsecure port.
        /// </summary>
        public const int DDPortNoSSL = 10514;

        /// <summary>
        /// URL of the server to send log events to.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Port of the server to send log events to.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Use SSL or plain text.
        /// </summary>
        public bool UseSSL { get; set; }

        /// <summary>
        /// Use TCP or HTTP.
        /// </summary>
        public bool UseTCP { get; set; }

        List<LogEventInfo> _events;

        Timer _batchTimer;

        public DatadogSink()
        {
            _batchTimer = new Timer(SendBatch, null, Period.Milliseconds, Period.Milliseconds);
            _events = new List<LogEventInfo>();
        }

        private void SendBatch(Object state)
        {
            EnsureClient();
            lock (_events)
            {
                try
                {
                    _client.WriteAsync(new List<LogEventInfo>(_events));
                    _events.Clear();
                }
                catch
                {

                }
            }
        }

        private void EnsureClient()
        {
            if (_client == null)
            {
                var configuration = new DatadogConfiguration(Url, Port, UseSSL, UseTCP);
                if (UseTCP)
                {
                    _client = new DatadogTcpClient(configuration, new LogFormatter(Source, Service, Host, Tags.Split(',')), ApiKey);
                }
                else
                {
                    _client = new DatadogHttpClient(configuration, new LogFormatter(Source, Service, Host, Tags.Split(',')), ApiKey);
                }
            }
        }

        /// <summary>
        /// Emit a batch of log events to Datadog logs-backend.
        /// </summary>
        /// <param name="events">The events to emit.</param>
        protected override void Write(LogEventInfo logEvent)
        {
            lock (_events)
            {
                _events.Add(logEvent);
            }
        }

        /// <summary>
        /// Free resources held by the sink.
        /// </summary>
        /// <param name="disposing">If true, called because the object is being disposed; if false,
        /// the object is being disposed from the finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            _batchTimer.Dispose();
            _client.Close();

            _client = null;
            _batchTimer = null;

            base.Dispose(disposing);
        }
    }
}
