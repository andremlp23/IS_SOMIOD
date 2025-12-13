using SOMIOD_API.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Net;
using System.Web.Http;

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

        // ============================
        // 1. APLICAÇÕES (Applications)
        // ============================

        // POST: api/somiod
        [HttpPost]
        [Route("")]
        public IHttpActionResult CreateApplication([FromBody] Application app)
        {
            if (app == null) return BadRequest("Body vazio.");
            if (app.ResType != "application") return BadRequest("O 'res-type' deve ser 'application'.");

            // Se não houver nome, gera um.
            if (string.IsNullOrEmpty(app.ResourceName))
            {
                string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                app.ResourceName = "app-" + timestamp;
            }

            string finalName = app.ResourceName;

            string sql = @"INSERT INTO Application (Name) VALUES (@Name);
                           SELECT Id, Name, CreationDate FROM Application WHERE Id = SCOPE_IDENTITY();";

            Application createdApp = null;

            bool inserted = ExecuteSqlWithRetry(sql, (cmd, name) =>
            {
                cmd.Parameters.AddWithValue("@Name", name);

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        createdApp = new Application
                        {
                            ResourceName = (string)reader["Name"],
                            CreationDateTime = (DateTime)reader["CreationDate"],
                            ResType = "application"
                        };
                    }
                }
            }, ref finalName);

            if (!inserted) return InternalServerError();
            if (createdApp == null) return InternalServerError();

            return Ok(createdApp);
        }

        // GET: api/somiod (Discovery de Aplicações)
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetApplicationsList()
        {
            // --- DISCOVERY GLOBAL (Recursivo a partir da Raiz) ---

            // Discovery de ContentInstances
            if (IsDiscoveryRequest("content-instance"))
            {
                var instances = new List<string>();
                string sqlDisc = @"
                    SELECT CI.Name AS InstanceName, C.Name AS ContainerName, A.Name AS AppName
                    FROM ContentInstance CI
                    JOIN Container C ON CI.ParentContainerId = C.Id
                    JOIN Application A ON C.ParentAppId = A.Id";

                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand(sqlDisc, conn))
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string appName = reader["AppName"].ToString();
                                string contName = reader["ContainerName"].ToString();
                                string instName = reader["InstanceName"].ToString();
                                instances.Add($"/api/somiod/{appName}/{contName}/{instName}");
                            }
                        }
                    }
                    return Ok(instances);
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }

            // Discovery de Containers
            if (IsDiscoveryRequest("container"))
            {
                var containers = new List<string>();
                string sqlDisc = @"
                    SELECT C.Name AS ContainerName, A.Name AS AppName
                    FROM Container C
                    JOIN Application A ON C.ParentAppId = A.Id";

                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand(sqlDisc, conn))
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string appName = reader["AppName"].ToString();
                                string contName = reader["ContainerName"].ToString();
                                containers.Add($"/api/somiod/{appName}/{contName}");
                            }
                        }
                    }
                    return Ok(containers);
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }

            // Discovery de Subscriptions
            if (IsDiscoveryRequest("subscription"))
            {
                var subs = new List<string>();
                string sqlDisc = @"
                    SELECT S.Name AS SubName, C.Name AS ContainerName, A.Name AS AppName
                    FROM Subscription S
                    JOIN Container C ON S.ParentContainerId = C.Id
                    JOIN Application A ON C.ParentAppId = A.Id";

                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand(sqlDisc, conn))
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string appName = reader["AppName"].ToString();
                                string contName = reader["ContainerName"].ToString();
                                string subName = reader["SubName"].ToString();

                                subs.Add($"/api/somiod/{appName}/{contName}/subs/{subName}");
                            }
                        }
                    }
                    return Ok(subs);
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }

            // --- NORMAL GET (Lista de Aplicações) ---
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
                            apps.Add($"/api/somiod/{reader["Name"]}");
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

        // GET: api/somiod/{appName}
        [HttpGet]
        [Route("{appName}")]
        public IHttpActionResult GetApplication(string appName)
        {
            // --- DISCOVERY ---
            string discoveryType = GetDiscoveryType();

            if (!string.IsNullOrEmpty(discoveryType))
            {
                var resources = new List<string>();
                string sqlDisc = "";

                if (discoveryType == "container")
                {
                    sqlDisc = @"SELECT C.Name FROM Container C 
                                JOIN Application A ON C.ParentAppId = A.Id 
                                WHERE A.Name = @AppName";
                }
                else if (discoveryType == "content-instance")
                {
                    sqlDisc = @"SELECT CI.Name, C.Name as ContainerName 
                                FROM ContentInstance CI
                                JOIN Container C ON CI.ParentContainerId = C.Id
                                JOIN Application A ON C.ParentAppId = A.Id
                                WHERE A.Name = @AppName";
                }
                else if (discoveryType == "subscription")
                {
                    sqlDisc = @"SELECT S.Name, C.Name as ContainerName
                                FROM Subscription S
                                JOIN Container C ON S.ParentContainerId = C.Id
                                JOIN Application A ON C.ParentAppId = A.Id
                                WHERE A.Name = @AppName";
                }
                else
                {
                    return BadRequest("Tipo de discovery inválido para nível de Aplicação.");
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(sqlDisc, conn))
                    {
                        cmd.Parameters.AddWithValue("@AppName", appName);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string resName = reader["Name"].ToString();

                                if (discoveryType == "container")
                                {
                                    resources.Add($"/api/somiod/{appName}/{resName}");
                                }
                                else if (discoveryType == "content-instance")
                                {
                                    string contName = reader["ContainerName"].ToString();
                                    resources.Add($"/api/somiod/{appName}/{contName}/{resName}");
                                }
                                else if (discoveryType == "subscription")
                                {
                                    string contName = reader["ContainerName"].ToString();
                                    resources.Add($"/api/somiod/{appName}/{contName}/subs/{resName}");
                                }
                            }
                        }
                    }
                }
                return Ok(resources);
            }

            // --- NORMAL GET (Dados da App) ---
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
            return Ok($"Aplicação '{appName}' e todos os seus filhos apagados.");
        }

        // ===========================
        // 2. CONTENTORES (Containers)
        // ===========================

        // POST: api/somiod/{appName}
        [HttpPost]
        [Route("{appName}")]
        public IHttpActionResult CreateContainer(string appName, [FromBody] Container container)
        {
            if (container == null) return BadRequest("Body vazio.");
            if (container.ResType != "container") return BadRequest("O 'res-type' deve ser 'container'.");

            int? appId = GetAppIdByName(appName);
            if (appId == null) return NotFound();

            // Auto-naming se vazio
            if (string.IsNullOrEmpty(container.ResourceName))
            {
                string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                container.ResourceName = "cont-" + timestamp;
            }

            string finalName = container.ResourceName;

            string sql = @"INSERT INTO Container (Name, ParentAppId) VALUES (@Name, @ParentId);
                           SELECT Id, Name, CreationDate, ParentAppId FROM Container WHERE Id = SCOPE_IDENTITY();";

            Container createdContainer = null;

            bool inserted = ExecuteSqlWithRetry(sql, (cmd, name) =>
            {
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@ParentId", appId);

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        createdContainer = new Container
                        {
                            ResourceName = (string)reader["Name"],
                            CreationDateTime = (DateTime)reader["CreationDate"],
                            ResType = "container"
                        };
                    }
                }
            }, ref finalName);

            if (!inserted) return InternalServerError();
            if (createdContainer == null) return InternalServerError();

            return Ok(createdContainer);
        }

        // GET: api/somiod/{appName}/{containerName}
        [HttpGet]
        [Route("{appName}/{containerName}")]
        public IHttpActionResult GetContainer(string appName, string containerName)
        {
            string discoveryType = GetDiscoveryType();

            if (discoveryType == "content-instance" || discoveryType == "subscription")
            {
                var children = new List<string>();
                string sqlDisc = "";

                if (discoveryType == "content-instance")
                {
                    sqlDisc = @"SELECT Name FROM ContentInstance 
                                WHERE ParentContainerId = (SELECT C.Id FROM Container C 
                                                           JOIN Application A ON C.ParentAppId = A.Id 
                                                           WHERE A.Name = @AppName AND C.Name = @ContName)";
                }
                else // subscription
                {
                    sqlDisc = @"SELECT Name FROM Subscription 
                                WHERE ParentContainerId = (SELECT C.Id FROM Container C 
                                                           JOIN Application A ON C.ParentAppId = A.Id 
                                                           WHERE A.Name = @AppName AND C.Name = @ContName)";
                }

                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand(sqlDisc, conn))
                        {
                            cmd.Parameters.AddWithValue("@AppName", appName);
                            cmd.Parameters.AddWithValue("@ContName", containerName);
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string name = reader["Name"].ToString();

                                    if (discoveryType == "subscription")
                                        children.Add($"/api/somiod/{appName}/{containerName}/subs/{name}");
                                    else
                                        children.Add($"/api/somiod/{appName}/{containerName}/{name}");
                                }
                            }
                        }
                    }
                    return Ok(children);
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }

            // Normal GET (Dados do Contentor)
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

        // ===========================================
        // 3. RECURSOS: ContentInstance & Subscription
        // ===========================================

        // POST: api/somiod/{appName}/{containerName}
        [HttpPost]
        [Route("{appName}/{containerName}")]
        public IHttpActionResult CreateResource(string appName, string containerName, [FromBody] Newtonsoft.Json.Linq.JObject body)
        {
            if (body == null) return BadRequest("Body vazio.");
            string resType = (string)body["res-type"];

            int? contId = GetContainerIdByName(appName, containerName);
            if (contId == null) return NotFound();

            if (resType == "content-instance")
            {
                var instance = body.ToObject<ContentInstance>();
                if (string.IsNullOrEmpty(instance.Content) || string.IsNullOrEmpty(instance.ContentType))
                    return BadRequest("Content e ContentType obrigatórios.");

                if (string.IsNullOrEmpty(instance.ResourceName))
                {
                    string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                    instance.ResourceName = "ci-" + timestamp;
                }

                string finalName = instance.ResourceName;

                string sql = @"INSERT INTO ContentInstance (Name, Content, ContentType, ParentContainerId) VALUES (@Name, @Content, @Type, @ParentId);
                               SELECT Id, Name, CreationDate, Content, ContentType, ParentContainerId FROM ContentInstance WHERE Id = SCOPE_IDENTITY();";

                ContentInstance createdInstance = null;

                bool inserted = ExecuteSqlWithRetry(sql, (cmd, name) => {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Content", instance.Content);
                    cmd.Parameters.AddWithValue("@Type", instance.ContentType);
                    cmd.Parameters.AddWithValue("@ParentId", contId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            createdInstance = new ContentInstance
                            {
                                ResourceName = (string)reader["Name"],
                                CreationDateTime = (DateTime)reader["CreationDate"],
                                Content = (string)reader["Content"],
                                ContentType = (string)reader["ContentType"],
                                ResType = "content-instance"
                            };
                        }
                    }
                }, ref finalName);

                if (!inserted) return InternalServerError();
                if (createdInstance == null) return InternalServerError();

                // [MQTT] Dispara notificação de Evento 1 (Criação) para o Broker
                NotifySubscribers((int)contId, appName, containerName, createdInstance.ResourceName, createdInstance.Content, 1);

                return Ok(createdInstance);
            }
            else if (resType == "subscription")
            {
                var sub = body.ToObject<Subscription>();
                if (sub.Evt != 1 && sub.Evt != 2) return BadRequest("EventId inválido (1=Create, 2=Delete).");
                if (string.IsNullOrEmpty(sub.Endpoint)) return BadRequest("Endpoint obrigatório.");

                if (string.IsNullOrEmpty(sub.ResourceName))
                {
                    string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                    sub.ResourceName = "sub-" + timestamp;
                }

                string finalName = sub.ResourceName;

                string sql = @"INSERT INTO Subscription (Name, EventId, Endpoint, ParentContainerId) VALUES (@Name, @Event, @Endpoint, @ParentId);
                               SELECT Id, Name, CreationDate, EventId, Endpoint, ParentContainerId FROM Subscription WHERE Id = SCOPE_IDENTITY();";

                Subscription createdSub = null;

                bool inserted = ExecuteSqlWithRetry(sql, (cmd, name) => {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Event", sub.Evt);
                    cmd.Parameters.AddWithValue("@Endpoint", sub.Endpoint);
                    cmd.Parameters.AddWithValue("@ParentId", contId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            createdSub = new Subscription
                            {
                                ResourceName = (string)reader["Name"],
                                CreationDateTime = (DateTime)reader["CreationDate"],
                                Evt = (int)reader["EventId"],
                                Endpoint = (string)reader["Endpoint"],
                                ResType = "subscription"
                            };
                        }
                    }
                }, ref finalName);

                if (!inserted) return InternalServerError();
                if (createdSub == null) return InternalServerError();

                return Ok(createdSub);
            }

            return BadRequest("Tipo de recurso desconhecido.");
        }

        // GET/DELETE ContentInstance
        [HttpDelete]
        [Route("{appName}/{containerName}/{resourceName}")]
        public IHttpActionResult DeleteContentInstance(string appName, string containerName, string resourceName)
        {
            // [MQTT] Precisamos do ID do contentor para buscar subscrições
            int? contId = GetContainerIdByName(appName, containerName);
            // Se contId for null, o DELETE em baixo falharia de qualquer forma ou daria 0 rows

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

            // [MQTT] Se apagou algo com sucesso, notifica Evento 2 (Deletion)
            if (rows > 0 && contId != null)
            {
                NotifySubscribers((int)contId, appName, containerName, resourceName, "DELETED", 2);
                return Ok($"ContentInstance '{resourceName}' apagada.");
            }

            return NotFound();
        }

        // GET ContentInstance
        [HttpGet]
        [Route("{appName}/{containerName}/{resourceName}")]
        public IHttpActionResult GetContentInstance(string appName, string containerName, string resourceName)
        {
            string sql = @"SELECT R.* FROM ContentInstance R
                           JOIN Container C ON R.ParentContainerId = C.Id
                           JOIN Application A ON C.ParentAppId = A.Id
                           WHERE A.Name = @AppName AND C.Name = @ContName AND R.Name = @ResName";

            ContentInstance ci = null;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@AppName", appName);
                    cmd.Parameters.AddWithValue("@ContName", containerName);
                    cmd.Parameters.AddWithValue("@ResName", resourceName);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ci = new ContentInstance
                            {
                                ResourceName = (string)reader["Name"],
                                CreationDateTime = (DateTime)reader["CreationDate"],
                                Content = (string)reader["Content"],
                                ContentType = (string)reader["ContentType"],
                                ResType = "content-instance"
                            };
                        }
                    }
                }
            }

            if (ci == null) return NotFound();
            return Ok(ci);
        }

        // =====================
        // SUBSCRIPTIONS (/subs)
        // =====================

        [HttpDelete]
        [Route("{appName}/{containerName}/subs/{subName}")]
        public IHttpActionResult DeleteSubscription(string appName, string containerName, string subName)
        {
            string sql = @"DELETE S FROM Subscription S
                           JOIN Container C ON S.ParentContainerId = C.Id
                           JOIN Application A ON C.ParentAppId = A.Id
                           WHERE A.Name = @AppName AND C.Name = @ContName AND S.Name = @SubName";

            int rows = 0;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@AppName", appName);
                    cmd.Parameters.AddWithValue("@ContName", containerName);
                    cmd.Parameters.AddWithValue("@SubName", subName);
                    rows = cmd.ExecuteNonQuery();
                }
            }

            if (rows == 0) return NotFound();
            return Ok($"Subscrição '{subName}' apagada.");
        }

        [HttpGet]
        [Route("{appName}/{containerName}/subs/{subName}")]
        public IHttpActionResult GetSubscription(string appName, string containerName, string subName)
        {
            string sql = @"SELECT S.* FROM Subscription S
                           JOIN Container C ON S.ParentContainerId = C.Id
                           JOIN Application A ON C.ParentAppId = A.Id
                           WHERE A.Name = @AppName AND C.Name = @ContName AND S.Name = @SubName";

            Subscription sub = null;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@AppName", appName);
                    cmd.Parameters.AddWithValue("@ContName", containerName);
                    cmd.Parameters.AddWithValue("@SubName", subName);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            sub = new Subscription
                            {
                                ResourceName = (string)reader["Name"],
                                CreationDateTime = (DateTime)reader["CreationDate"],
                                Evt = (int)reader["EventId"],
                                Endpoint = (string)reader["Endpoint"],
                                ResType = "subscription"
                            };
                        }
                    }
                }
            }

            if (sub == null) return NotFound();
            return Ok(sub);
        }

        // ==================
        // MÉTODOS AUXILIARES
        // ==================

        // Método genérico para Inserir com Retry e Timestamp
        private bool ExecuteSqlWithRetry(string sql, Action<SqlCommand, string> configureAndExecute, ref string finalName)
        {
            string originalName = finalName;
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        // Chama a ação que configura os parâmetros E executa o comando (ExecuteReader)
                        configureAndExecute(cmd, finalName);
                    }
                }
                return true;
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601)
                {
                    // Adiciona timestamp
                    string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                    finalName = $"{originalName}-{timestamp}";
                    try
                    {
                        using (SqlConnection connRetry = new SqlConnection(connectionString))
                        {
                            connRetry.Open();
                            using (SqlCommand cmdRetry = new SqlCommand(sql, connRetry))
                            {
                                configureAndExecute(cmdRetry, finalName);
                            }
                        }
                        return true;
                    }
                    catch { return false; }
                }
                return false;
            }
        }

        private bool IsDiscoveryRequest(string type)
        {
            if (Request.Headers.TryGetValues("somiod-discovery", out var values))
            {
                string headerVal = string.Join(",", values);
                return headerVal == type;
            }
            return false;
        }

        private string GetDiscoveryType()
        {
            if (Request.Headers.TryGetValues("somiod-discovery", out var values))
            {
                return string.Join(",", values);
            }
            return null;
        }

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

        // [MQTT] Responsável por encontrar subscritores e montar o XML de notificação
        private void NotifySubscribers(int containerId, string appName, string containerName, string resourceName, string contentVal, int eventType)
        {
            var endpoints = new List<string>();
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Usa DISTINCT para evitar mensagens duplicadas
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

            // Cria o XML de Notificação
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

        // [MQTT] Responsável pela conexão e publicação assíncrona no Broker
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