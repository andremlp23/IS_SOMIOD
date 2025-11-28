using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace SOMIOD_API.Models
{
    public class Subscription
    {
        [JsonProperty("resource-name")]
        public string ResourceName { get; set; }

        [JsonProperty("res-type")]
        public string ResType { get; set; } = "subscription";

        [JsonProperty("evt")]
        public int Evt { get; set; } // 1 = Creation, 2 = Deletion

        [JsonProperty("endpoint")]
        public string Endpoint { get; set; }

        [JsonProperty("creation-datetime")]
        public DateTimeOffset? CreationDateTime { get; set; }
    }
}
