/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using OCPPCentralSystem.Schemas.DTDL;
using System;
using System.Text;
using System.Threading;

namespace OCPPCentralSystem
{
    public class IoTCentralClient : ICloudGatewayClient
    {
        private ModuleClient _client;
        private Logger _logger = new Logger();
        private Timer _trigger;

        public OCPPChargePoint Telemetry { get; set; }

        public IoTCentralClient()
        {
            try
            {
                _client = ModuleClient.CreateFromEnvironmentAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError("GatewayClient constructor", ex);
            }

            Telemetry = new OCPPChargePoint();
          
            _trigger = new Timer(new TimerCallback(SendTelemetryAsync));
            int interval = 15000; // default to 15 seconds
            try
            {
                interval = int.Parse(Environment.GetEnvironmentVariable("Publishing_Interval"));
            }
            catch(Exception)
            {
                // do nothing
            }

            _trigger.Change(interval, interval);
        }

        private async void SendTelemetryAsync(object state)
        {
            try
            {
                if (_client != null)
                {
                    Message message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Telemetry)));
                    await _client.SendEventAsync(message).ConfigureAwait(false);
                    _logger.LogInformation($"Gateway Client : Sent telemetry for chargepoint {Telemetry.ID}");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(Telemetry.ID, "Gateway Client : SendTelemetryAsync ", e);
            }
        }
    }
}
