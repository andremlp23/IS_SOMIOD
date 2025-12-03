using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using RestSharp;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml; // Para criar o XML de log

namespace IS_SOMIOD
{
    public partial class Form2 : Form
    {
        // --- CONFIGURAÇÕES ---
        private const string BROKER_IP = "127.0.0.1";
        private const string API_URL = "http://localhost:5000/api/somiod";

        // --- ESTADO DO ESTACIONAMENTO ---
        private int _vagasXOcupadas = 0;
        private int _camioesFilaY = 0;
        private const int MAX_VAGAS_X = 5;

        // Cliente MQTT
        private IMqttClient _mqttClient;

        public Form2()
        {
            InitializeComponent();
        }

        private async void Form2_Load(object sender, EventArgs e)
        {

        }

        private void lblEsperaY_Click(object sender, EventArgs e)
        {

        }
    }
}
