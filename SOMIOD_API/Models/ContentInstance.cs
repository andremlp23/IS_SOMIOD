using System;
using System.Text.Json.Serialization;

namespace SOMIOD_API.Models
{
    public class ContentInstance
    {
        [JsonPropertyName("resource-name")]
        public string ResourceName { get; set; }

        [JsonPropertyName("res-type")]
        public string ResType { get; set; } = "content-instance";

        [JsonPropertyName("content-type")]
        public string ContentType { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("creation-datetime")]
        public DateTimeOffset? CreationDateTime { get; set; }
    }
}
