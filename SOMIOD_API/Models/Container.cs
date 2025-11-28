using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace SOMIOD_API.Models
{
    public class Container
    {
        [JsonProperty("resource-name")]
        public string ResourceName { get; set; }

        [JsonProperty("res-type")]
        public string ResType { get; set; } = "container";

        [JsonProperty("creation-datetime")]
        public DateTimeOffset? CreationDateTime { get; set; }
    }
}
