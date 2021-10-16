/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using OCPPCentralStation.schemas.dtdl;
using System;
using System.Text;
using System.Threading.Tasks;

namespace ProtocolGateway
{
    public class IoTCentralClient : ICloudGatewayClient
    {
        private static ModuleClient _client;
        private static Logger _logger;

        public IoTCentralClient()
        {
            _logger = new Logger();

            try
            {
                _client = ModuleClient.CreateFromEnvironmentAsync().GetAwaiter().GetResult();
            }
            catch(Exception ex)
            {
                _logger.LogError("GatewayClient constructor", ex);
            }
        }

        public async Task CloseAsync()
        {
            _logger.LogInformation("Closing gateway client");

            if (_client != null)
            {
                await _client.CloseAsync();
                _client = null;
            }
        }

        public async Task SendTelemetryAsync(EVChargingStation telemetry)
        {
            try
            {
                if (_client != null)
                {
                    Message message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(telemetry)));
                    await _client.SendEventAsync(message).ConfigureAwait(false);
                    _logger.LogInformation($"Gateway Client : Sent telemetry for chargepoint {telemetry.ID}");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(telemetry.ID, "Gateway Client : SendTelemetryAsync ", e);
            }
        }
    }
}
