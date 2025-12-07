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
        private const string API_BASE = "http://localhost:62056/api/somiod"; // adjust if needed
        private const string APP_NAME = "smart-dock"; // requested application name

        // MQTT
        private IMqttClient mqttClient;
        private const string MQTT_BROKER = "localhost";
        private const int MQTT_PORT = 1883;
        private const string MQTT_TOPIC = "api/somiod/smart-dock/semaforo";

        // UI slot groups
        private Button[] zonaXSlots; // btn1..btn4
        private Button[] zonaYSlots; // btn5..btn8

        // Color conventions
        private readonly Color FreeColor = Color.LightGreen;
        private readonly Color OccupiedColor = Color.IndianRed;
        private readonly Color HighlightColor = Color.Yellow;

        public Form1()
        {
            InitializeComponent();

            // Wire button clicks (ensure these controls exist in Designer)
            this.btnMais1.Click += btnMais1_Click;
            this.btnMenos1.Click += btnMenos1_Click;

            // Assign arrays for convenience
            zonaXSlots = new[] { btn1, btn2, btn3, btn4 };
            zonaYSlots = new[] { btn5, btn6, btn7, btn8 };

            InitializeDefaultColors();

            // Form load will start bootstrap and MQTT
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            // Bootstrap API resources then start MQTT
            await BootstrapAsync();
            await StartMqttAsync();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (mqttClient != null && mqttClient.IsConnected)
                {
                    mqttClient.DisconnectAsync().GetAwaiter().GetResult();
                }
            }
            catch { /* ignore */ }
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

            btnMenos1.UseVisualStyleBackColor = true;
            btnMenos1.BackColor = SystemColors.Control;
        }

        // -----------------------
        // API: Bootstrap helpers
        // -----------------------
        private async Task BootstrapAsync()
        {
            try
            {
                await CreateApplicationAsync();
                await CreateContainerAsync("zona-x");
                await CreateContainerAsync("zona-y");
                await CreateContainerAsync("semaforo");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Bootstrap exception: " + ex.Message);
            }
        }

        private async Task CreateApplicationAsync()
        {
            var client = new RestClient(API_BASE);
            var req = new RestRequest("", Method.Post);

            // API expects these fields (adjust names if your middleware expects different keys)
            var body = new
            {
                res_type = "application",
                resource_name = APP_NAME
            };

            req.AddJsonBody(body);

            RestResponse resp = null;
            try
            {
                resp = await client.ExecuteAsync(req);
                System.Diagnostics.Debug.WriteLine($"CreateApplication: Success={resp.IsSuccessful}, Status={(int)resp.StatusCode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CreateApplication error: " + ex.Message);
            }
        }

        private async Task CreateContainerAsync(string containerName)
        {
            var client = new RestClient(API_BASE);
            var req = new RestRequest(APP_NAME, Method.Post);

            var body = new
            {
                res_type = "container",
                resource_name = containerName
            };

            req.AddJsonBody(body);

            RestResponse resp = null;
            try
            {
                resp = await client.ExecuteAsync(req);
                System.Diagnostics.Debug.WriteLine($"CreateContainer('{containerName}'): Success={resp.IsSuccessful}, Status={(int)resp.StatusCode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateContainer('{containerName}') error: " + ex.Message);
            }
        }

        // -----------------------
        // API: send / delete helpers
        // -----------------------
        private async Task<bool> SendDataAsync(string container, string content)
        {
            try
            {
                var client = new RestClient(API_BASE);
                var req = new RestRequest($"{APP_NAME}/{container}", Method.Post);

                var body = new
                {
                    res_type = "content-instance",
                    resource_name = $"evt-{DateTime.Now.Ticks}",
                    content = content,
                    content_type = "text/plain"
                };

                req.AddJsonBody(body);

                var resp = await client.ExecuteAsync(req);
                System.Diagnostics.Debug.WriteLine($"SendData '{container}' content='{content}': Success={resp.IsSuccessful}, Status={(int)resp.StatusCode}");
                return resp.IsSuccessful;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SendDataAsync('{container}') error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Attempts to delete from container using HTTP DELETE. If not supported by middleware falls back to posting a "saida" event.
        /// </summary>
        private async Task<bool> DeleteFromContainerAsync(string container, string optionalIdentifier = null)
        {
            try
            {
                var client = new RestClient(API_BASE);
                // Try a straight DELETE to the container - middleware might accept it
                var req = new RestRequest($"{APP_NAME}/{container}", Method.Delete);
                var resp = await client.ExecuteAsync(req);
                if (resp.IsSuccessful)
                {
                    System.Diagnostics.Debug.WriteLine($"DeleteFromContainer '{container}': Success DELETE.");
                    return true;
                }

                // fallback: POST a "saiu" content-instance
                var fallbackContent = optionalIdentifier != null ? $"saiu-{optionalIdentifier}" : "saiu";
                System.Diagnostics.Debug.WriteLine($"DeleteFromContainer fallback posting 'saiu' to '{container}' because DELETE returned {(int)resp.StatusCode}");
                return await SendDataAsync(container, fallbackContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteFromContainerAsync('{container}') error: " + ex.Message);
                // fallback to posting a "saiu" event
                return await SendDataAsync(container, "saiu");
            }
        }

                            // -----------------------
        // UI events: Entrada / Saída
        // -----------------------
        // Entrada (E) -> zona-x
        private async void btnMais1_Click(object sender, EventArgs e)
        {
            // Visual: occupy first free spot in zona-x
            var firstFree = zonaXSlots.FirstOrDefault(b => b.BackColor == FreeColor);
            if (firstFree == null)
            {
                MessageBox.Show("Zona X cheia!");
                return;
            }

            // Mark occupied
            firstFree.BackColor = OccupiedColor;

            // Send arrival to middleware
            string matriculaXml = "<matricula>CAMIAO-01</matricula>";
            await SendDataAsync("zona-x", matriculaXml);

            // If zone-x now full, optionally notify semaforo (not required but consistent)
            UpdateSemaphoreStateBySlots();
        }

        // Saída (SA) -> zona-y (user triggered departure: remove one from zona-y)
        private async void btnMenos1_Click(object sender, EventArgs e)
        {
            // Find first occupied in zona-y to remove
            var occupied = zonaYSlots.FirstOrDefault(b => b.BackColor == OccupiedColor);
            if (occupied == null)
            {
                MessageBox.Show("Nenhum camião em zona Y para sair.");
                return;
            }

            // Free the slot visually
            occupied.BackColor = FreeColor;

            // Notify middleware: try DELETE then fallback
            await DeleteFromContainerAsync("zona-y", occupied.Name);

            // Update semaforo (if manual departure frees space)
            UpdateSemaphoreStateBySlots();
        }

        // -----------------------
        // MQTT: connect & handler
        // -----------------------
        private async Task StartMqttAsync()
        {
            try
            {
                var factory = new MqttFactory();
                mqttClient = factory.CreateMqttClient();

                var options = new MqttClientOptionsBuilder()
                    .WithClientId("IS_SOMIOD_Simulator_" + Guid.NewGuid().ToString("N").Substring(0, 8))
                    .WithTcpServer(MQTT_BROKER, MQTT_PORT)
                    .WithCleanSession()
                    .Build();

                mqttClient.UseConnectedHandler(async e =>
                {
                    System.Diagnostics.Debug.WriteLine("MQTT connected.");
                    await mqttClient.SubscribeAsync(new MQTTnet.Client.Subscribing.MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter(MQTT_TOPIC)
                        .Build());
                    System.Diagnostics.Debug.WriteLine($"Subscribed to topic {MQTT_TOPIC}");
                });

                mqttClient.UseDisconnectedHandler(e =>
                {
                    System.Diagnostics.Debug.WriteLine("MQTT disconnected.");
                });

                mqttClient.UseApplicationMessageReceivedHandler(e =>
                {
                    try
                    {
                        var payload = e.ApplicationMessage?.Payload == null ? string.Empty : Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                        var msg = payload?.Trim().ToUpperInvariant();
                        System.Diagnostics.Debug.WriteLine($"MQTT message received on '{e.ApplicationMessage.Topic}': {payload}");

                        if (msg == "VERDE")
                        {
                            // Update UI and perform movement: run on UI thread
                            this.BeginInvoke(new Action(async () =>
                            {
                                SetSemaphoreColorGreen();
                                await MoveOneTruckFromXToYAsync();
                            }));
                        }
                        else if (msg == "VERMELHO")
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                                SetSemaphoreColorRed();
                            }));
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("MQTT message not recognized for semaforo: " + payload);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Error handling MQTT message: " + ex.Message);
                    }
                });

                await mqttClient.ConnectAsync(options, CancellationToken.None);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("StartMqttAsync error: " + ex.Message);
            }
        }

        // -----------------------
        // Movement logic called on MQTT VERDE
        // -----------------------
        private async Task MoveOneTruckFromXToYAsync()
        {
            try
            {
                // Find first occupied in zona-x
                var occupiedX = zonaXSlots.FirstOrDefault(b => b.BackColor == OccupiedColor);
                if (occupiedX == null)
                {
                    System.Diagnostics.Debug.WriteLine("MoveOne: no truck in zona-x to move.");
                    return;
                }

                // Find first free in zona-y
                var freeY = zonaYSlots.FirstOrDefault(b => b.BackColor == FreeColor);
                if (freeY == null)
                {
                    System.Diagnostics.Debug.WriteLine("MoveOne: no free slot in zona-y to move into.");
                    // If no space in Y, we might set semaforo to red to block further movement
                    SetSemaphoreColorRed();
                    await SendDataAsync("semaforo", "<cmd>VERMELHO</cmd>");
                    return;
                }

                // 1) Remove from zona-x -> notify middleware using DELETE/fallback
                var identifier = occupiedX.Name;
                occupiedX.BackColor = FreeColor;
                await DeleteFromContainerAsync("zona-x", identifier);

                // small visual delay to simulate movement
                await Task.Delay(400);

                // 2) Add to zona-y -> notify middleware via POST with matricula xml
                freeY.BackColor = OccupiedColor;
                await SendDataAsync("zona-y", "<matricula>CAMIAO-01</matricula>");

                // 3) Update semaforo state: if zona-y full => vermelho else verde
                UpdateSemaphoreStateBySlots();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MoveOneTruckFromXToYAsync error: " + ex.Message);
            }
        }

        // -----------------------
        // Semaphore UI helpers
        // -----------------------
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
            try
            {
                // If zona-y is full -> semaforo red, publish to API; otherwise green.
                bool zonaYFull = zonaYSlots.All(b => b.BackColor == OccupiedColor);

                if (zonaYFull)
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateSemaphoreStateBySlots error: " + ex.Message);
            }
        }

        private void bindingNavigatorAddNewItem_Click(object sender, EventArgs e)
        {

        }
    }
}