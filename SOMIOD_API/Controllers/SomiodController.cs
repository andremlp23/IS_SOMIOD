using SOMIOD_API.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Text;

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
        // 1. APPLICATIONS
        // =============================================================

        [HttpPost]
        [Route("")]
        public IHttpActionResult CreateApplication([FromBody] Application app)
        {
            if (app == null) return BadRequest("Body vazio.");
            if (app.ResType != "application") return BadRequest("O 'res-type' deve ser 'application'.");

            string finalName = app.ResourceName;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string checkSql = "SELECT COUNT(*) FROM Application WHERE Name = @Name OR Name LIKE @NameLike";

                    int count = 0;
                    using (SqlCommand checkCmd = new SqlCommand(checkSql, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@Name", app.ResourceName);
                        checkCmd.Parameters.AddWithValue("@NameLike", app.ResourceName + "-%");
                        count = (int)checkCmd.ExecuteScalar();
                    }

                    if (count > 0)
                    {
                        finalName = $"{app.ResourceName}-{count}";
                    }

                    string insertSql = "INSERT INTO Application (Name) VALUES (@Name)";
                    using (SqlCommand cmd = new SqlCommand(insertSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", finalName);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601)
                    return Content(HttpStatusCode.Conflict, $"Erro de conflito: A aplicação '{finalName}' já existe.");

                return InternalServerError(ex);
            }

            return Ok($"Aplicação '{finalName}' criada com sucesso.");
        }

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
                        while (reader.Read()) apps.Add(reader["Name"].ToString());
                }
                return Ok(apps);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet]
        [Route("{appName}")]
        public IHttpActionResult GetApplication(string appName)
        {
            IEnumerable<string> headerValues;

            if (Request.Headers.TryGetValues("somiod-discovery", out headerValues))
            {
                if (string.Join(",", headerValues) == "container")
                {
                    var containers = new List<string>();
                    string sqlDisc =
                        @"SELECT C.Name FROM Container C 
                          JOIN Application A ON C.ParentAppId = A.Id 
                          WHERE A.Name = @AppName";

                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand(sqlDisc, conn))
                        {
                            cmd.Parameters.AddWithValue("@AppName", appName);
                            using (SqlDataReader reader = cmd.ExecuteReader())
                                while (reader.Read()) containers.Add(reader["Name"].ToString());
                        }
                    }
                    return Ok(containers);
                }
            }

            Application app = null;
            string sql = "SELECT Name, CreationDate FROM Application WHERE Name = @Name";

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
        // 2. CONTAINERS
        // =============================================================

        [HttpPost]
        [Route("{appName}")]
        public IHttpActionResult CreateContainer(string appName, [FromBody] Container container)
        {
            if (container == null) return BadRequest();
            if (container.ResType != "container") return BadRequest("O 'res-type' deve ser 'container'.");

            int? appId = GetAppIdByName(appName);
            if (appId == null) return NotFound();

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
                if (ex.Number == 2627)
                    return Content(HttpStatusCode.Conflict, "Já existe um contentor com esse nome nesta aplicação.");

                return InternalServerError(ex);
            }

            return Ok($"Contentor '{container.ResourceName}' criado em '{appName}'.");
        }

        [HttpGet]
        [Route("{appName}/{containerName}")]
        public IHttpActionResult GetContainer(string appName, string containerName)
        {
            IEnumerable<string> headerValues;

            if (Request.Headers.TryGetValues("somiod-discovery", out headerValues))
            {
                string disc = string.Join(",", headerValues);

                if (disc == "content-instance" || disc == "subscription")
                {
                    var children = new List<string>();
                    string table = (disc == "content-instance") ? "ContentInstance" : "Subscription";

                    string sqlDisc =
                        $@"SELECT R.Name FROM {table} R
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
                                while (reader.Read()) children.Add(reader["Name"].ToString());
                        }
                    }
                    return Ok(children);
                }
            }

            string sqlContainer =
                @"SELECT C.* FROM Container C
                  JOIN Application A ON C.ParentAppId = A.Id
                  WHERE A.Name = @AppName AND C.Name = @ContName";

            Container cont = null;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sqlContainer, conn))
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

        [HttpDelete]
        [Route("{appName}/{containerName}")]
        public IHttpActionResult DeleteContainer(string appName, string containerName)
        {
            string sql =
                @"DELETE C FROM Container C
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
        // 3. CONTENT-INSTANCE & SUBSCRIPTION
        // =============================================================

        [HttpPost]
        [Route("{appName}/{containerName}")]
        public IHttpActionResult CreateResource(string appName, string containerName, [FromBody] Newtonsoft.Json.Linq.JObject body)
        {
            if (body == null) return BadRequest();

            string resType = (string)body["res-type"];
            if (string.IsNullOrEmpty(resType)) return BadRequest("Falta propriedade 'res-type'.");

            int? contId = GetContainerIdByName(appName, containerName);
            if (contId == null) return NotFound();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                if (resType == "content-instance")
                {
                    var instance = body.ToObject<ContentInstance>();

                    if (string.IsNullOrEmpty(instance.Content) || string.IsNullOrEmpty(instance.ContentType))
                        return BadRequest("Content e ContentType são obrigatórios.");

                    string sql =
                        @"INSERT INTO ContentInstance (Name, Content, ContentType, ParentContainerId)
                          VALUES (@Name, @Content, @Type, @ParentId)";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", instance.ResourceName);
                        cmd.Parameters.AddWithValue("@Content", instance.Content);
                        cmd.Parameters.AddWithValue("@Type", instance.ContentType);
                        cmd.Parameters.AddWithValue("@ParentId", contId);
                        cmd.ExecuteNonQuery();
                    }

                    FireNotifications(appName, containerName, instance.Content, 1);
                    return Ok($"Dado '{instance.ResourceName}' criado.");
                }
                else if (resType == "subscription")
                {
                    var sub = body.ToObject<Subscription>();

                    if (sub.Evt != 1 && sub.Evt != 2)
                        return BadRequest("Evento deve ser 1 ou 2.");

                    if (string.IsNullOrEmpty(sub.Endpoint))
                        return BadRequest("Endpoint obrigatório.");

                    string sql =
                        @"INSERT INTO Subscription (Name, EventId, Endpoint, ParentContainerId)
                          VALUES (@Name, @Event, @Endpoint, @ParentId)";

                    try
                    {
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
                    catch (SqlException ex)
                    {
                        if (ex.Number == 2627 || ex.Number == 2601)
                            return Content(HttpStatusCode.Conflict, $"A subscrição '{sub.ResourceName}' já existe neste contentor.");

                        return InternalServerError(ex);
                    }
                }
            }

            return BadRequest("Tipo de recurso desconhecido.");
        }

        // =============================================================
        // DELETE CONTENT-INSTANCE
        // =============================================================

        [HttpDelete]
        [Route("{appName}/{containerName}/{resourceName}")]
        public IHttpActionResult DeleteContentInstance(string appName, string containerName, string resourceName)
        {
            string sql =
                @"DELETE R FROM ContentInstance R
                  JOIN Container C ON R.ParentContainerId = C.Id
                  JOIN Application A ON C.ParentAppId = A.Id
                  WHERE A.Name = @AppName AND C.Name = @ContName AND R.Name = @ResName";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@AppName", appName);
                    cmd.Parameters.AddWithValue("@ContName", containerName);
                    cmd.Parameters.AddWithValue("@ResName", resourceName);
                    cmd.ExecuteNonQuery();
                }
            }

            return Ok("Deleted");
        }

        // =============================================================
        // AUXILIARES
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
            string sql =
                @"SELECT C.Id FROM Container C 
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

        // =============================================================
        // FIRE NOTIFICATIONS (MQTT) — CORRIGIDO
        // =============================================================
        private void FireNotifications(string appName, string contName, string content, int eventType)
        {
            string sql =
                @"SELECT S.Endpoint FROM Subscription S
                  JOIN Container C ON S.ParentContainerId = C.Id
                  JOIN Application A ON C.ParentAppId = A.Id
                  WHERE A.Name = @App AND C.Name = @Cont AND (S.EventId = @Evt OR S.EventId = 3)";

            var endpoints = new List<string>();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@App", appName);
                        cmd.Parameters.AddWithValue("@Cont", contName);
                        cmd.Parameters.AddWithValue("@Evt", eventType);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                            while (reader.Read())
                                endpoints.Add(reader["Endpoint"].ToString());
                    }
                }
            }
            catch
            {
                return;
            }

            if (endpoints.Count == 0) return;

            Task.Run(() =>
            {
                foreach (var rawEndpoint in endpoints)
                {
                    try
                    {
                        string clean = rawEndpoint.Trim().ToLower();

                        if (!clean.StartsWith("mqtt://"))
                            continue;

                        string afterPrefix = clean.Replace("mqtt://", "");

                        string host = afterPrefix.Contains(":")
                            ? afterPrefix.Split(':')[0]
                            : afterPrefix;

                        if (string.IsNullOrWhiteSpace(host))
                            continue;

                        string portStr = afterPrefix.Contains(":")
                            ? afterPrefix.Split(':')[1]
                            : "1883";

                        int port;
                        if (!int.TryParse(portStr, out port))
                            port = 1883;

                        var client = new MqttClient(host, port, false, null, null, MqttSslProtocols.None);

                        string clientId = Guid.NewGuid().ToString();
                        client.Connect(clientId);

                        if (!client.IsConnected)
                            continue;

                        string topic = $"api/somiod/{appName}/{contName}".Trim();
                        byte[] msgBytes = Encoding.UTF8.GetBytes(content);

                        client.Publish(topic, msgBytes, MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);

                        client.Disconnect();
                    }
                    catch
                    {
                        continue;
                    }
                }
            });
        }
    }
}
