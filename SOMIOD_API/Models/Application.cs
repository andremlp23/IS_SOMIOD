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
        public int Id { get; set; } // Interno da BD

        [JsonProperty("resource-name")]
        public string ResourceName { get; set; } // Mapeia para coluna 'Name' na BD

        [JsonProperty("res-type")]
        public string ResType { get; set; } // Deve ser 'application'

        [JsonProperty("creation-datetime")]
        public DateTime CreationDateTime { get; set; } // Mapeia para 'CreationDate'
    }
}