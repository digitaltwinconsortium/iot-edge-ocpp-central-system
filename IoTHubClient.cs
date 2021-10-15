/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

using ChargePointOperator.Models;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ProtocolGateway.Models;
using System;
using System.Text;
using System.Threading.Tasks;

namespace ProtocolGateway
{
    public class IoTHubClient : ICloudGatewayClient
    {
        private static ModuleClient _client;
        private readonly IConfiguration _configuration;
        private static Logger _logger;

        public IoTHubClient(IConfiguration configuration)
        {
            _configuration = configuration;
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

        public async Task SendTransactionMessageAsync(RequestPayload request, string chargepointName)
        {
            _logger.LogInformation($"Gateway Client : Updating {request.Action} for chargepoint {chargepointName}");

            try
            {
                RequestPayload requestPayload = (RequestPayload)request;
                requestPayload.Payload.Add(StringConstants.StationChargerTag, chargepointName);
                TwinCollection twins = new TwinCollection();
                twins[requestPayload.Action] = requestPayload.Payload;
                if (_client != null)
                {
                    await _client.UpdateReportedPropertiesAsync(twins).ConfigureAwait(false);
                    _logger.LogInformation($"Gateway Client : Updated {request.Action} for chargepoint {chargepointName}");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(chargepointName, "Gateway client : SendTransactionMessageAsync", e);
            }
        }

        public async Task SendTelemetryAsync(object request, string chargepointName)
        {
            _logger.LogInformation($"Gateway Client : Sending telemetry for chargepoint {chargepointName}");

            try
            {
                if (_client != null)
                {
                    Message message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request)));
                    await _client.SendEventAsync(message).ConfigureAwait(false);
                    _logger.LogInformation($"Gateway Client : Sent telemetry for chargepoint {chargepointName}");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(chargepointName, "Gateway Client : SendTelemetryAsync ", e);
            }
        }
    }
}
