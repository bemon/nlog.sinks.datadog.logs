﻿using NLog;
using NLog.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nlog.Sinks.Datadog.Logs
{
    public class DatadogHttpClient : IDatadogClient
    {
        private const string _content = "application/json";
        private const int _maxSize = 2 * 1024 * 1024 - 51;  // Need to reserve space for at most 49 "," and "[" + "]"
        private const int _maxMessageSize = 256 * 1024;

        private readonly DatadogConfiguration _config;
        private readonly string _url;
        private readonly LogFormatter _formatter;
        private readonly HttpClient _client;

        /// <summary>
        /// Max number of retries when sending failed.
        /// </summary>
        private const int MaxRetries = 10;

        /// <summary>
        /// Max backoff used when sending failed.
        /// </summary>
        private const int MaxBackoff = 30;

        /// <summary>
        /// Shared UTF8 encoder.
        /// </summary>
        private static readonly UTF8Encoding UTF8 = new UTF8Encoding();

        public DatadogHttpClient(DatadogConfiguration config, LogFormatter formatter, string apiKey)
        {
            _config = config;
            _client = new HttpClient();
            _url = $"{config.Url}/v1/input/{apiKey}";
            _formatter = formatter;
        }

        public async Task WriteAsync(IEnumerable<LogEventInfo> events)
        {
            var chunks = SerializeEvents(events);
            var tasks = chunks.Select(Post);

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private List<String> SerializeEvents(IEnumerable<LogEventInfo> events)
        {
            List<string> chunks = new List<string>();

            int currentSize = 0;

            var chunkBuffer = new List<string>(events.Count());
            foreach (var logEvent in events)
            {
                var formattedLog = _formatter.formatMessage(logEvent);
                var logSize = Encoding.UTF8.GetByteCount(formattedLog);
                if (logSize > _maxMessageSize)
                {
                    continue;  // The log is dropped because the backend would not accept it
                }
                if (currentSize + logSize > _maxSize)
                {
                    // Flush the chunkBuffer to the chunks and reset the chunkBuffer
                    chunks.Add(GenerateChunk(chunkBuffer, ",", "[", "]"));
                    chunkBuffer.Clear();
                    currentSize = 0;
                }
                chunkBuffer.Add(formattedLog);
                currentSize += logSize;
            }
            chunks.Add(GenerateChunk(chunkBuffer, ",", "[", "]"));

            return chunks;

        }

        private static string GenerateChunk(IEnumerable<string> collection, string delimiter, string prefix, string suffix)
        {
            return prefix + String.Join(delimiter, collection) + suffix;
        }

        private async Task Post(string payload)
        {
            var content = new StringContent(payload, Encoding.UTF8, _content);
            for (int retry = 0; retry < MaxRetries; retry++)
            {
                int backoff = (int)Math.Min(Math.Pow(2, retry), MaxBackoff);
                if (retry > 0)
                {
                    await Task.Delay(backoff * 1000);
                }

                try
                {
                    var result = await _client.PostAsync(_url, content);
                    if (result == null) { continue; }
                    if ((int)result.StatusCode >= 500) { continue; }
                    if ((int)result.StatusCode >= 400) { break; }
                    if (result.IsSuccessStatusCode) { return; }
                }
                catch (Exception)
                {
                    continue;
                }
            }
            InternalLogger.Error("Could not send payload to Datadog: {0}", payload);
        }

        void IDatadogClient.Close() { }
    }
}
