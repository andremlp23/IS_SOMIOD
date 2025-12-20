using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SOMIOD_API.DTOs
{
    public class RenameResourceDto
    {
        [JsonProperty("resource-name")]
        public string ResourceName { get; set; }
    }

}