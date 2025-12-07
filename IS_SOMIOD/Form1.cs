// Adicionar estas referências:
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

namespace IS_SOMIOD
{
    public partial class Form1 : Form
    {
        // --- CONFIGURAÇÕES DA API SOMIOD ---
        private const string API_BASE = "http://localhost:62056/api/somiod";
        private const string APP_NAME = "smart-dock";

        // MQTT - CORRIGIDO PARA O FORMATO DO CONTROLLER
        private IMqttClient mqttClient;
        private const string MQTT_BROKER = "127.0.0.1";
        private const int MQTT_PORT = 1883;

        // Tópico correto do Controller corrigido:
        // api/somiod/{app}/{container}/{subscription}
        private const string MQTT_TOPIC = "api/somiod/smart-dock/semaforo/sub-hardware";

        // UI slot groups
        private Button[] zonaXSlots;
        private Button[] zonaYSlots;

        // Colors
        private readonly Color FreeColor = Color.LightGreen;
        private readonly Color OccupiedColor = Color.IndianRed;

        public Form1()
        {
            InitializeComponent();

            // Eventos UI
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
            await BootstrapAsync();
            await StartMqttAsync();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                mqttClient?.DisconnectAsync().GetAwaiter().GetResult();
            }
            catch { }
        }

        private void InitializeDefaultColors()
        {
            foreach (var b in zonaXSlots.Concat(zonaYSlots))
            {
                b.UseVisualStyleBackColor = false;
                b.BackColor = FreeColor;
            }

            btnSemaforo.BackColor = FreeColor;
        }

        private bool IsSuccessOrExists(RestResponse resp)
        {
            if (resp.IsSuccessful) return true;

            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict ||
                resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
                return true;

            return false;
        }

        // ============================================================
        // BOOTSTRAP
        // ============================================================
        private async Task BootstrapAsync()
        {
            await CreateApplicationAsync();
            await CreateContainerAsync("zona-x");
            await CreateContainerAsync("zona-y");
            await CreateContainerAsync("semaforo");

            // Subscription corrigida
            await CreateSubscriptionAsync("semaforo", "sub-hardware", "mqtt://localhost:1883", 1);
        }

        private async Task CreateApplicationAsync()
        {
            var client = new RestClient(API_BASE);
            var req = new RestRequest("", Method.Post);

            var body = new Dictionary<string, object>
            {
                { "res-type", "application" },
                { "resource-name", APP_NAME }
            };

            req.AddJsonBody(body);

            var resp = await client.ExecuteAsync(req);

            if (IsSuccessOrExists(resp))
                return;

            System.Diagnostics.Debug.WriteLine("ERRO criar app: " + resp.StatusCode);
        }

        private async Task CreateContainerAsync(string name)
        {
            var client = new RestClient(API_BASE);
            var req = new RestRequest(APP_NAME, Method.Post);

            req.AddJsonBody(new Dictionary<string, object>
            {
                { "res-type", "container" },
                { "resource-name", name }
            });

            var resp = await client.ExecuteAsync(req);

            if (!IsSuccessOrExists(resp))
                System.Diagnostics.Debug.WriteLine("ERRO criar container: " + name);
        }

        private async Task CreateSubscriptionAsync(string container, string subName, string endpoint, int evt)
        {
            var client = new RestClient(API_BASE);
            var req = new RestRequest($"{APP_NAME}/{container}", Method.Post);

            req.AddJsonBody(new Dictionary<string, object>
            {
                { "res-type", "subscription" },
                { "resource-name", subName },
                { "endpoint", endpoint },
                { "evt", evt.ToString() }
            });

            var resp = await client.ExecuteAsync(req);

            if (!IsSuccessOrExists(resp))
                System.Diagnostics.Debug.WriteLine("ERRO criar subscription.");
        }

