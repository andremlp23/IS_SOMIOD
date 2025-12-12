using RestSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System.Text;
using System.Threading;
using System.Xml.Linq; // Necessário para parsear notificação

namespace IS_SOMIOD
{
    public partial class Form1 : Form
    {
        // --- CONFIG ---
        private const string API_BASE = "http://localhost:62056/api/somiod";
        private const string APP_NAME = "smart-dock";

        // MQTT
        private IMqttClient mqttClient;
        private const string MQTT_BROKER = "127.0.0.1";
        private const int MQTT_PORT = 1883;
        private const string MQTT_TOPIC_SEMAFORO = "api/somiod/smart-dock/semaforo";

        // UI Arrays
        private Button[] zonaXSlots;
        private Button[] zonaYSlots;

        private readonly Color FreeColor = Color.LightGreen;
        private readonly Color OccupiedColor = Color.IndianRed;

        public Form1()
        {
            InitializeComponent();

            // Setup Buttons UI
            this.btnMais1.Click += btnMais1_Click;
            this.btnMenos1.Click += btnMenos1_Click;

            // Certifique-se que estes botões existem no Designer
            zonaXSlots = new[] { btn1, btn2, btn3, btn4 };
            zonaYSlots = new[] { btn5, btn6, btn7, btn8 };

            InitializeDefaultColors();

            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            InitializeDefaultColors();

            bool exists = await ApplicationExistsAsync(APP_NAME);

            if (!exists)
            {
                // Só cria se NÃO existir
                await BootstrapAsync();
            }

            // Em ambos os casos (existe ou acabou de criar)
            await StartMqttAsync();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (mqttClient != null && mqttClient.IsConnected)
                mqttClient.DisconnectAsync().GetAwaiter().GetResult();
        }

        private void InitializeDefaultColors()
        {
            foreach (var b in zonaXSlots.Concat(zonaYSlots))
            {
                b.UseVisualStyleBackColor = false;
                b.BackColor = FreeColor;
            }
            btnSemaforo.UseVisualStyleBackColor = false;
            btnSemaforo.BackColor = FreeColor;
        }

        private async Task<bool> ApplicationExistsAsync(string appName)
        {
            var client = new RestClient(API_BASE);
            var req = new RestRequest(appName, Method.Get);

            try
            {
                var resp = await client.ExecuteAsync(req);
                return resp.StatusCode == System.Net.HttpStatusCode.OK;
            }
            catch
            {
                return false;
            }
        }

        // ----------------------------------------------------
        // API BOOTSTRAP (Criação da estrutura)
        // ----------------------------------------------------
        private async Task BootstrapAsync()
        {
            await CreateResource("application", "", APP_NAME);
            await CreateResource("container", APP_NAME, "zona-x");
            await CreateResource("container", APP_NAME, "zona-y");
            await CreateResource("container", APP_NAME, "semaforo");

       
            // O endpoint deve apontar para o broker MQTT local
            await CreateSubscription("semaforo", "sub-app-main", $"mqtt://{MQTT_BROKER}:{MQTT_PORT}", 1);
        }

