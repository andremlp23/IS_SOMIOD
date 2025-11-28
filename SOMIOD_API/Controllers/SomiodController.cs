using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using SOMIOD_API.Models;
using System.Data.SqlClient;
using System.Web.Http;

namespace SOMIOD_API.Controllers
{
    [Route("api/somiod")]
    public class SomiodController : ApiController
    {
        // POST /api/somiod (Criar Aplicação)
        [HttpPost]
        public IHttpActionResult CreateApplication([FromBody] Application app)
        {
            if (app.ResType != "application") return BadRequest("ResType deve ser 'application'");

            // TODO

            return Ok($"Aplicação '{app.ResourceName}' criada.");
        }

        // GET /api/somiod (Listar Aplicações)
        [HttpGet]
        public IHttpActionResult GetApplicationsList()
        {
            // TODO
            var apps = new List<string> { "LightingApp", "HeatingApp" }; // Exemplo
            return Ok(apps);
        }
    }
}