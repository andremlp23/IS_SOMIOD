using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Web;

namespace SOMIOD_API.Models
{
    public class Application
    {
        public int Id { get; set; }

        [JsonProperty("resource-name")]
        public string ResourceName { get; set; }

        [JsonProperty("res-type")]
        public string ResType { get; set; }

        [JsonProperty("creation-datetime")]
        public DateTime CreationDateTime { get; set; }
    }
}
