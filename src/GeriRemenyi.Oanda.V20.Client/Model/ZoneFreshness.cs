using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GeriRemenyi.Oanda.V20.Client.Model
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ZoneFreshness
    {
        Untested,
        Tested,
        Broken
    }
}
