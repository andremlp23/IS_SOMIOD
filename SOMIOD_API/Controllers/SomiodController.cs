using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Net;
using System.Web.Http;
using SOMIOD_API.Models;

namespace SOMIOD_API.Controllers
{
    [RoutePrefix("api/somiod")]
    public class SomiodController : ApiController
    {
        private readonly string connectionString;

        public SomiodController()
        {
            connectionString = ConfigurationManager.ConnectionStrings["SomiodConnStr"].ConnectionString;
        }

        // =============================================================
        // 1.(Applications)
        // =============================================================

        // POST: api/somiod
        [HttpPost]
        [Route("")]
        public IHttpActionResult CreateApplication([FromBody] Application app)
        {
            if (app == null) return BadRequest("Body vazio.");
            if (app.ResType != "application") return BadRequest("O 'res-type' deve ser 'application'.");

            string sql = "INSERT INTO Application (Name) VALUES (@Name)";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", app.ResourceName);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601) // Unique Constraint
                    return Content(HttpStatusCode.Conflict, $"A aplicação '{app.ResourceName}' já existe.");

                return InternalServerError(ex);
            }

            return Ok($"Aplicação '{app.ResourceName}' criada com sucesso.");
        }

        // GET: api/somiod (Listar Aplicações)
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetApplicationsList()
        {
            var apps = new List<string>();
            string sql = "SELECT Name FROM Application";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            apps.Add(reader["Name"].ToString());
                        }
                    }
                }
                return Ok(apps);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET: api/somiod/{appName} (Obter App OU Discovery de Contentores)
        [HttpGet]
        [Route("{appName}")]
        public IHttpActionResult GetApplication(string appName)
        {
            //Discovery (Listar Contentores)
            IEnumerable<string> headerValues;
            if (Request.Headers.TryGetValues("somiod-discovery", out headerValues))
            {
                if (string.Join(",", headerValues) == "container")
                {
                    var containers = new List<string>();
                    string sqlDisc = @"SELECT C.Name FROM Container C 
                                       JOIN Application A ON C.ParentAppId = A.Id 
                                       WHERE A.Name = @AppName";

                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand(sqlDisc, conn))
                        {
                            cmd.Parameters.AddWithValue("@AppName", appName);
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read()) containers.Add(reader["Name"].ToString());
                                // TODO -> Remover id do contentor que é mostrado
                            }
                        }
                    }
                    return Ok(containers);
                }
            }

            //Obter Aplicação
            Application app = null;
            string sql = "SELECT * FROM Application WHERE Name = @Name";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Name", appName);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            app = new Application
                            {
                                Id = (int)reader["Id"],
                                ResourceName = (string)reader["Name"],
                                CreationDateTime = (DateTime)reader["CreationDate"],
                                ResType = "application"
                            };
                        }
                    }
                }
            }

            if (app == null) return NotFound();
            return Ok(app);
        }

        // DELETE: api/somiod/{appName}
        [HttpDelete]
        [Route("{appName}")]
        public IHttpActionResult DeleteApplication(string appName)
        {
            string sql = "DELETE FROM Application WHERE Name = @Name";
            int rows = 0;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Name", appName);
                    rows = cmd.ExecuteNonQuery();
                }
            }

            if (rows == 0) return NotFound();
            return Ok($"Aplicação '{appName}' apagada.");
        }

        // =============================================================
        // 2.(Containers)
        // =============================================================

        // POST: api/somiod/{appName} Criar Contentor
        [HttpPost]
        [Route("{appName}")]
        public IHttpActionResult CreateContainer(string appName, [FromBody] Container container)
        {
            if (container == null) return BadRequest();
            if (container.ResType != "container") return BadRequest("O 'res-type' deve ser 'container'.");

            int? appId = GetAppIdByName(appName);
            if (appId == null) return NotFound(); // App não existe

            // 2. Inserir na DB (Tabela: Container)
            string sql = "INSERT INTO Container (Name, ParentAppId) VALUES (@Name, @ParentId)";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", container.ResourceName);
                        cmd.Parameters.AddWithValue("@ParentId", appId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627) return Content(HttpStatusCode.Conflict, "Já existe um contentor com esse nome nesta aplicação.");
                return InternalServerError(ex);
            }

            return Ok($"Contentor '{container.ResourceName}' criado em '{appName}'.");
        }

        // GET: api/somiod/{appName}/{containerName}
        [HttpGet]
        [Route("{appName}/{containerName}")]
        public IHttpActionResult GetContainer(string appName, string containerName)
        {
            // Discovery (Listar Filhos)
            IEnumerable<string> headerValues;
            if (Request.Headers.TryGetValues("somiod-discovery", out headerValues))
            {
                string disc = string.Join(",", headerValues);
                if (disc == "content-instance" || disc == "subscription")
                {
                    var children = new List<string>();
                    string table = (disc == "content-instance") ? "ContentInstance" : "Subscription";

                    string sqlDisc = $@"SELECT R.Name FROM {table} R
                                        JOIN Container C ON R.ParentContainerId = C.Id
                                        JOIN Application A ON C.ParentAppId = A.Id
                                        WHERE A.Name = @AppName AND C.Name = @ContName";

                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand(sqlDisc, conn))
                        {
                            cmd.Parameters.AddWithValue("@AppName", appName);
                            cmd.Parameters.AddWithValue("@ContName", containerName);
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read()) children.Add(reader["Name"].ToString());
                            }
                        }
                    }
                    return Ok(children);
                }
            }

            // (Obter Contentor)
            string sql = @"SELECT C.* FROM Container C
                           JOIN Application A ON C.ParentAppId = A.Id
                           WHERE A.Name = @AppName AND C.Name = @ContName";

            Container cont = null;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@AppName", appName);
                    cmd.Parameters.AddWithValue("@ContName", containerName);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            cont = new Container
                            {
                                ResourceName = (string)reader["Name"],
                                CreationDateTime = (DateTime)reader["CreationDate"],
                                ResType = "container"
                            };
                        }
                    }
                }
            }

            if (cont == null) return NotFound();
            return Ok(cont);
        }

        // DELETE: api/somiod/{appName}/{containerName}
        [HttpDelete]
        [Route("{appName}/{containerName}")]
        public IHttpActionResult DeleteContainer(string appName, string containerName)
        {
            // Apaga contentor
            string sql = @"DELETE C FROM Container C
                           JOIN Application A ON C.ParentAppId = A.Id
                           WHERE A.Name = @AppName AND C.Name = @ContName";

            int rows = 0;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@AppName", appName);
                    cmd.Parameters.AddWithValue("@ContName", containerName);
                    rows = cmd.ExecuteNonQuery();
                }
            }

            if (rows == 0) return NotFound();
            return Ok($"Contentor '{containerName}' apagado.");
        }

        // =============================================================
        // 3.(ContentInstance & Subscription)
        // =============================================================

        // POST: api/somiod/{appName}/{containerName}
        [HttpPost]
        [Route("{appName}/{containerName}")]
        public IHttpActionResult CreateResource(string appName, string containerName, [FromBody] Newtonsoft.Json.Linq.JObject body)
        {
            if (body == null) return BadRequest();

            // Ler res-type
            string resType = (string)body["res-type"];
            if (string.IsNullOrEmpty(resType)) return BadRequest("Falta propriedade 'res-type'.");

            // Obter ID do Contentor Pai
            int? contId = GetContainerIdByName(appName, containerName);
            if (contId == null) return NotFound();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                if (resType == "content-instance")
                {
                    // Converter para Modelo ContentInstance
                    var instance = body.ToObject<ContentInstance>();

                    // Validar Content e ContentType (Obrigatórios na DB)
                    if (string.IsNullOrEmpty(instance.Content) || string.IsNullOrEmpty(instance.ContentType))
                        return BadRequest("Content e ContentType são obrigatórios.");

                    string sql = "INSERT INTO ContentInstance (Name, Content, ContentType, ParentContainerId) VALUES (@Name, @Content, @Type, @ParentId)";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", instance.ResourceName);
                        cmd.Parameters.AddWithValue("@Content", instance.Content); // Coluna: Content
                        cmd.Parameters.AddWithValue("@Type", instance.ContentType); // Coluna: ContentType
                        cmd.Parameters.AddWithValue("@ParentId", contId);
                        cmd.ExecuteNonQuery();
                    }
                    return Ok($"Dado '{instance.ResourceName}' criado.");
                }
                else if (resType == "subscription")
                {
                    // Converter para Modelo Subscription
                    var sub = body.ToObject<Subscription>();

                    // Validar Event e Endpoint
                    if (sub.Evt != 1 && sub.Evt != 2) return BadRequest("Evento deve ser 1 ou 2.");
                    if (string.IsNullOrEmpty(sub.Endpoint)) return BadRequest("Endpoint obrigatório.");

                    string sql = "INSERT INTO Subscription (Name, EventId, Endpoint, ParentContainerId) VALUES (@Name, @Event, @Endpoint, @ParentId)";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", sub.ResourceName);
                        cmd.Parameters.AddWithValue("@Event", sub.Evt);
                        cmd.Parameters.AddWithValue("@Endpoint", sub.Endpoint);
                        cmd.Parameters.AddWithValue("@ParentId", contId);
                        cmd.ExecuteNonQuery();
                    }
                    return Ok($"Subscrição '{sub.ResourceName}' criada.");
                }
            }

            return BadRequest("Tipo de recurso desconhecido.");
        }

        // DELETE: api/somiod/{appName}/{containerName}/{resourceName}

        // GET/DELETE ContentInstance
        [HttpDelete]
        [Route("{appName}/{containerName}/{resourceName}")]
        public IHttpActionResult DeleteContentInstance(string appName, string containerName, string resourceName)
        {
            string sql = @"DELETE R FROM ContentInstance R
                           JOIN Container C ON R.ParentContainerId = C.Id
                           JOIN Application A ON C.ParentAppId = A.Id
                           WHERE A.Name = @AppName AND C.Name = @ContName AND R.Name = @ResName";
            
            return Ok("Deleted");
        }

        // =============================================================
        // MÉTODOS AUXILIARES
        // =============================================================
        private int? GetAppIdByName(string name)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT Id FROM Application WHERE Name = @Name", conn);
                cmd.Parameters.AddWithValue("@Name", name);
                object res = cmd.ExecuteScalar();
                if (res != null) return (int)res;
            }
            return null;
        }

        private int? GetContainerIdByName(string appName, string contName)
        {
            string sql = @"SELECT C.Id FROM Container C 
                           JOIN Application A ON C.ParentAppId = A.Id 
                           WHERE A.Name = @AppName AND C.Name = @ContName";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@AppName", appName);
                cmd.Parameters.AddWithValue("@ContName", contName);
                object res = cmd.ExecuteScalar();
                if (res != null) return (int)res;
            }
            return null;
        }
    }
}