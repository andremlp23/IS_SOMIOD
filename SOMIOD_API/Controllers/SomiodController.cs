using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Xml.Linq;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
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
        // 1. APPLICATIONS
        // =============================================================

        [HttpPost]
        [Route("")]
        public IHttpActionResult CreateApplication([FromBody] Application app)
        {
            if (app == null || app.ResType != "application") return BadRequest("Invalid resource type.");

            string finalName = app.ResourceName;
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    // Lógica para garantir nome único simples
                    string checkSql = "SELECT COUNT(*) FROM Application WHERE Name = @Name";
                    SqlCommand checkCmd = new SqlCommand(checkSql, conn);
                    checkCmd.Parameters.AddWithValue("@Name", app.ResourceName);
                    int count = (int)checkCmd.ExecuteScalar();

                    if (count > 0) finalName = $"{app.ResourceName}-{Guid.NewGuid().ToString().Substring(0, 4)}";

                    string insertSql = "INSERT INTO Application (Name) VALUES (@Name)";
                    SqlCommand cmd = new SqlCommand(insertSql, conn);
                    cmd.Parameters.AddWithValue("@Name", finalName);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { return InternalServerError(ex); }

            return Ok($"Application '{finalName}' created.");
        }

        [HttpGet]
        [Route("")]
        public IHttpActionResult GetApplicationsList()
        {
            // Header somiod-discovery: application [cite: 71]
            // Deve retornar caminhos completos [cite: 72]
            var apps = new List<string>();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    SqlCommand cmd = new SqlCommand("SELECT Name FROM Application", conn);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Retorna o PATH completo conforme enunciado
                            apps.Add($"/api/somiod/{reader["Name"]}");
                        }
                    }
                }
                return Ok(apps);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpGet]
        [Route("{appName}")]
        public IHttpActionResult GetApplication(string appName)
        {
            // Headers handling for Discovery [cite: 71]
            if (Request.Headers.TryGetValues("somiod-discovery", out var headerValues))
            {
                string discType = string.Join(",", headerValues);
                if (discType == "container")
                {
                    var containers = new List<string>();
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        string sql = @"SELECT C.Name FROM Container C 
                                       JOIN Application A ON C.ParentAppId = A.Id 
                                       WHERE A.Name = @AppName";
                        SqlCommand cmd = new SqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue("@AppName", appName);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                containers.Add($"/api/somiod/{appName}/{reader["Name"]}");
                        }
                    }
                    return Ok(containers);
                }
            }

            // Normal GET Application
            Application app = null;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT * FROM Application WHERE Name = @Name", conn);
                cmd.Parameters.AddWithValue("@Name", appName);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        app = new Application
                        {
                            ResourceName = reader["Name"].ToString(),
                            CreationDateTime = Convert.ToDateTime(reader["CreationDate"]),
                            ResType = "application"
                        };
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
            // Nota: Deveria haver Cascade Delete na DB ou apagar filhos primeiro aqui.
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("DELETE FROM Application WHERE Name = @Name", conn);
                cmd.Parameters.AddWithValue("@Name", appName);
                int rows = cmd.ExecuteNonQuery();
                if (rows == 0) return NotFound();
            }
            return Ok($"Application '{appName}' deleted.");
        }

        // =============================================================
        // 2. CONTAINERS
        // =============================================================

        [HttpPost]
        [Route("{appName}")]
        public IHttpActionResult CreateContainer(string appName, [FromBody] Container container)
        {
            if (container == null || container.ResType != "container") return BadRequest();

            int? appId = GetAppIdByName(appName);
            if (appId == null) return NotFound();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string sql = "INSERT INTO Container (Name, ParentAppId) VALUES (@Name, @ParentId)";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Name", container.ResourceName);
                    cmd.Parameters.AddWithValue("@ParentId", appId);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627) return Content(HttpStatusCode.Conflict, "Container exists.");
                return InternalServerError(ex);
            }
            return Ok($"Container '{container.ResourceName}' created.");
        }

        [HttpGet]
        [Route("{appName}/{containerName}")]
        public IHttpActionResult GetContainer(string appName, string containerName)
        {
            // Discovery Logic
            if (Request.Headers.TryGetValues("somiod-discovery", out var headerValues))
            {
                string disc = string.Join(",", headerValues);
                if (disc == "content-instance" || disc == "subscription")
                {
                    var children = new List<string>();
                    string table = (disc == "content-instance") ? "ContentInstance" : "Subscription";
                    // Para subscrições, o path deve incluir /subs/ [cite: 54]
                    string pathMid = (disc == "subscription") ? "/subs/" : "/";

                    string sql = $@"SELECT R.Name FROM {table} R
                                    JOIN Container C ON R.ParentContainerId = C.Id
                                    JOIN Application A ON C.ParentAppId = A.Id
                                    WHERE A.Name = @AppName AND C.Name = @ContName";

                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        SqlCommand cmd = new SqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue("@AppName", appName);
                        cmd.Parameters.AddWithValue("@ContName", containerName);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                children.Add($"/api/somiod/{appName}/{containerName}{pathMid}{reader["Name"]}");
                        }
                    }
                    return Ok(children);
                }
            }

            // Normal GET Container
            Container cont = null;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string sql = @"SELECT C.* FROM Container C
                               JOIN Application A ON C.ParentAppId = A.Id
                               WHERE A.Name = @AppName AND C.Name = @ContName";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@AppName", appName);
                cmd.Parameters.AddWithValue("@ContName", containerName);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        cont = new Container
                        {
                            ResourceName = reader["Name"].ToString(),
                            CreationDateTime = Convert.ToDateTime(reader["CreationDate"]),
                            ResType = "container"
                        };
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
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string sql = @"DELETE C FROM Container C
                               JOIN Application A ON C.ParentAppId = A.Id
                               WHERE A.Name = @AppName AND C.Name = @ContName";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@AppName", appName);
                cmd.Parameters.AddWithValue("@ContName", containerName);
                int rows = cmd.ExecuteNonQuery();
                if (rows == 0) return NotFound();
            }
            return Ok($"Container '{containerName}' deleted.");
        }

        // =============================================================
        // 3. RESOURCES (ContentInstance & Subscription)
        // =============================================================

        [HttpPost]
        [Route("{appName}/{containerName}")]
        public IHttpActionResult CreateResource(string appName, string containerName, [FromBody] Newtonsoft.Json.Linq.JObject body)
        {
            string resType = (string)body["res-type"];
            int? contId = GetContainerIdByName(appName, containerName);
            if (contId == null) return NotFound();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                if (resType == "content-instance")
                {
                    var instance = body.ToObject<ContentInstance>();
                    // Garantir nome único se vazio [cite: 83]
                    if (string.IsNullOrEmpty(instance.ResourceName)) instance.ResourceName = Guid.NewGuid().ToString();

                    string sql = "INSERT INTO ContentInstance (Name, Content, ContentType, ParentContainerId) VALUES (@Name, @Content, @Type, @ParentId)";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Name", instance.ResourceName);
                    cmd.Parameters.AddWithValue("@Content", instance.Content);
                    cmd.Parameters.AddWithValue("@Type", instance.ContentType ?? "text/plain");
                    cmd.Parameters.AddWithValue("@ParentId", contId);
                    cmd.ExecuteNonQuery();

                    // NOTIFICAÇÃO MQTT - EVENTO 1 (Creation) [cite: 58]
                    NotifySubscribers((int)contId, appName, containerName, instance.ResourceName, instance.Content, 1);

                    return Ok($"Content-Instance '{instance.ResourceName}' created.");
                }
                else if (resType == "subscription")
                {
                    var sub = body.ToObject<Subscription>();
                    string sql = "INSERT INTO Subscription (Name, EventId, Endpoint, ParentContainerId) VALUES (@Name, @Event, @Endpoint, @ParentId)";
                    try
                    {
                        SqlCommand cmd = new SqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue("@Name", sub.ResourceName);
                        cmd.Parameters.AddWithValue("@Event", sub.Evt);
                        cmd.Parameters.AddWithValue("@Endpoint", sub.Endpoint);
                        cmd.Parameters.AddWithValue("@ParentId", contId);
                        cmd.ExecuteNonQuery();
                        return Ok($"Subscription '{sub.ResourceName}' created.");
                    }
                    catch (SqlException) { return Conflict(); }
                }
            }
            return BadRequest("Unknown res-type.");
        }

        // DELETE CONTENT-INSTANCE
        [HttpDelete]
        [Route("{appName}/{containerName}/{resourceName}")]
        public IHttpActionResult DeleteContentInstance(string appName, string containerName, string resourceName)
        {
            int? contId = GetContainerIdByName(appName, containerName);
            if (contId == null) return NotFound();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string sql = @"DELETE R FROM ContentInstance R
                               JOIN Container C ON R.ParentContainerId = C.Id
                               JOIN Application A ON C.ParentAppId = A.Id
                               WHERE A.Name = @AppName AND C.Name = @ContName AND R.Name = @ResName";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@AppName", appName);
                cmd.Parameters.AddWithValue("@ContName", containerName);
                cmd.Parameters.AddWithValue("@ResName", resourceName);
                int rows = cmd.ExecuteNonQuery();

                if (rows > 0)
                {
                    // NOTIFICAÇÃO MQTT - EVENTO 2 (Deletion) [cite: 58]
                    NotifySubscribers((int)contId, appName, containerName, resourceName, "DELETED", 2);
                    return Ok("Deleted");
                }
            }
            return NotFound();
        }

        // =============================================================
        // 4. ROTAS ESPECÍFICAS PARA SUBSCRIPTIONS (/subs/)
        // =============================================================

        [HttpGet]
        [Route("{appName}/{containerName}/subs/{subName}")]
        public IHttpActionResult GetSubscription(string appName, string containerName, string subName)
        {
            Subscription sub = null;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string sql = @"SELECT S.* FROM Subscription S
                               JOIN Container C ON S.ParentContainerId = C.Id
                               JOIN Application A ON C.ParentAppId = A.Id
                               WHERE A.Name = @AppName AND C.Name = @ContName AND S.Name = @SubName";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@AppName", appName);
                cmd.Parameters.AddWithValue("@ContName", containerName);
                cmd.Parameters.AddWithValue("@SubName", subName);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        sub = new Subscription
                        {
                            ResourceName = reader["Name"].ToString(),
                            Evt = (int)reader["EventId"],
                            Endpoint = reader["Endpoint"].ToString(),
                            ResType = "subscription",
                            CreationDateTime = Convert.ToDateTime(reader["CreationDate"])
                        };
                    }
                }
            }
            if (sub == null) return NotFound();
            return Ok(sub);
        }

        [HttpDelete]
        [Route("{appName}/{containerName}/subs/{subName}")]
        public IHttpActionResult DeleteSubscription(string appName, string containerName, string subName)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string sql = @"DELETE S FROM Subscription S
                               JOIN Container C ON S.ParentContainerId = C.Id
                               JOIN Application A ON C.ParentAppId = A.Id
                               WHERE A.Name = @AppName AND C.Name = @ContName AND S.Name = @SubName";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@AppName", appName);
                cmd.Parameters.AddWithValue("@ContName", containerName);
                cmd.Parameters.AddWithValue("@SubName", subName);
                int rows = cmd.ExecuteNonQuery();
                if (rows == 0) return NotFound();
            }
            return Ok($"Subscription '{subName}' deleted.");
        }

        // =============================================================
        // HELPERS & MQTT ENGINE
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
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string sql = @"SELECT C.Id FROM Container C 
                               JOIN Application A ON C.ParentAppId = A.Id 
                               WHERE A.Name = @AppName AND C.Name = @ContName";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@AppName", appName);
                cmd.Parameters.AddWithValue("@ContName", contName);
                object res = cmd.ExecuteScalar();
                if (res != null) return (int)res;
            }
            return null;
        }

        private void NotifySubscribers(int containerId, string appName, string containerName, string resourceName, string contentVal, int eventType)
        {
            // 1. Obter endpoints subscritos
            var endpoints = new List<string>();
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                // Verifica se a subscrição quer saber deste evento (Creation=1 ou Deletion=2)
                string sql = "SELECT Endpoint FROM Subscription WHERE ParentContainerId = @ParentId AND (EventId = @Evt OR EventId = 0)"; // EventId 0 could mean 'Both' if implemented
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ParentId", containerId);
                cmd.Parameters.AddWithValue("@Evt", eventType);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read()) endpoints.Add(reader["Endpoint"].ToString());
                }
            }

            if (endpoints.Count == 0) return;

            // 2. Construir XML de notificação [cite: 58, 93]
            XElement xmlMsg = new XElement("Notification",
                new XElement("Event", eventType == 1 ? "Creation" : "Deletion"),
                new XElement("Resource", resourceName),
                new XElement("Container", containerName),
                new XElement("App", appName),
                new XElement("Content", contentVal ?? "")
            );
            string payload = xmlMsg.ToString();
            string topic = $"api/somiod/{appName}/{containerName}";

            // 3. Disparar MQTT
            foreach (var ep in endpoints)
            {
                if (ep.ToLower().StartsWith("mqtt://"))
                {
                    try
                    {
                        var uri = new Uri(ep);
                        string brokerIp = uri.Host;
                        int port = uri.Port > 0 ? uri.Port : 1883;

                        Task.Run(async () => await PublishMqttAsync(brokerIp, port, topic, payload));
                    }
                    catch { }
                }
            }
        }

        private async Task PublishMqttAsync(string ip, int port, string topic, string payload)
        {
            try
            {
                var factory = new MqttFactory();
                using (var mqttClient = factory.CreateMqttClient())
                {
                    var options = new MqttClientOptionsBuilder()
                        .WithTcpServer(ip, port)
                        .WithClientId("SOMIOD_Server_" + Guid.NewGuid())
                        .Build();
                    await mqttClient.ConnectAsync(options, CancellationToken.None);
                    var msg = new MqttApplicationMessageBuilder()
                        .WithTopic(topic)
                        .WithPayload(payload)
                        .WithExactlyOnceQoS()
                        .Build();
                    await mqttClient.PublishAsync(msg, CancellationToken.None);
                    await mqttClient.DisconnectAsync();
                }
            }
            catch { }
        }
    }
}