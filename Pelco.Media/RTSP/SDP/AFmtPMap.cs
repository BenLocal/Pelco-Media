using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pelco.Media.RTSP.SDP
{
    public class AFmtPMap
    {
        internal AFmtPMap()
        {
            Parameters = new Dictionary<string, string>();
        }

        /// <summary>
        /// same as PayloadType
        /// </summary>
        public ushort Format { get; private set; }

        /// <summary>
        /// name -> value
        /// </summary>
        public Dictionary<string, string> Parameters { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="str">
        /// eg: 96 profile-level-id=420029; packetization-mode=1; sprop-parameter-sets=Z00AKpY1QPAET8s3AQEBQAABwgAAV+QB,aO4xsg==
        /// </param>
        /// <returns></returns>
        public static AFmtPMap Parse(string str)
        {
            var items = str.Split(new char[] { ' ' }, 2);
            if (items.Length != 2)
            {
                throw new SdpParseException($"Unable to parse malformed afmtp attriute '{str}'");
            }
            var builder = CreateBuilder()
                .Format(ushort.Parse(items[0]));
            var subItems = items[1].Split(';');
            if (subItems.Length > 0)
            {
                foreach (var item in subItems)
                {
                    if (string.IsNullOrWhiteSpace(item))
                    {
                        continue;
                    }

                    var kv = item.Split('=');
                    if (kv.Length != 2)
                    {
                        continue;
                    }

                    builder.AddParameters(kv[0].Trim(), kv[1].Trim());
                }
            }

            return builder.Build();
        }

        public static Builder CreateBuilder()
        {
            return new Builder();
        }

        public sealed class Builder
        {
            private ushort _format;
            private Dictionary<string, string> _parameters;

            public Builder()
            {
                _parameters = new Dictionary<string, string>();
            }

            public Builder Clear()
            {
                _format = 0;
                _parameters.Clear();

                return this;
            }

            public Builder Format(ushort format)
            {
                _format = format;

                return this;
            }

            public Builder AddParameters(string key, string value)
            {
                if (_parameters == null)
                {
                    _parameters = new Dictionary<string, string>();
                }

                _parameters.Add(key, value);
                return this;
            }

            public AFmtPMap Build()
            {
                var map = new AFmtPMap()
                {
                    Format = _format,
                };
                foreach (var item in _parameters)
                {
                    map.Parameters.Add(item.Key, item.Value);
                }
                return map;
            }
        }
    }
}
