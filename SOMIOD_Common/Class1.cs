using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using uPLibrary.Networking.M2Mqtt;

namespace SOMIOD_Common
{
    public class SomiodMqttHelper
    {
        private IMqttClient _client;
        private MqttFactory _factory;
        private string _brokerIp;
        private int _brokerPort;

        public event Action<string, string, string, string> OnMessageReceived;

        public SomiodMqttHelper(string brokerIp, int brokerPort)
        {
            _brokerIp = brokerIp;
            _brokerPort = brokerPort;
            _factory = new MqttFactory();
            _client = _factory.CreateMqttClient();
        }

        public async Task ConnectAsync(string clientIdSuffix)
        {
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_brokerIp, _brokerPort)
                .WithClientId($"SomiodClient_{clientIdSuffix}_{Guid.NewGuid()}")
                .Build();

            // Configurar o handler de mensagens ANTES de conectar
            _client.UseApplicationMessageReceivedHandler(e =>
            {
                string topic = e.ApplicationMessage.Topic;
                string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                ParseAndNotify(topic, payload);
            });

            if (!_client.IsConnected)
            {
                await _client.ConnectAsync(options, CancellationToken.None);
            }
        }

        public async Task SubscribeAsync(string topic)
        {
            if (_client.IsConnected)
            {
                await _client.SubscribeAsync(new MQTTnet.Client.Subscribing.MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(topic)
                    .Build());
            }
        }

        public async Task DisconnectAsync()
        {
            if (_client != null && _client.IsConnected)
            {
                await _client.DisconnectAsync();
            }
        }

        public bool IsConnected => _client != null && _client.IsConnected;

        // Lógica centralizada de parsing XML
        private void ParseAndNotify(string topic, string xmlPayload)
        {
            try
            {
                XDocument doc = XDocument.Parse(xmlPayload);
                string container = doc.Root.Element("Container")?.Value;
                string content = doc.Root.Element("Content")?.Value;
                string eventType = doc.Root.Element("Event")?.Value;

                // Só dispara o evento se alguém estiver à escuta
                OnMessageReceived?.Invoke(topic, container, content, eventType);
            }
            catch
            {
                // Ignora erros de parse ou logs se necessário
                // Opcional: Criar evento OnError
            }
        }
    }
}