using Newtonsoft.Json;
using NLog;
using NLog.Layouts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nlog.Sinks.Datadog.Logs
{
    public class LogFormatter
    {
        private readonly string _source;
        private readonly string _service;
        private readonly string _host;
        private readonly string _tags;

        /// <summary>
        /// Default source value for the serilog integration.
        /// </summary>
        private const string CSHARP = "csharp";

        /// <summary>
        /// Settings to drop null values.
        /// </summary>
        private static readonly JsonSerializerSettings settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

        JsonLayout jsonLayout = new JsonLayout
        {
            Attributes =
            {
                new JsonAttribute("date", "${longdate}"),
                new JsonAttribute("level", "${level:upperCase=true}"),
                new JsonAttribute("message", "${message}"),
                new JsonAttribute("exception", "${exception:format=ToString}")
            }
        };

        public LogFormatter(string source, string service, string host, string[] tags)
        {
            _source = source ?? CSHARP;
            _service = service;
            _host = host;
            _tags = tags != null ? string.Join(",", tags) : null;
        }

        /// <summary>
        /// formatMessage enrich the log event with DataDog metadata such as source, service, host and tags.
        /// </summary>
        public string formatMessage(LogEventInfo logEvent)
        {
            string payload = jsonLayout.Render(logEvent);

            // Convert the JSON to a dictionnary and add the DataDog properties
            var logEventAsDict = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(payload.ToString());
            if (_source != null) { logEventAsDict.Add("ddsource", _source); }
            if (_service != null) { logEventAsDict.Add("service", _service); }
            if (_host != null) { logEventAsDict.Add("host", _host); }
            if (_tags != null) { logEventAsDict.Add("ddtags", _tags); }

            // Rename serilog attributes to Datadog reserved attributes to have them properly
            // displayed on the Log Explorer
            RenameKey(logEventAsDict, "RenderedMessage", "message");
            RenameKey(logEventAsDict, "Level", "level");

            // Convert back the dict to a JSON string
            return JsonConvert.SerializeObject(logEventAsDict, Newtonsoft.Json.Formatting.None, settings);
        }

        /// <summary>
        /// Renames a key in a dictionary.
        /// </summary>
        private void RenameKey<TKey, TValue>(IDictionary<TKey, TValue> dict,
                                           TKey oldKey, TKey newKey)
        {
            TValue value;
            if (dict.TryGetValue(oldKey, out value))
            {
                dict.Remove(oldKey);
                dict.Add(newKey, value);
            }
        }
    }
}
