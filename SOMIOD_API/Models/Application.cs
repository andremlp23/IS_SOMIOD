using Newtonsoft.Json;
using System;

namespace SOMIOD_API.Models
{
    public class Application
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? Id { get; set; }

        [JsonProperty("resource-name")]
        public string ResourceName { get; set; }

        [JsonProperty("res-type")]
        public string ResType { get; set; }

        [JsonProperty("creation-datetime")]
        public DateTime CreationDateTime { get; set; }
    }
}