        // Helper genérico para criar recursos
        private async Task CreateResource(string type, string parentPath, string name, string subEndpoint = null, int evt = 1)
        {
            var client = new RestClient(API_BASE);
            // Define o path do POST (Parent)
            string resourcePath = type == "application" ? "" : parentPath;
            var req = new RestRequest(resourcePath, Method.Post);

            var body = new Dictionary<string, object>
            {
                { "res-type", type },
                { "resource-name", name }
            };

            if (type == "subscription")
            {
                body.Add("endpoint", subEndpoint);
                body.Add("evt", evt);
            }

            req.AddJsonBody(body);

            try
            {
                var resp = await client.ExecuteAsync(req);
                // 409 Conflict é aceitável (já existe)
                if (!resp.IsSuccessful && resp.StatusCode != System.Net.HttpStatusCode.Conflict)
                    System.Diagnostics.Debug.WriteLine($"Error creating {name}: {resp.Content}");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        private async Task CreateSubscription(string container, string name, string endpoint, int evt)
        {
            // O URL para criar subscription é .../App/Container
            await CreateResource("subscription", $"{APP_NAME}/{container}", name, endpoint, evt);
        }

        // ----------------------------------------------------
        // OPERAÇÕES LÓGICAS (Send Data / Delete)
        // ----------------------------------------------------
        private async Task<bool> SendDataAsync(string container, string content)
        {
            var client = new RestClient(API_BASE);
            var req = new RestRequest($"{APP_NAME}/{container}", Method.Post);

            var body = new Dictionary<string, object>
            {
                { "res-type", "content-instance" },
                { "resource-name", $"evt-{DateTime.Now.Ticks}" },
                { "content", content },
                { "content-type", "application/xml" }
            };
            req.AddJsonBody(body);

            var resp = await client.ExecuteAsync(req);
            return resp.IsSuccessful;
        }

        private async Task DeleteFromContainerAsync(string container, string resourceName)
        {
            var client = new RestClient(API_BASE);
            // URL: .../App/Container/ResourceName
            var req = new RestRequest($"{APP_NAME}/{container}/{resourceName}", Method.Delete);
            await client.ExecuteAsync(req);
        }

        // ----------------------------------------------------
        // MQTT HANDLER (Parsing XML)
        // ----------------------------------------------------
        private async Task StartMqttAsync()
        {
            var factory = new MqttFactory();
            mqttClient = factory.CreateMqttClient();
            var options = new MqttClientOptionsBuilder()
                .WithClientId("Client_App_" + Guid.NewGuid())
                .WithTcpServer(MQTT_BROKER, MQTT_PORT)
                .Build();

            mqttClient.UseConnectedHandler(async e =>
            {
                await mqttClient.SubscribeAsync(new MQTTnet.Client.Subscribing.MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(MQTT_TOPIC_SEMAFORO)
                    .Build());
            });

            mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                // Payload agora é XML: <Notification><Content>...</Content></Notification>

                try
                {
                    XDocument doc = XDocument.Parse(payload);
                    // Buscar o valor dentro de <Content>
                    string contentVal = doc.Root.Element("Content")?.Value;

                    // O conteúdo pode ser ele próprio um XML como <cmd>VERDE</cmd> ou texto simples
                    if (!string.IsNullOrEmpty(contentVal))
                    {
                        if (contentVal.Contains("VERDE"))
                        {
                            this.BeginInvoke(new Action(async () => {
                                SetSemaphoreColor(Color.LightGreen);
                                await MoveOneTruckAsync();
                            }));
                        }
                        else if (contentVal.Contains("VERMELHO"))
                        {
                            this.BeginInvoke(new Action(() => SetSemaphoreColor(Color.IndianRed)));
                        }
                    }
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("Erro ao parsear XML MQTT: " + payload);
                }
            });

            await mqttClient.ConnectAsync(options, CancellationToken.None);
        }

        // ----------------------------------------------------
        // LÓGICA DE MOVIMENTO
        // ----------------------------------------------------

        // Entrada Manual (Zona X)
        private async void btnMais1_Click(object sender, EventArgs e)
        {
            btnMais1.Enabled = false;
            try
            {
                var slot = zonaXSlots.FirstOrDefault(b => b.BackColor == FreeColor);
                if (slot == null) return;

                slot.BackColor = OccupiedColor;
                await SendDataAsync("zona-x", "<matricula>CAMIAO-IN</matricula>");

                // [CORREÇÃO] force: true (Porque fui eu que cliquei, quero que executes!)
                CheckAndUpdateSemaforo(force: true);
            }
            finally
            {
                btnMais1.Enabled = true;
            }
        }

        // Saída Manual (Zona Y)
        private async void btnMenos1_Click(object sender, EventArgs e)
        {
            var slot = zonaYSlots.FirstOrDefault(b => b.BackColor == OccupiedColor);
            if (slot == null) return; 

            slot.BackColor = FreeColor;

            await SendDataAsync("zona-y", "saiu");

            CheckAndUpdateSemaforo();
        }

        // Movimento Automático (X -> Y) quando Verde
        private async Task MoveOneTruckAsync()
        {
            var truckX = zonaXSlots.FirstOrDefault(b => b.BackColor == OccupiedColor);
            var freeY = zonaYSlots.FirstOrDefault(b => b.BackColor == FreeColor);

            if (truckX != null && freeY != null)
            {
                truckX.BackColor = FreeColor;
                await Task.Delay(500); // Animação
                freeY.BackColor = OccupiedColor;

                await SendDataAsync("zona-y", "<matricula>MOVIDO</matricula>");

                CheckAndUpdateSemaforo(force:false);
            }
        }

        private async void CheckAndUpdateSemaforo(bool force = false)
        {
            // 1. Calcula o estado desejado
            bool zonaYFull = zonaYSlots.All(b => b.BackColor == OccupiedColor);
            string novoComando = zonaYFull ? "<cmd>VERMELHO</cmd>" : "<cmd>VERDE</cmd>";

            // 2. Verifica o estado ATUAL visual
            bool isAtualmenteVerde = btnSemaforo.BackColor == Color.LightGreen;
            bool queroVerde = !zonaYFull;

            bool estadoMudou = (isAtualmenteVerde != queroVerde);

            if (force || estadoMudou)
            {
                await SendDataAsync("semaforo", novoComando);
            }
        }

        private void SetSemaphoreColor(Color c)
        {
            btnSemaforo.BackColor = c;
        }
    }
}