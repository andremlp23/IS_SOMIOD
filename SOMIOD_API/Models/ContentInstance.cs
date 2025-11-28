using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace SOMIOD_API.Models
{
    public class ContentInstance
    {
        [JsonProperty("resource-name")]
        public string ResourceName { get; set; }

        [JsonProperty("res-type")]
        public string ResType { get; set; } = "content-instance";

        [JsonProperty("content-type")]
        public string ContentType { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("creation-datetime")]
        public DateTimeOffset? CreationDateTime { get; set; }
    }
}
