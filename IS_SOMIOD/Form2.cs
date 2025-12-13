using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq; // Necessário para parsear o XML

namespace IS_SOMIOD
{
    public partial class Form2 : Form
    {
        // --- CONFIGURAÇÕES MQTT ---
        private IMqttClient _mqttClient;
        private const string BROKER_IP = "127.0.0.1";
        private const int BROKER_PORT = 1883;

        // Subscreve a TUDO o que acontece na aplicação smart-dock usando o wildcard '#'
        private const string TOPIC_WILDCARD = "api/somiod/smart-dock/#";

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
            lblSemaforo.BackColor = Color.Gray; // Estado desconhecido inicial

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
                .WithClientId("Dashboard_Viewer_" + Guid.NewGuid()) // ID único
                .Build();

            // Handler de Conexão
            _mqttClient.UseConnectedHandler(async e =>
            {
                InvokeLog("Conectado ao Broker MQTT.");
                // Subscreve ao tópico wildcard para ouvir tudo da app
                await _mqttClient.SubscribeAsync(new MQTTnet.Client.Subscribing.MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(TOPIC_WILDCARD)
                    .Build());
                InvokeLog($"Subscrito a: {TOPIC_WILDCARD}");
            });

            // Handler de Mensagens Recebidas
            _mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                string topic = e.ApplicationMessage.Topic;

                // 1. Logar a mensagem bruta
                InvokeLog($"[{topic}] MSG Recebida");

                // 2. Processar Lógica
                ProcessNotification(payload);
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
        // LÓGICA DE NEGÓCIO (Parsing XML)
        // ---------------------------------------------------------
        private void ProcessNotification(string xmlPayload)
        {
            try
            {
                XDocument doc = XDocument.Parse(xmlPayload);

                string container = doc.Root.Element("Container")?.Value;
                string content = doc.Root.Element("Content")?.Value;
                string eventType = doc.Root.Element("Event")?.Value; // "Creation" ou "Deletion"

                // Apenas nos interessam eventos de Criação (Creation) onde os dados são passados
                // O middleware envia Creation quando fazemos POST
                if (eventType != "Creation") return;

                // Atualizar UI na Thread Principal
                this.BeginInvoke(new Action(() =>
                {
                    // Adiciona ao Log visual o conteúdo descodificado
                    txtLog.AppendText($" > Cont: {container} | Dados: {content}" + Environment.NewLine);

                    // Lógica de Contagem e Semáforo
                    switch (container)
                    {
                        case "semaforo":
                            if (content.Contains("VERDE"))
                                lblSemaforo.BackColor = Color.LightGreen;
                            else if (content.Contains("VERMELHO"))
                                lblSemaforo.BackColor = Color.IndianRed;
                            break;

                        case "zona-x":
                            // Se entrou um camião na Zona X
                            if (content.Contains("CAMIAO-IN"))
                            {
                                _countZonaX++;
                            }
                            break;

                        case "zona-y":
                            // Zona Y recebe "MOVIDO" (vem de X) ou "saiu" (vai embora)
                            if (content.Contains("MOVIDO"))
                            {
                                // Saiu de X e entrou em Y
                                _countZonaX--;
                                _countZonaY++;
                            }
                            else if (content.Contains("saiu") || content.Contains("SAIU"))
                            {
                                // Saiu de Y (foi embora)
                                _countZonaY--;
                            }
                            break;
                    }

                    // Proteção para não ter números negativos (caso a dashboard abra a meio)
                    if (_countZonaX < 0) _countZonaX = 0;
                    if (_countZonaY < 0) _countZonaY = 0;

                    UpdateLabels();
                }));
            }
            catch (Exception ex)
            {
                InvokeLog("Erro ao processar XML: " + ex.Message);
            }
        }

        // ---------------------------------------------------------
        // UI HELPERS
        // ---------------------------------------------------------
        private void UpdateLabels()
        {
            lblVagasX.Text = $"Zona X (Descarga): {_countZonaX} Ocupadas";
            lblEsperaY.Text = $"Zona Y (Saída): {_countZonaY} em Espera";
        }

        private void InvokeLog(string msg)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(InvokeLog), msg);
            }
            else
            {
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            }
        }

        // Eventos vazios gerados pelo designer (podem ficar ou ser apagados se removeres do designer)
        private void lblEsperaY_Click(object sender, EventArgs e) { }
        private void txtLog_TextChanged(object sender, EventArgs e) { }
    }
}