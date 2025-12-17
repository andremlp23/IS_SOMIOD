using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SOMIOD_Common
{
    public class SomiodMqttHelper
    {
        private IMqttClient _client;
        private MqttFactory _factory;
        private string _brokerIp;
        private int _brokerPort;

        // [NOVO] Um cadeado para impedir conexões simultâneas (Thread-Safety)
        private static readonly SemaphoreSlim _connectLock = new SemaphoreSlim(1, 1);

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
            // 1. Espera a sua vez para entrar (Lock)
            await _connectLock.WaitAsync();

            try
            {
                // 2. Se já estiver conectado, não faz nada e sai
                if (_client.IsConnected) return;

                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(_brokerIp, _brokerPort)
                    .WithClientId($"SomiodClient_{clientIdSuffix}_{Guid.NewGuid()}")
                    .Build();

                _client.UseApplicationMessageReceivedHandler(e =>
                {
                    string topic = e.ApplicationMessage.Topic;
                    string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    ParseAndNotify(topic, payload);
                });

                // 3. Tenta conectar com segurança
                await _client.ConnectAsync(options, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Se der erro real, lançamos para a aplicação saber
                throw ex;
            }
            finally
            {
                // 4. Liberta a porta para o próximo (Unlock)
                _connectLock.Release();
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

        private void ParseAndNotify(string topic, string xmlPayload)
        {
            try
            {
                XDocument doc = XDocument.Parse(xmlPayload);
                string container = doc.Root.Element("Container")?.Value;
                string content = doc.Root.Element("Content")?.Value;
                string eventType = doc.Root.Element("Event")?.Value;

                OnMessageReceived?.Invoke(topic, container, content, eventType);
            }
            catch
            {
                // Ignora erros de XML malformado
            }
        }
    }
}