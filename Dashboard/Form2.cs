using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace IS_SOMIOD
{
    public partial class Form2 : Form
    {
        // --- CONFIGURAÇÕES MQTT ---
        private IMqttClient _mqttClient;
        private const string BROKER_IP = "127.0.0.1";
        private const int BROKER_PORT = 1883;

        private const string API_BASE = "http://localhost:62056/";
        private const string APP_NAME = "smart-dock";

        // [ALTERAÇÃO] Tópicos Específicos (Sem Wildcard)
        private const string TOPIC_SEMAFORO = "api/somiod/smart-dock/semaforo";
        private const string TOPIC_ZONA_X = "api/somiod/smart-dock/zona-x";
        private const string TOPIC_ZONA_Y = "api/somiod/smart-dock/zona-y";
        private const string TOPIC_LUZES = "api/somiod/smart-dock/luzes";

        // Estado local das luzes
        private bool _lightsOn = true;

        // --- ESTADO DO SISTEMA (Contadores Locais) ---
        private int _countZonaX = 0; // Camiões na descarga
        private int _countZonaY = 0; // Camiões à espera/saída

        public Form2()
        {
            InitializeComponent();
        }

        private async void Form2_Load(object sender, EventArgs e)
        {
            // Inicializa UI
            UpdateLabels();
            lblSemaforo.BackColor = Color.Gray;

            // Inicia MQTT
            await StartMqttListener();
        }

        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_mqttClient != null && _mqttClient.IsConnected)
            {
                _mqttClient.DisconnectAsync();
            }
        }

        // ---------------------------------------------------------
        // MQTT LOGIC
        // ---------------------------------------------------------
        private async Task StartMqttListener()
        {
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(BROKER_IP, BROKER_PORT)
                .WithClientId("Dashboard_Viewer_" + Guid.NewGuid())
                .Build();

            // Handler de Conexão
            _mqttClient.UseConnectedHandler(async e =>
            {
                // Log inicial nas duas caixas
                InvokeLog(ParkTextBox, "Conectado ao Broker MQTT.");
                InvokeLog(LuzesTextBox, "Conectado ao Broker MQTT.");

                // [ALTERAÇÃO] Subscrever Tópicos Específicos

                // 1. Tráfego
                await _mqttClient.SubscribeAsync(new MQTTnet.Client.Subscribing.MqttClientSubscribeOptionsBuilder().WithTopicFilter(TOPIC_SEMAFORO).Build());
                await _mqttClient.SubscribeAsync(new MQTTnet.Client.Subscribing.MqttClientSubscribeOptionsBuilder().WithTopicFilter(TOPIC_ZONA_X).Build());
                await _mqttClient.SubscribeAsync(new MQTTnet.Client.Subscribing.MqttClientSubscribeOptionsBuilder().WithTopicFilter(TOPIC_ZONA_Y).Build());

                // 2. Luzes
                await _mqttClient.SubscribeAsync(new MQTTnet.Client.Subscribing.MqttClientSubscribeOptionsBuilder().WithTopicFilter(TOPIC_LUZES).Build());

                InvokeLog(ParkTextBox, "Subscrito: Zonas & Semáforo");
                InvokeLog(LuzesTextBox, "Subscrito: Luzes");
            });

            // Handler de Mensagens Recebidas
            _mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                string topic = e.ApplicationMessage.Topic;

                // Passamos o tópico para decidir em que caixa escrever
                ProcessNotification(topic, payload);
            });

            // Tentar conectar
            try
            {
                await _mqttClient.ConnectAsync(options, CancellationToken.None);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao ligar ao MQTT: " + ex.Message);
            }
        }

        // ---------------------------------------------------------
        // LÓGICA DE NEGÓCIO (Parsing XML e Routing)
        // ---------------------------------------------------------
        private void ProcessNotification(string topic, string xmlPayload)
        {
            try
            {
                XDocument doc = XDocument.Parse(xmlPayload);

                string container = doc.Root.Element("Container")?.Value;
                string content = doc.Root.Element("Content")?.Value;
                string eventType = doc.Root.Element("Event")?.Value;

                if (eventType != "Creation") return;

                // Atualizar UI na Thread Principal
                this.BeginInvoke(new Action(() =>
                {
                    // [ALTERAÇÃO] Decisão de Roteamento baseada no Tópico
                    if (topic == TOPIC_LUZES)
                    {
                        // Vai para a caixa da direita (Luzes)
                        InvokeLog(LuzesTextBox, $"CMD: {content}");
                    }
                    else
                    {
                        // Vai para a caixa da esquerda (Camiões)
                        InvokeLog(ParkTextBox, $"[{container}] {content}");

                        // Executa lógica de contagem apenas se for tráfego
                        ProcessTrafficLogic(container, content);
                    }
                }));
            }
            catch (Exception ex)
            {
                InvokeLog(ParkTextBox, "Erro XML: " + ex.Message);
            }
        }

        private void ProcessTrafficLogic(string container, string content)
        {
            switch (container)
            {
                case "semaforo":
                    if (content.Contains("VERDE"))
                        lblSemaforo.BackColor = Color.LightGreen;
                    else if (content.Contains("VERMELHO"))
                        lblSemaforo.BackColor = Color.IndianRed;
                    break;

                case "zona-x":
                    if (content.Contains("CAMIAO-IN")) _countZonaX++;
                    break;

                case "zona-y":
                    if (content.Contains("MOVIDO"))
                    {
                        _countZonaX--;
                        _countZonaY++;
                    }
                    else if (content.Contains("saiu") || content.Contains("SAIU"))
                    {
                        _countZonaY--;
                    }
                    break;
            }

            if (_countZonaX < 0) _countZonaX = 0;
            if (_countZonaY < 0) _countZonaY = 0;

            UpdateLabels();
        }

        // ---------------------------------------------------------
        // UI HELPERS
        // ---------------------------------------------------------
        private void UpdateLabels()
        {
            lblVagasX.Text = $"Zona X (Espera): {_countZonaX} em espera";
            lblEsperaY.Text = $"Zona Y (Descarga): {_countZonaY} ocupados";
        }

        // [ALTERAÇÃO] Agora aceita o 'target' para saber em que caixa escrever
        private void InvokeLog(RichTextBox target, string msg)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<RichTextBox, string>(InvokeLog), target, msg);
            }
            else
            {
                target.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
                // Scroll to bottom
                target.SelectionStart = target.Text.Length;
                target.ScrollToCaret();
            }
        }

        // ---------------------------------------------------------
        // BOTÕES / API CALLS
        // ---------------------------------------------------------
        private async void btnLuzes_Click(object sender, EventArgs e)
        {
            _lightsOn = !_lightsOn;
            string command = _lightsOn ? "ON" : "OFF";

            // Feedback visual no botão
            // Se tiveres um botão chamado btnLuzes no designer, descomenta:
            // btnLuzes.Text = _lightsOn ? "Desligar Luzes" : "Ligar Luzes";

            string contentXml = $"<cmd>{command}</cmd>";
            bool sucesso = await SendCommandToApi("luzes", contentXml);

            if (sucesso)
                InvokeLog(LuzesTextBox, $"Enviado: {command}"); // Log na caixa das luzes
            else
                InvokeLog(LuzesTextBox, "Erro ao enviar comando.");
        }

        private async Task<bool> SendCommandToApi(string container, string content)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(API_BASE);

                string jsonBody = "{" +
                    "\"res-type\": \"content-instance\"," +
                    $"\"resource-name\": \"cmd-{DateTime.Now.Ticks}\"," +
                    $"\"content\": \"{content}\"," +
                    "\"content-type\": \"application/xml\"" +
                    "}";

                var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                try
                {
                    string endpoint = $"api/somiod/{APP_NAME}/{container}";
                    var response = await client.PostAsync(endpoint, httpContent);
                    return response.IsSuccessStatusCode;
                }
                catch (Exception ex)
                {
                    InvokeLog(LuzesTextBox, "API EXCEPTION: " + ex.Message);
                    return false;
                }
            }
        }

        // Eventos vazios
        private void lblEsperaY_Click(object sender, EventArgs e) { }
        private void txtLog_TextChanged(object sender, EventArgs e) { }
        private void lblVagasX_Click(object sender, EventArgs e) { }
    }
}