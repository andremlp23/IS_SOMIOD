using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Net;
using System.Web.Http;
using SOMIOD_API.Models;

// [MQTT] Imports adicionados para suporte a assincronismo, XML e MQTTnet
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;

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

        // POST: api/somiod (Criar Aplicações)
        [HttpPost]
        [Route("")]
        public IHttpActionResult CreateApplication([FromBody] Application app)
        {
            // [INFO] Mantida a lógica original de nomenclatura (ex: app-1)
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
                            }
                        }
                    }
                    return Ok(containers);
                }
            }

            //Obter Aplicação
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
            if (appId == null) return NotFound();

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

            string resType = (string)body["res-type"];
            if (string.IsNullOrEmpty(resType)) return BadRequest("Falta propriedade 'res-type'.");

            int? contId = GetContainerIdByName(appName, containerName);
            if (contId == null) return NotFound();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                if (resType == "content-instance")
                {
                    // Converter para Modelo ContentInstance
                    var instance = body.ToObject<ContentInstance>();

                    // [MQTT] Garantia de nome único se não for fornecido (necessário para notificação correta)
                    if (string.IsNullOrEmpty(instance.ResourceName)) instance.ResourceName = Guid.NewGuid().ToString();

                    if (string.IsNullOrEmpty(instance.Content))
                        return BadRequest("Content é obrigatório.");

                    string sql = "INSERT INTO ContentInstance (Name, Content, ContentType, ParentContainerId) VALUES (@Name, @Content, @Type, @ParentId)";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", instance.ResourceName);
                        cmd.Parameters.AddWithValue("@Content", instance.Content);
                        cmd.Parameters.AddWithValue("@Type", instance.ContentType ?? "text/plain");
                        cmd.Parameters.AddWithValue("@ParentId", contId);
                        cmd.ExecuteNonQuery();
                    }

                    // [MQTT] BLOCO ADICIONADO: Dispara notificação de Evento 1 (Criação) para o Broker
                    NotifySubscribers((int)contId, appName, containerName, instance.ResourceName, instance.Content, 1);

                    return Ok($"Dado '{instance.ResourceName}' criado.");
                }
                else if (resType == "subscription")
                {
                    // Converter para Modelo Subscription
                    var sub = body.ToObject<Subscription>();

                    if (sub.Evt != 1 && sub.Evt != 2) return BadRequest("Evento deve ser 1 ou 2.");
                    if (string.IsNullOrEmpty(sub.Endpoint)) return BadRequest("Endpoint obrigatório.");

                    string sql = "INSERT INTO Subscription (Name, EventId, Endpoint, ParentContainerId) VALUES (@Name, @Event, @Endpoint, @ParentId)";

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
                        {
                            return Content(HttpStatusCode.Conflict, $"A subscrição '{sub.ResourceName}' já existe neste contentor.");
                        }
                        return InternalServerError(ex);
                    }
                }
            }

            return BadRequest("Tipo de recurso desconhecido.");
        }

        // DELETE: api/somiod/{appName}/{containerName}/{resourceName}
        [HttpDelete]
        [Route("{appName}/{containerName}/{resourceName}")]
        public IHttpActionResult DeleteContentInstance(string appName, string containerName, string resourceName)
        {
            // [MQTT] Precisamos do ID do contentor para buscar subscrições antes de apagar (ou logo após)
            int? contId = GetContainerIdByName(appName, containerName);
            if (contId == null) return NotFound();

            string sql = @"DELETE R FROM ContentInstance R
                           JOIN Container C ON R.ParentContainerId = C.Id
                           JOIN Application A ON C.ParentAppId = A.Id
                           WHERE A.Name = @AppName AND C.Name = @ContName AND R.Name = @ResName";

            int rows = 0;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@AppName", appName);
                    cmd.Parameters.AddWithValue("@ContName", containerName);
                    cmd.Parameters.AddWithValue("@ResName", resourceName);
                    rows = cmd.ExecuteNonQuery();
                }
            }

            // [MQTT] Lógica adicionada: Se apagou algo com sucesso, notifica Evento 2 (Deletion)
            if (rows > 0)
            {
                NotifySubscribers((int)contId, appName, containerName, resourceName, "DELETED", 2);
                return Ok("Deleted");
            }

            return NotFound();
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

        // [MQTT] MÉTODO NOVO: Responsável por encontrar subscritores e montar o XML de notificação
        private void NotifySubscribers(int containerId, string appName, string containerName, string resourceName, string contentVal, int eventType)
        {
            var endpoints = new List<string>();
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // [CORREÇÃO] Adicionado 'DISTINCT' para evitar enviar mensagens duplicadas 
                // caso existam múltiplas subscrições idênticas na BD (lixo de testes anteriores).
                string sql = "SELECT DISTINCT Endpoint FROM Subscription WHERE ParentContainerId = @ParentId AND EventId = @Evt";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ParentId", containerId);
                cmd.Parameters.AddWithValue("@Evt", eventType);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read()) endpoints.Add(reader["Endpoint"].ToString());
                }
            }

            if (endpoints.Count == 0) return;

            // ... (O resto do método mantém-se igual: Criação do XML e envio MQTT) ...
            // ...

            // (Apenas para contexto, o resto do código abaixo mantém-se igual ao que tinhas)
            XElement xmlMsg = new XElement("Notification",
                new XElement("Event", eventType == 1 ? "Creation" : "Deletion"),
                new XElement("Resource", resourceName),
                new XElement("Container", containerName),
                new XElement("App", appName),
                new XElement("Content", contentVal ?? "")
            );
            string payload = xmlMsg.ToString();
            string topic = $"api/somiod/{appName}/{containerName}";

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

        // [MQTT] MÉTODO NOVO: Responsável pela conexão e publicação assíncrona no Broker
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