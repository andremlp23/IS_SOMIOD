<<<<<<< HEAD
﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Web;
=======
>>>>>>> 9f75797899fa44ef57d1bfb86da5b302209a21d0

namespace SOMIOD_API.Models
{
    public class Application
    {
<<<<<<< HEAD
        public int Id { get; set; } // Interno da BD

        [JsonProperty("resource-name")]
        public string ResourceName { get; set; } // Mapeia para coluna 'Name' na BD

        [JsonProperty("res-type")]
        public string ResType { get; set; } // Deve ser 'application'

        [JsonProperty("creation-datetime")]
        public DateTime CreationDateTime { get; set; } // Mapeia para 'CreationDate'
    }
}
=======
>>>>>>> 9f75797899fa44ef57d1bfb86da5b302209a21d0
