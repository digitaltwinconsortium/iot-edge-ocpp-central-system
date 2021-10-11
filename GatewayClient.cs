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
    public class GatewayClient : IGatewayClient
    {
        private static ModuleClient _client;
        private static Dictionary<string, TwinRequestInfo> _chargerTwinRequests = new Dictionary<string, TwinRequestInfo>();
        private static Func<string, object, Task> _sendToCharger = null;
        private readonly IConfiguration _configuration;
        private readonly int _bootInterval;
        private static Logger _logger;

        public GatewayClient(IConfiguration configuration)
        {
            _configuration = configuration;
            _logger = new Logger();
            String interval = string.IsNullOrEmpty(_configuration["OCPPBootInterval"]) ? "120" : _configuration["OCPPBootInterval"];
            _bootInterval = Convert.ToInt32(interval);

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
        /// This method sets the callback method that has to be invoked to send the payload to the charger
        /// </summary>
        /// <param name="action">send method</param>
        public void SetSendToChargepointMethod(Func<string, object, Task> action)
        {
            _logger.LogInformation("Setting SendToCharger method");
            _sendToCharger = action;
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
                _chargerTwinRequests.Clear();
        }

        /// <summary>
        /// This method removes the inactive chargerpoint from the dictionary
        /// </summary>
        /// <param name="chargepointName">Id of the charger</param>
        /// <returns></returns>
        public void RemoveChargepointAsync(string chargepointName)
        {
            _logger.LogInformation($"Gateway Client : Removing chargepoint {chargepointName}");

            if (_chargerTwinRequests.ContainsKey(chargepointName))
                _chargerTwinRequests.Remove(chargepointName);
        }

        /// <summary>
        /// This method verifies the BootNotification request and sends back appropriate response
        /// </summary>
        /// <param name="request">request payload</param>
        /// <param name="chargepointName">charger Id</param>
        /// <returns></returns>
        public Task<ResponsePayload> SendBootNotificationAsync(RequestPayload request, string chargepointName)
        {
            _logger.LogInformation($"Gateway Client : Sending BootNotification for {chargepointName}");

            BootNotificationResponse bootNotificationResponse = null;
            RequestPayload requestPayload = new RequestPayload(request);
            ResponsePayload responsePayload = null;

            bootNotificationResponse = new BootNotificationResponse(RegistrationStatus.Accepted, DateTime.Now, _bootInterval);
            responsePayload = new ResponsePayload(requestPayload.UniqueId, bootNotificationResponse);
            return Task.FromResult(responsePayload);
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
                    _chargerTwinRequests[chargepointName] = twinRequestInfo;
                else
                    _chargerTwinRequests.Add(chargepointName, twinRequestInfo);

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
                var serializedPayload = JsonConvert.SerializeObject(request);
                Message message = new Message(Encoding.UTF8.GetBytes(serializedPayload));

                if (_client != null)
                {
                    await _client.SendEventAsync(message).ConfigureAwait(false);
                    _logger.LogInformation($"Gateway Client : Sent telemetry for chargepoint {chargepointName}");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(chargepointName, "Gateway Client : SendTelemetryAsync ", e);
            }
        }

        //Helper methods

        /// <summary>
        /// This method listens to Authorize and other transaction message response (desired properties) from the IoTHub
        /// </summary>
        /// <param name="twins">twin properties</param>
        /// <param name="userContext">chargerpointname</param>
        /// <returns></returns>
        private async Task OnTransactionMessageResponse(TwinCollection twins, object userContext)
        {
            var chargepointName = userContext.ToString();
            ResponsePayload responsePayload = null;

            _logger.LogInformation($"Gateway Client : Received property update for {chargepointName}.");

            try
            {

                if (_chargerTwinRequests.ContainsKey(chargepointName))
                {
                    var twinRequestInfo = _chargerTwinRequests[chargepointName];
                    if (twins.Contains(twinRequestInfo.Action))
                    {
                        object payload = twins[twinRequestInfo.Action];

                        responsePayload = new ResponsePayload(twinRequestInfo.UniqueId, payload);
                        _chargerTwinRequests.Remove(chargepointName);
                        await _sendToCharger(chargepointName, responsePayload.WrappedPayload);

                        _logger.LogInformation($"Gateway Client : Sending updated response for {chargepointName}.");
                    }
                    else
                        _logger.LogTrace($"Gateway Client : No action found in twins but Desired update received for {chargepointName}. Twins : {JsonConvert.SerializeObject(twins)}");

                }
                else
                    _logger.LogTrace($"Gateway Client : No record in dictionary but Desired update received for {chargepointName}. Twins : {JsonConvert.SerializeObject(twins)}");
            }
            catch (Exception e)
            {
                _logger.LogError(chargepointName, "Gateway Client : OnTransactionMessageAsync ", e);
            }
        }
    }
}