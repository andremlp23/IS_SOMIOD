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

        // Receber tudo
        private const string MQTT_TOPIC_ALL = "api/somiod/smart-dock/#";

        // UI Arrays
        private Button[] zonaXSlots;
        private Button[] zonaYSlots;

        private readonly Color FreeColor = Color.LightGreen;
        private readonly Color OccupiedColor = Color.IndianRed;

        public Form1()
        {
            InitializeComponent();

            this.btnMais1.Click += btnMais1_Click;
            this.btnMenos1.Click += btnMenos1_Click;

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
                await BootstrapAsync();
            }
            else
            {
                await EnsureSubscriptionsExist();
            }

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
                b.BackgroundImage = null; // LIMPA IMAGEM
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
        // API BOOTSTRAP
        // ----------------------------------------------------
        private async Task BootstrapAsync()
        {
            await CreateResource("application", "", APP_NAME);
            await CreateResource("container", APP_NAME, "zona-x");
            await CreateResource("container", APP_NAME, "zona-y");
            await CreateResource("container", APP_NAME, "semaforo");
            await CreateResource("container", APP_NAME, "luzes");

            await EnsureSubscriptionsExist();
        }

        private async Task EnsureSubscriptionsExist()
        {
            string endpoint = $"mqtt://{MQTT_BROKER}:{MQTT_PORT}";

            await CreateSubscription("semaforo", "sub-client1-semaforo", endpoint, 1);
            await CreateSubscription("zona-x", "sub-client1-zonax", endpoint, 1);
            await CreateSubscription("zona-y", "sub-client1-zonay", endpoint, 1);
            await CreateSubscription("luzes", "sub-client1-luzes", endpoint, 1);
        }

        private async Task CreateResource(string type, string parentPath, string name, string subEndpoint = null, int evt = 1)
        {
            var client = new RestClient(API_BASE);
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
            }
            catch { }
        }

        private async Task CreateSubscription(string container, string name, string endpoint, int evt)
        {
            await CreateResource("subscription", $"{APP_NAME}/{container}", name, endpoint, evt);
        }

        // ----------------------------------------------------
        // SEND DATA
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

        // ----------------------------------------------------
        // MQTT HANDLER
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
                    .WithTopicFilter(MQTT_TOPIC_ALL)
                    .Build());
            });

            mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                try
                {
                    XDocument doc = XDocument.Parse(payload);

                    string container = doc.Root.Element("Container")?.Value;
                    string content = doc.Root.Element("Content")?.Value;
                    string eventType = doc.Root.Element("Event")?.Value;

                    if (eventType != "Creation") return;

                    switch (container)
                    {
                        case "semaforo":
                            if (!string.IsNullOrEmpty(content))
                            {
                                this.BeginInvoke(new Action(async () =>
                                {
                                    if (content.Contains("VERDE"))
                                    {
                                        SetSemaphoreColor(Color.LightGreen);
                                        await MoveOneTruckAsync();
                                    }
                                    else if (content.Contains("VERMELHO"))
                                    {
                                        SetSemaphoreColor(Color.IndianRed);
                                    }
                                }));
                            }
                            break;

                        case "luzes":
                            if (!string.IsNullOrEmpty(content))
                            {
                                this.BeginInvoke(new Action(() =>
                                {
                                    if (content.Contains("OFF"))
                                    {
                                        this.BackColor = Color.FromArgb(40, 40, 40);
                                        this.ForeColor = Color.White;
                                    }
                                    else if (content.Contains("ON"))
                                    {
                                        this.BackColor = SystemColors.Control;
                                        this.ForeColor = SystemColors.ControlText;
                                    }
                                }));
                            }
                            break;
                    }

                }
                catch { }
            });

            await mqttClient.ConnectAsync(options, CancellationToken.None);
        }

        // ----------------------------------------------------
        // MOVIMENTO
        // ----------------------------------------------------
        private async void btnMais1_Click(object sender, EventArgs e)
        {
            btnMais1.Enabled = false;
            try
            {
                var slot = zonaXSlots.FirstOrDefault(b => b.BackColor == FreeColor);
                if (slot == null) return;

                slot.BackColor = OccupiedColor;
                slot.BackgroundImage = Properties.Resources.box_truck;
                slot.BackgroundImageLayout = ImageLayout.Zoom;

                await SendDataAsync("zona-x", "<matricula>CAMIAO-IN</matricula>");
                CheckAndUpdateSemaforo(force: true);
            }
            finally
            {
                btnMais1.Enabled = true;
            }
        }

        private async void btnMenos1_Click(object sender, EventArgs e)
        {
            var slot = zonaYSlots.FirstOrDefault(b => b.BackColor == OccupiedColor);
            if (slot == null) return;

            slot.BackColor = FreeColor;
            slot.BackgroundImage = null;

            await SendDataAsync("zona-y", "saiu");
            CheckAndUpdateSemaforo();
        }

        private async Task MoveOneTruckAsync()
        {
            var truckX = zonaXSlots.FirstOrDefault(b => b.BackColor == OccupiedColor);
            var freeY = zonaYSlots.FirstOrDefault(b => b.BackColor == FreeColor);

            if (truckX != null && freeY != null)
            {
                truckX.BackColor = FreeColor;
                truckX.BackgroundImage = null;

                await Task.Delay(500);

                freeY.BackColor = OccupiedColor;
                freeY.BackgroundImage = Properties.Resources.box_truck;
                freeY.BackgroundImageLayout = ImageLayout.Zoom;

                await SendDataAsync("zona-y", "<matricula>MOVIDO</matricula>");
                CheckAndUpdateSemaforo(force: false);
            }
        }

        private async void CheckAndUpdateSemaforo(bool force = false)
        {
            bool zonaYFull = zonaYSlots.All(b => b.BackColor == OccupiedColor);
            string novoComando = zonaYFull ? "<cmd>VERMELHO</cmd>" : "<cmd>VERDE</cmd>";

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

        private void btnMais1_Click_1(object sender, EventArgs e)
        {

        }
    }
}
