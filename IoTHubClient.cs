/*****************************************************************************************************************
 * Author: Ajantha Dhanasekaran
 * Date: 07-Sept-2020
 * Purpose: Gateway client to communicate charger commands with Iot Hub
 * Change History:
 * Name                         Date                    Change description
 * Ajantha Dhanasekaran         07-Sept-2020              Initial version
 * Ajantha Dhanasekaran         16-Sept-2020              Enabled logging
 * Ajantha Dhanasekaran         23-Sept-2020              Enabled additional logging; Modified _chargerClients to be
                                                                    thread-safe
 * ****************************************************************************************************************/


#region license

/*
Cognizant EV Charging Protocol Gateway 1.0
© 2020 Cognizant. All rights reserved.
"Cognizant EV Charging Protocol Gateway 1.0" by Cognizant  is licensed under Apache License Version 2.0


Copyright 2020 Cognizant


Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at


    http://www.apache.org/licenses/LICENSE-2.0


Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

#endregion

using ChargePointOperator.Models;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ProtocolGateway.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ProtocolGateway
{
    public class IoTHubClient : ICloudGatewayClient
    {
        private static ModuleClient _client;
        private static Dictionary<string, TwinRequestInfo> _chargerTwinRequests = new Dictionary<string, TwinRequestInfo>();
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

        /// <summary>
        /// This method closes and removes all the charger device client connections
        /// </summary>
        /// <returns></returns>
        public async Task CloseAsync()
        {
            _logger.LogInformation("Closing gateway client");

            if (_client != null)
            {
                await _client.CloseAsync();
                _client = null;
            }

            if (_chargerTwinRequests.Count != 0)
            {
                _chargerTwinRequests.Clear();
            }
        }

        /// <summary>
        /// This method updates Authorize, StartTransaction and StopTransaction as ReportedProperties for the given charger */
        /// </summary>
        /// <param name="request">request payload</param>
        /// <param name="chargepointName">charger Id</param>
        /// <returns></returns>
        public async Task SendTransactionMessageAsync(RequestPayload request, string chargepointName)
        {
            _logger.LogInformation($"Gateway Client : Updating {request.Action} for chargepoint {chargepointName}");

            try
            {
                RequestPayload requestPayload = (RequestPayload)request;
                requestPayload.Payload.Add(StringConstants.StationChargerTag, chargepointName);
                TwinRequestInfo twinRequestInfo = new TwinRequestInfo(requestPayload.UniqueId, requestPayload.Action);

                if (_chargerTwinRequests.ContainsKey(chargepointName))
                {
                    _chargerTwinRequests[chargepointName] = twinRequestInfo;
                }
                else
                {
                    _chargerTwinRequests.Add(chargepointName, twinRequestInfo);
                }

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

        /// <summary>
        /// This method sends the telemetries such as Heartbeat,StatusNotification and MeterValues to the IoTHub
        /// </summary>
        /// <param name="request">request payload</param>
        /// <param name="chargepointName">charger Id</param>
        /// <returns></returns>
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