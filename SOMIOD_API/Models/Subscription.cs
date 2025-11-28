using System;
using System.Text.Json.Serialization;

namespace SOMIOD_API.Models
{
    public class Subscription
    {
        [JsonPropertyName("resource-name")]
        public string ResourceName { get; set; }

        [JsonPropertyName("res-type")]
        public string ResType { get; set; } = "subscription";

        [JsonPropertyName("evt")]
        public int Evt { get; set; } // 1 = Creation, 2 = Deletion

        [JsonPropertyName("endpoint")]
        public string Endpoint { get; set; }

        [JsonPropertyName("creation-datetime")]
        public DateTimeOffset? CreationDateTime { get; set; }
    }
}
