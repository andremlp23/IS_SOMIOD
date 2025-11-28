using System;
using System.Text.Json.Serialization;

namespace SOMIOD_API.Models
{
    public class Application
    {
        [JsonPropertyName("resource-name")]
        public string ResourceName { get; set; }

        [JsonPropertyName("res-type")]
        public string ResType { get; set; } = "application";

        [JsonPropertyName("creation-datetime")]
        public DateTimeOffset? CreationDateTime { get; set; }
    }
}
