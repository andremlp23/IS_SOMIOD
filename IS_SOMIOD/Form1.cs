using RestSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SOMIOD_Common; // Certifica-te que tens a referência adicionada ao projeto

namespace IS_SOMIOD
{
    public partial class Form1 : Form
    {
        // Helper da biblioteca comum
        private SomiodMqttHelper _mqttHelper;

        private const string API_BASE = "http://localhost:62056/api/somiod";
        private const string APP_NAME = "smart-dock";

        // Tópicos específicos (ou Wildcard se preferires receber tudo)
        private const string MQTT_TOPIC_ALL = "api/somiod/smart-dock/#";

        // UI Arrays
        private Button[] zonaXSlots;
        private Button[] zonaYSlots;
        private readonly Color FreeColor = Color.LightGreen;
        private readonly Color OccupiedColor = Color.IndianRed;

        // Estado local (opcional, mas útil para manter sincronia)
        private int _countZonaX = 0;
        private int _countZonaY = 0;

        public Form1()
        {
            InitializeComponent();

            // Instancia o Helper da DLL partilhada
            _mqttHelper = new SomiodMqttHelper("127.0.0.1", 1883);

            // Liga eventos de botão (caso não estejam no designer)
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

            // 1. Garante que a estrutura na API existe
            bool exists = await ApplicationExistsAsync(APP_NAME);
            if (!exists)
                await BootstrapAsync();
            else
                await EnsureSubscriptionsExist();

            // 2. Configura e conecta o MQTT
            _mqttHelper.OnMessageReceived += HandleMqttMessage;

            try
            {
                await _mqttHelper.ConnectAsync("Controller");
                await _mqttHelper.SubscribeAsync(MQTT_TOPIC_ALL);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao conectar MQTT: " + ex.Message);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _mqttHelper.DisconnectAsync();
        }

        // ==========================================================
        //  LÓGICA MQTT (Receção)
        // ==========================================================
        private void HandleMqttMessage(string topic, string container, string content, string eventType)
        {
            if (eventType != "Creation") return;

            this.BeginInvoke(new Action(() =>
            {
                switch (container)
                {
                    case "semaforo":
                        if (!string.IsNullOrEmpty(content))
                        {
                            if (content.Contains("VERDE"))
                            {
                                SetSemaphoreColor(Color.LightGreen);
                                // Dispara movimento automático
                                Task.Run(() => MoveOneTruckAsync());
                            }
                            else if (content.Contains("VERMELHO"))
                            {
                                SetSemaphoreColor(Color.IndianRed);
                            }
                        }
                        break;

                    case "luzes":
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
                        break;

                    case "zona-x":
                        if (content.Contains("CAMIAO-IN")) _countZonaX++;
                        break;

                    case "zona-y":
                        if (content.Contains("MOVIDO")) { _countZonaX--; _countZonaY++; }
                        else if (content.Contains("saiu")) _countZonaY--;
                        break;
                }
            }));
        }

        // ==========================================================
        //  LÓGICA DE NEGÓCIO (Movimento & API)
        // ==========================================================

        private async void btnMais1_Click(object sender, EventArgs e)
        {
            btnMais1.Enabled = false;
            try
            {
                var slot = zonaXSlots.FirstOrDefault(b => b.BackColor == FreeColor);
                if (slot == null) return; // Cheio

                slot.BackColor = OccupiedColor;
                slot.BackgroundImage = Properties.Resources.box_truck; // Certifica-te que tens a imagem nos Resources
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
            if (slot == null) return; // Vazio

            slot.BackColor = FreeColor;
            slot.BackgroundImage = null;

            await SendDataAsync("zona-y", "saiu");
            CheckAndUpdateSemaforo();
        }

        private async Task MoveOneTruckAsync()
        {
            // Executar na UI thread se chamado de Task.Run
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(async () => await MoveOneTruckAsync()));
                return;
            }

            var truckX = zonaXSlots.FirstOrDefault(b => b.BackColor == OccupiedColor);
            var freeY = zonaYSlots.FirstOrDefault(b => b.BackColor == FreeColor);

            if (truckX != null && freeY != null)
            {
                // Animação visual
                truckX.BackColor = FreeColor;
                truckX.BackgroundImage = null;

                await Task.Delay(500);

                freeY.BackColor = OccupiedColor;
                freeY.BackgroundImage = Properties.Resources.box_truck;
                freeY.BackgroundImageLayout = ImageLayout.Zoom;

                // Notificar API
                await SendDataAsync("zona-y", "<matricula>MOVIDO</matricula>");

                // Verificar estado (sem forçar, para evitar loops)
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

            try
            {
                var resp = await client.ExecuteAsync(req);
                return resp.IsSuccessful;
            }
            catch { return false; }
        }

        // ==========================================================
        //  BOOTSTRAP & HELPERS
        // ==========================================================

        private void InitializeDefaultColors()
        {
            foreach (var b in zonaXSlots.Concat(zonaYSlots))
            {
                b.UseVisualStyleBackColor = false;
                b.BackColor = FreeColor;
                b.BackgroundImage = null;
            }
            btnSemaforo.UseVisualStyleBackColor = false;
            btnSemaforo.BackColor = FreeColor;
        }

        private void SetSemaphoreColor(Color c)
        {
            btnSemaforo.BackColor = c;
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
            catch { return false; }
        }

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
            string endpoint = $"mqtt://127.0.0.1:1883";
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
            try { await client.ExecuteAsync(req); } catch { }
        }

        private async Task CreateSubscription(string container, string name, string endpoint, int evt)
        {
            await CreateResource("subscription", $"{APP_NAME}/{container}", name, endpoint, evt);
        }

        // Eventos vazios
        private void btnMais1_Click_1(object sender, EventArgs e) { }
    }
}