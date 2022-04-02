/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

using MQTTnet;
using MQTTnet.Adapter;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Options;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using OCPPCentralSystem.Schemas.DTDL;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace OCPPCentralSystem
{
    public class MQTTClient : ICloudGatewayClient
    {
        private IMqttClient _client = null;
        private Timer _trigger;

        public ConcurrentDictionary<string, OCPPChargePoint> ChargePoints { get; set; }

        public MQTTClient()
        {
            ChargePoints = new ConcurrentDictionary<string, OCPPChargePoint>();

            _trigger = new Timer(new TimerCallback(SendTelemetry));
            int interval = 15000; // default to 15 seconds
            try
            {
                interval = int.Parse(Environment.GetEnvironmentVariable("Publishing_Interval"));
            }
            catch (Exception)
            {
                // do nothing
            }

            Connect();

            _trigger.Change(interval, interval);
        }

        private void SendTelemetry(object state)
        {
            try
            {
                string serializedMessage = JsonConvert.SerializeObject(ChargePoints);

                Publish(Encoding.UTF8.GetBytes(serializedMessage));

                Console.WriteLine("Sent to IoT Edge: " + serializedMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }
        }

        public void Connect()
        {
            try
            {
                // disconnect if still connected
                if ((_client != null) && _client.IsConnected)
                {
                    _client.DisconnectAsync().GetAwaiter().GetResult();
                    _client.Dispose();
                    _client = null;
                }

                // create MQTT password
                string password = Environment.GetEnvironmentVariable("MQTTPassword");
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CreateMQTTSASToken")))
                {
                    // create SAS token as password
                    TimeSpan sinceEpoch = DateTime.UtcNow - new DateTime(1970, 1, 1);
                    int week = 60 * 60 * 24 * 7;
                    string expiry = Convert.ToString((int)sinceEpoch.TotalSeconds + week);
                    string stringToSign = HttpUtility.UrlEncode(Environment.GetEnvironmentVariable("MQTTBrokerName") + " / devices/" + Environment.GetEnvironmentVariable("MQTTClientName")) + "\n" + expiry;
                    HMACSHA256 hmac = new HMACSHA256(Convert.FromBase64String(password));
                    string signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
                    password = "SharedAccessSignature sr=" + HttpUtility.UrlEncode(Environment.GetEnvironmentVariable("MQTTBrokerName") + " / devices/" + Environment.GetEnvironmentVariable("MQTTClientName")) + " & sig=" + HttpUtility.UrlEncode(signature) + "&se=" + expiry;
                }

                // create MQTT client
                _client = new MqttFactory().CreateMqttClient();
                var clientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer(opt => opt.NoDelay = true)
                    .WithClientId(Environment.GetEnvironmentVariable("MQTTClientName"))
                    .WithTcpServer(Environment.GetEnvironmentVariable("MQTTBrokerName"), !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UseTLS")) ? 8883 : 1883)
                    .WithTls(new MqttClientOptionsBuilderTlsParameters { UseTls = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UseTLS")) })
                    .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V311)
                    .WithCommunicationTimeout(TimeSpan.FromSeconds(10))
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(100))
                    .WithCleanSession(true) // clear existing subscriptions 
                    .WithCredentials(Environment.GetEnvironmentVariable("MQTTUsername"), password);

                // setup disconnection handling
                _client.UseDisconnectedHandler(disconnectArgs =>
                {
                    Console.WriteLine($"Disconnected from MQTT broker: {disconnectArgs.Reason}");

                    // wait a 5 seconds, then simply reconnect again, if needed
                    Task.Delay(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                    if ((_client == null) || !_client.IsConnected)
                    {
                        Connect();
                    }
                });

                try
                {
                    var connectResult = _client.ConnectAsync(clientOptions.Build(), CancellationToken.None).GetAwaiter().GetResult();
                    if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
                    {
                        var status = GetStatus(connectResult.UserProperties)?.ToString("x4");
                        throw new Exception($"Connection to MQTT broker failed. Status: {connectResult.ResultCode}; status: {status}");
                    }

                    Console.WriteLine("Connected to MQTT broker.");
                }
                catch (MqttConnectingFailedException ex)
                {
                    Console.WriteLine($"Failed to connect with reason {ex.ResultCode} and message: {ex.Message}");
                    if (ex.Result?.UserProperties != null)
                    {
                        foreach (var prop in ex.Result.UserProperties)
                        {
                            Console.WriteLine($"{prop.Name}: {prop.Value}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to connect to MQTT broker: " + ex.Message);
            }
        }
        // parses status from packet properties
        private int? GetStatus(List<MqttUserProperty> properties)
        {
            var status = properties.FirstOrDefault(up => up.Name == "status");
            if (status == null)
            {
                return null;
            }

            return int.Parse(status.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        public void Publish(byte[] payload)
        {
            MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithTopic(Environment.GetEnvironmentVariable("MQTTMessageTopic"))
                .WithPayload(payload)
                .Build();

            _client.PublishAsync(message).GetAwaiter().GetResult();
        }
    }
}