        // ============================================================
        // SEND & DELETE
        // ============================================================
        private async Task<bool> SendDataAsync(string container, string content)
        {
            try
            {
                var client = new RestClient(API_BASE);
                var req = new RestRequest($"{APP_NAME}/{container}", Method.Post);

                req.AddJsonBody(new Dictionary<string, object>
                {
                    { "res-type", "content-instance" },
                    { "resource-name", $"evt-{DateTime.Now.Ticks}" },
                    { "content", content },

                    // CORRIGIDO para o controller:
                    { "content_type", "text/plain" }
                });

                var resp = await client.ExecuteAsync(req);
                return resp.IsSuccessful;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> DeleteFromContainerAsync(string container, string id = null)
        {
            var client = new RestClient(API_BASE);
            var req = new RestRequest($"{APP_NAME}/{container}", Method.Delete);

            var resp = await client.ExecuteAsync(req);

            if (resp.IsSuccessful)
                return true;

            // fallback
            return await SendDataAsync(container, "saiu");
        }

        // ============================================================
        // UI EVENTS
        // ============================================================
        private async void btnMais1_Click(object sender, EventArgs e)
        {
            var free = zonaXSlots.FirstOrDefault(b => b.BackColor == FreeColor);
            if (free == null)
            {
                MessageBox.Show("Zona X cheia!");
                return;
            }

            free.BackColor = OccupiedColor;

            await SendDataAsync("zona-x", "<matricula>CAMIAO-01</matricula>");
            UpdateSemaphoreStateBySlots();
        }

        private async void btnMenos1_Click(object sender, EventArgs e)
        {
            var occ = zonaYSlots.FirstOrDefault(b => b.BackColor == OccupiedColor);
            if (occ == null)
            {
                MessageBox.Show("Nenhum camião em zona Y");
                return;
            }

            occ.BackColor = FreeColor;

            await DeleteFromContainerAsync("zona-y", occ.Name);
            UpdateSemaphoreStateBySlots();
        }

        // ============================================================
        // MQTT
        // ============================================================
        private async Task StartMqttAsync()
        {
            try
            {
                var factory = new MqttFactory();
                mqttClient = factory.CreateMqttClient();

                var options = new MqttClientOptionsBuilder()
                    .WithClientId("Simu_" + Guid.NewGuid().ToString("N").Substring(0, 6))
                    .WithTcpServer(MQTT_BROKER, MQTT_PORT)
                    .WithCleanSession()
                    .Build();

                mqttClient.UseConnectedHandler(async _ =>
                {
                    System.Diagnostics.Debug.WriteLine("MQTT connected.");
                    await mqttClient.SubscribeAsync(MQTT_TOPIC);
                });

                mqttClient.UseApplicationMessageReceivedHandler(e =>
                {
                    var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload ?? new byte[0]);
                    var msg = payload.Trim().ToUpperInvariant();

                    this.BeginInvoke(new Action(async () =>
                    {
                        if (msg.Contains("VERDE"))
                        {
                            SetSemaphoreColorGreen();
                            await MoveOneTruckFromXToYAsync();
                        }
                        else if (msg.Contains("VERMELHO"))
                        {
                            SetSemaphoreColorRed();
                        }
                    }));
                });

                await mqttClient.ConnectAsync(options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MQTT ERROR: " + ex.Message);
            }
        }

        // ============================================================
        // MOVE TRUCK X → Y
        // ============================================================
        private async Task MoveOneTruckFromXToYAsync()
        {
            var occX = zonaXSlots.FirstOrDefault(b => b.BackColor == OccupiedColor);
            var freeY = zonaYSlots.FirstOrDefault(b => b.BackColor == FreeColor);

            if (occX == null || freeY == null)
            {
                SetSemaphoreColorRed();
                return;
            }

            occX.BackColor = FreeColor;
            await DeleteFromContainerAsync("zona-x", occX.Name);

            await Task.Delay(300);

            freeY.BackColor = OccupiedColor;
            await SendDataAsync("zona-y", "<matricula>CAMIAO-01</matricula>");

            UpdateSemaphoreStateBySlots();
        }

        // ============================================================
        // SEMAFORO
        // ============================================================
        private void SetSemaphoreColorGreen()
        {
            btnSemaforo.BackColor = Color.LightGreen;
        }

        private void SetSemaphoreColorRed()
        {
            btnSemaforo.BackColor = Color.IndianRed;
        }

        private async void UpdateSemaphoreStateBySlots()
        {
            bool fullY = zonaYSlots.All(b => b.BackColor == OccupiedColor);

            if (fullY)
            {
                SetSemaphoreColorRed();
                await SendDataAsync("semaforo", "<cmd>VERMELHO</cmd>");
            }
            else
            {
                SetSemaphoreColorGreen();
                await SendDataAsync("semaforo", "<cmd>VERDE</cmd>");
            }
        }
    }
}
