using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SOMIOD_Common;

namespace IS_SOMIOD
{
    public partial class Form2 : Form
    {
        // Usa a nossa nova classe Helper
        private SomiodMqttHelper _mqttHelper;

        private const string API_BASE = "http://localhost:62056/";
        private const string APP_NAME = "smart-parking";

        // Tópicos a que subscreve
        private const string TOPIC_SEMAFORO = "api/somiod/smart-parking/semaforo";
        private const string TOPIC_ZONA_X = "api/somiod/smart-parking/zona-x";
        private const string TOPIC_ZONA_Y = "api/somiod/smart-parking/zona-y";
        private const string TOPIC_LUZES = "api/somiod/smart-parking/luzes";

        private bool _lightsOn = true;
        private int _countZonaX = 0;
        private int _countZonaY = 0;
        private const int MAX_CAPACITY = 4;

        public Form2()
        {
            InitializeComponent();
            // Instanciar o Helper
            _mqttHelper = new SomiodMqttHelper("127.0.0.1", 1883);
        }

        private async void Form2_Load(object sender, EventArgs e)
        {
            UpdateLabels();
            lblSemaforo.BackColor = Color.Gray;
            // --- ESTADO: LIGADO ---
            btnLuzes.BackColor = Color.Yellow;
            btnLuzes.ForeColor = Color.Black;
            btnLuzes.Text = "💡 LUZES: LIGADAS\n(Clicar para Desligar)";

            // 1. Ouve o evento do Helper
            _mqttHelper.OnMessageReceived += HandleMqttMessage;

            // 2. Conecta e Subscreve
            try
            {
                await _mqttHelper.ConnectAsync("Dashboard");

                await _mqttHelper.SubscribeAsync(TOPIC_SEMAFORO);
                await _mqttHelper.SubscribeAsync(TOPIC_ZONA_X);
                await _mqttHelper.SubscribeAsync(TOPIC_ZONA_Y);
                await _mqttHelper.SubscribeAsync(TOPIC_LUZES);

                InvokeLog(ParkTextBox, "Dashboard Conectada e Subscrita.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro MQTT: " + ex.Message);
            }
        }

        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            _mqttHelper.DisconnectAsync().Wait();
        }

        // Este método é chamado automaticamente pelo Helper quando chega uma mensagem válida
        private void HandleMqttMessage(string topic, string container, string content, string eventType)
        {
            if (eventType != "Creation") return;

            this.BeginInvoke(new Action(() =>
            {
                // Encamenhamento para as TextBoxes
                if (topic == TOPIC_LUZES)
                    InvokeLog(LuzesTextBox, $"CMD: {content}");
                else
                    InvokeLog(ParkTextBox, $"[{container}] {content}");

                // Lógica de Estado
                ProcessTrafficLogic(container, content);
            }));
        }

        private void ProcessTrafficLogic(string container, string content)
        {
            switch (container)
            {
                case "semaforo":
                    if (content.Contains("VERDE")) lblSemaforo.BackColor = Color.LightGreen;
                    else if (content.Contains("VERMELHO")) lblSemaforo.BackColor = Color.IndianRed;
                    break;

                case "zona-x":
                    if (content.Contains("CAMIAO-IN"))
                    {
                        if (_countZonaX < MAX_CAPACITY) _countZonaX++;
                    }
                    break;

                case "zona-y":
                    if (content.Contains("MOVIDO"))
                    {
                        // MOVIMENTO DE X -> Y

                        if (_countZonaX > 0) _countZonaX--;

                        if (_countZonaY < MAX_CAPACITY) _countZonaY++;
                    }
                    else if (content.Contains("saiu") || content.Contains("SAIU"))
                    {
                        // SAÍDA DE Y
                        if (_countZonaY > 0) _countZonaY--;
                    }
                    break;

                case "luzes":
                    // Atualiza a variável de estado
                    bool isNowOn = content.Contains("ON");
                    _lightsOn = isNowOn; // Mantém a variável local sincronizada

                    if (isNowOn)
                    {
                        // --- ESTADO: LIGADO ---
                        btnLuzes.BackColor = Color.Yellow;
                        btnLuzes.ForeColor = Color.Black;
                        btnLuzes.Text = "💡 LUZES: LIGADAS\n(Clicar para Desligar)";
                    }
                    else
                    {
                        // --- ESTADO: DESLIGADO ---
                        btnLuzes.BackColor = Color.FromArgb(64, 64, 64);
                        btnLuzes.ForeColor = Color.White;
                        btnLuzes.Text = "LUZES: DESLIGADAS\n(Clicar para Ligar)";
                    }
                    InvokeLog(LuzesTextBox, $"Estado Luzes: {(isNowOn ? "ON" : "OFF")}");
                    break;
            }

            if (_countZonaX < 0) _countZonaX = 0;
            if (_countZonaY < 0) _countZonaY = 0;
            UpdateLabels();
        }

        // --- UI & API Helpers ---
        private void UpdateLabels()
        {
            lblVagasX.Text = $"Zona X (Espera): {_countZonaX} em espera";
            lblEsperaY.Text = $"Zona Y (Descarga): {_countZonaY} ocupados";
        }

        private void InvokeLog(RichTextBox target, string msg)
        {
            target.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            target.SelectionStart = target.Text.Length;
            target.ScrollToCaret();
        }

        private async void btnLuzes_Click(object sender, EventArgs e)
        {
            _lightsOn = !_lightsOn;
            string command = _lightsOn ? "ON" : "OFF";

            await SendCommandToApi("luzes", $"<cmd>{command}</cmd>");
        }

        private async Task<bool> SendCommandToApi(string container, string content)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(API_BASE);
                string jsonBody = "{\"res-type\": \"content-instance\", \"resource-name\": \"cmd-" + DateTime.Now.Ticks + "\", \"content\": \"" + content + "\", \"content-type\": \"application/xml\"}";
                var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                try
                {
                    var response = await client.PostAsync($"api/somiod/{APP_NAME}/{container}", httpContent);
                    return response.IsSuccessStatusCode;
                }
                catch { return false; }
            }
        }

        // Eventos vazios
        private void lblEsperaY_Click(object sender, EventArgs e) { }
        private void txtLog_TextChanged(object sender, EventArgs e) { }
        private void lblVagasX_Click(object sender, EventArgs e) { }
    }
}