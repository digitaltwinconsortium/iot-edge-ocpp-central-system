/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

using ChargePointOperator.Models;
using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Schema;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net;
using System.Collections.Concurrent;
using ProtocolGateway;
using ProtocolGateway.Models;
using Microsoft.Extensions.Configuration;
using System.IO;
using OCPP16;

namespace ChargePointOperator
{
    public class WebsocketJsonMiddlewareOCPP16
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly Logger _logger;

        private List<string> knownChargers = new List<string>();
        public static ConcurrentDictionary<string, Charger> activeCharger = new ConcurrentDictionary<string, Charger>();
        private ICloudGatewayClient _gatewayClient;
        private string _logURL;

        public WebsocketJsonMiddlewareOCPP16(RequestDelegate next, IConfiguration configuration, ICloudGatewayClient gatewayClient)
        {
            _next = next;
            _configuration = configuration;
            _logURL = _configuration["LogURL"];
            _logger = new Logger();
            _gatewayClient = gatewayClient;
        }

        public WebsocketJsonMiddlewareOCPP16()
        {
        }

        public async Task Invoke(HttpContext httpContext)
        {
            try
            {
                _logger.LogInformation("Request starting");

                if (httpContext.WebSockets.IsWebSocketRequest)
                {
                    await HandleWebsockets(httpContext);
                    return;
                }

                // passed on to next middleware
                await _next(httpContext);
            }
            catch (Exception e)
            {
                _logger.LogError(httpContext.Request.Path.Value.Split('/').LastOrDefault(),"Invoke",e);

                httpContext.Response.StatusCode=StatusCodes.Status500InternalServerError;
                await httpContext.Response.WriteAsync("Something went wrong!!. Please check with the Central system admin");
            }
   
            _logger.LogDebug("Request finished.");
        }

        private async Task<bool> CheckProtocolAsync(HttpContext httpContext, string chargepointName)
        {
            var errorMessage = string.Empty;

            var chargerProtocols = httpContext.WebSockets.WebSocketRequestedProtocols;
            _logger.LogInformation($"Charger requested protocols : {chargerProtocols} for {chargepointName}");


            if (chargerProtocols.Count == 0)
                errorMessage = StringConstants.NoProtocolHeaderMessage;
            else
            {

                if (!chargerProtocols.Contains(StringConstants.RequiredProtocol)) //Allow only ocpp1.6
                    errorMessage = StringConstants.SubProtocolNotSupportedMessage;
                else
                    return true;
            }

            _logger.LogInformation($"Protocol conflict for {chargepointName}");

            //Websocket request with Protcols that are not supported are accepted and closed
            var socket = await httpContext.WebSockets.AcceptWebSocketAsync();
            await socket.CloseOutputAsync(WebSocketCloseStatus.ProtocolError, errorMessage, CancellationToken.None);

            return false;
        }

        private async Task HandleWebsockets(HttpContext httpContext)
        {
            _logger.LogDebug($"Entering HandleWebsockets method");
            string chargepointName = string.Empty;
            try
            {
                string requestPath = httpContext.Request.Path.Value;
                chargepointName = requestPath.Split('/').LastOrDefault();


                if (!knownChargers.Contains(chargepointName))
                {
                        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                        return;
                }

                if (!await CheckProtocolAsync(httpContext, chargepointName))
                   return;

                var socket = await httpContext.WebSockets.AcceptWebSocketAsync(StringConstants.RequiredProtocol);

                if (socket == null || socket.State != WebSocketState.Open)
                {
                    await _next(httpContext);
                    return;
                }

                if (!activeCharger.ContainsKey(chargepointName))
                {
                    activeCharger.TryAdd(chargepointName, new Charger(chargepointName, socket));
                    _logger.LogInformation($"No. of active chargers : {activeCharger.Count}");
                }
                else
                {
                    try
                    {
                        var oldSocket = activeCharger[chargepointName].WebSocket;
                        activeCharger[chargepointName].WebSocket = socket;
                        if (oldSocket != null)
                        {
                            _logger.LogWarning($"New websocket request received for {chargepointName}");
                            if (oldSocket != socket && oldSocket.State != WebSocketState.Closed)
                            {
                                _logger.LogWarning($"Closing old websocket for {chargepointName}");

                                await oldSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, StringConstants.ClientInitiatedNewWebsocketMessage, CancellationToken.None);
                            }
                        }
                        _logger.LogWarning($"Websocket replaced successfully for {chargepointName}");
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(chargepointName,"While closing old socket in HandleWebsockets",e);
                    }
                }


                if (socket.State == WebSocketState.Open)
                    await HandleActiveConnection(socket, chargepointName);

            }
            catch (Exception e)
            {
                _logger.LogError(chargepointName,"HandleWebsockets",e);
            }

            _logger.LogDebug($"Exiting HandleWebsockets method for {chargepointName}");

        }

        private async Task HandleActiveConnection(WebSocket webSocket, string chargepointName)
        {
            _logger.LogDebug($"Entering HandleActiveConnections method for {chargepointName}");
            _logger.LogInformation($"Websocket connected for {chargepointName}");
            try
            {
                if (webSocket.State == WebSocketState.Open)
                    await HandlePayloadsAsync(chargepointName, webSocket);

                if (webSocket.State != WebSocketState.Open && activeCharger.ContainsKey(chargepointName) && activeCharger[chargepointName].WebSocket == webSocket)
                    await RemoveConnectionsAsync(chargepointName, webSocket);

            }
            catch (Exception e)
            {
                _logger.LogError(chargepointName,"HandleActiveConnections",e);
            }
            _logger.LogDebug($"Exiting HandleActiveConnections method for {chargepointName}");
        }

        private async Task<string> ReceiveDataFromChargerAsync(WebSocket webSocket, string chargepointName)
        {
            _logger.LogDebug($"Receiving payload from charger {chargepointName}");

            try
            {
                ArraySegment<byte> data = new ArraySegment<byte>(new byte[1024]);
                WebSocketReceiveResult result;
                string payloadString = string.Empty;

                do
                {
                    result = await webSocket.ReceiveAsync(data, CancellationToken.None);

                    //When the charger sends close frame
                    if (result.CloseStatus.HasValue)
                    {
                        if (webSocket != activeCharger[chargepointName].WebSocket)
                        {
                            if(webSocket.State!=WebSocketState.CloseReceived)
                                await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, StringConstants.ChargerNewWebRequestMessage, CancellationToken.None);
                        }
                        else
                            await RemoveConnectionsAsync(chargepointName, webSocket);
                        return null;
                    }

                    //Appending received data
                    payloadString += Encoding.UTF8.GetString(data.Array, 0, result.Count);

                } while (!result.EndOfMessage);

                _logger.LogTrace($"Data from charger {chargepointName} : {payloadString}");
                return payloadString;

            }
            catch (WebSocketException websocex)
            {
                if (webSocket != activeCharger[chargepointName].WebSocket)
                    _logger.LogWarning($"WebsocketException occured in the old socket while receiving payload from charger {chargepointName}. Error : {websocex.Message}");
                else
                    _logger.LogError(chargepointName,"ReceiveDataFromChargerAsync",websocex);
            }
            catch (Exception e)
            {
                _logger.LogError(chargepointName,"ReceiveDataFromChargerAsync",e);

            }

            _logger.LogDebug($"Exiting Receive method for charger {chargepointName}");
            return null;
        }

        private async Task SendPayloadToChargerAsync(string chargepointName, object payload, WebSocket webSocket)
        {
            _logger.LogDebug($"Sending payload to charger {chargepointName}");
            var charger = activeCharger[chargepointName];

            try
            {
                charger.WebsocketBusy = true;

                var settings = new JsonSerializerSettings { DateFormatString = StringConstants.DateTimeFormat, NullValueHandling = NullValueHandling.Ignore };
                var serializedPayload = JsonConvert.SerializeObject(payload, settings);

                _logger.LogTrace($"Serialized Payload : {serializedPayload} for {chargepointName}");

                ArraySegment<byte> data = Encoding.UTF8.GetBytes(serializedPayload);

                if (webSocket.State == WebSocketState.Open)
                    await webSocket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception e)
            {
                _logger.LogError(chargepointName,"SendPayloadToCharger",e);
            }

            charger.WebsocketBusy = false;
        }

        private JArray ProcessPayload(string payloadString, string chargepointName)
        {
            _logger.LogDebug($"Processing payload for charger {chargepointName}");
            try
            {
                if (payloadString != null)
                {
                    _logger.LogTrace($"Input payload string : {payloadString}");
                    var basePayload = JsonConvert.DeserializeObject<JArray>(payloadString);
                    return basePayload;

                }
                else
                    _logger.LogWarning($"Null payload received for {chargepointName}");
            }
            catch (Exception e)
            {
                _logger.LogError(chargepointName,"ProcessPayload",e);
            }

            _logger.LogDebug($"Exiting processpayload method for charger {chargepointName}");
            return null;
        }

        private JsonValidationResponse JsonValidation(JObject payload, string action, string chargepointName)
        {
            _logger.LogDebug($"Entering Jsonvalidation for {chargepointName}");

            JsonValidationResponse response = new JsonValidationResponse { Valid = false };

            try
            {
               if (action != null)
               {
                   _logger.LogInformation($"Validating payload for {chargepointName} for action {action}.");
                   //Getting Schema FilePath
                   string currentDirectory = Directory.GetCurrentDirectory();
                   string filePath = Path.Combine(currentDirectory, "Schemas", $"{action}.json");

                   //Parsing schema
                   JObject content = JObject.Parse(File.ReadAllText(filePath));
                   JSchema schema = JSchema.Parse(content.ToString());
                   JToken json = JToken.Parse(payload.ToString()); // Parsing input payload

                   // Validate json
                   response = new JsonValidationResponse
                   {
                       Valid = json.IsValid(schema, out IList<ValidationError> errors),
                       Errors = errors.ToList()
                   };

               }
               else
                   _logger.LogError(chargepointName,"JsonValidation","Action is null");
            }
            catch (FileNotFoundException)
            {
                response.CustomErrors = StringConstants.NotImplemented;
            }
            catch (JsonReaderException jsre)
            {
               _logger.LogError(chargepointName,"JsonValidation",jsre);
            }
            catch (Exception e)
            {

               _logger.LogError(chargepointName,"JsonValidation",e);
            }

            _logger.LogDebug($"Exiting Jsonvalidation for {chargepointName}");
            return response;
        }

        private async Task<JArray> ProcessRequestPayloadAsync(string chargepointName, RequestPayload requestPayload)
        {
            _logger.LogDebug($"Processing requestPayload for charger {chargepointName}");
            string action = string.Empty;
            try
            {

                await LogPayloads(new LogPayload(requestPayload, chargepointName), chargepointName);

                action = requestPayload.Action;

                var isValidPayload = JsonValidation(requestPayload.Payload, action, chargepointName);

                if (isValidPayload.Valid)
                {
                    _logger.LogInformation($"{action} request received for charger {chargepointName}");

                    object responsePayload = null;
                    string url = string.Empty;

                    //switching based on OCPP action name
                    switch (action)
                    {

                        case "BootNotification":

                            BootNotificationRequest bootNotificationRequest = new BootNotificationRequest();
                            await _gatewayClient.SendTelemetryAsync(bootNotificationRequest, chargepointName);

                            BootNotificationResponse bootNotificationResponse = new BootNotificationResponse(RegistrationStatus.Accepted, DateTime.Now, 60);
                            responsePayload = new ResponsePayload(requestPayload.UniqueId, bootNotificationResponse);
                            break;

                        case "Authorize":

                            await _gatewayClient.SendTransactionMessageAsync(requestPayload, chargepointName);
                            break;

                        case "StartTransaction":

                            await _gatewayClient.SendTransactionMessageAsync(requestPayload, chargepointName);
                            break;

                        case "StopTransaction":

                            await _gatewayClient.SendTransactionMessageAsync(requestPayload, chargepointName);
                            break;

                        case "Heartbeat":

                            await _gatewayClient.SendTelemetryAsync(requestPayload, chargepointName);

                            responsePayload = new ResponsePayload(requestPayload.UniqueId, new { currentTime = DateTime.UtcNow });
                            break;

                        case "MeterValues":

                            MeterValuesRequest meterValues = requestPayload.Payload.ToObject<MeterValuesRequest>();
                            responsePayload = new ResponsePayload(requestPayload.UniqueId, new object());

                            foreach (var i in meterValues.meterValue)
                            {
                                foreach (var j in i.sampledValue)
                                {
                                    if (Regex.IsMatch(j.unit.ToString(), @"^(W|Wh|kWh|kW)$"))
                                    {
                                        await _gatewayClient.SendTelemetryAsync(j,chargepointName);
                                    }
                                }
                            }

                            break;

                        case "StatusNotification":

                            StatusNotificationRequest statusNotification = requestPayload.Payload.ToObject<StatusNotificationRequest>();
                            responsePayload = new ResponsePayload(requestPayload.UniqueId, new object());
                            
                            await _gatewayClient.SendTelemetryAsync(statusNotification, chargepointName);
                            break;

                        case "DataTransfer":

                            //<Placeholder>
                            break;

                        case "DiagnosticsStatusNotification":

                            //<Placeholder>
                            break;

                        case "FirmwareStatusNotification":

                            //<Placeholder>
                            break;

                        default:

                            responsePayload = new ErrorPayload(requestPayload.UniqueId, StringConstants.NotImplemented);
                            break;
                    }

                    if (responsePayload != null)
                    {
                        if (((BasePayload)responsePayload).MessageTypeId == 3)
                        {
                            ResponsePayload response = (ResponsePayload)responsePayload;
                            await LogPayloads(new LogPayload(action, response, chargepointName), chargepointName);
                            return response.WrappedPayload;
                        }
                        else
                        {
                            ErrorPayload error = (ErrorPayload)responsePayload;
                            await LogPayloads(new LogPayload(action, error, chargepointName), chargepointName);
                            return error.WrappedPayload;
                        }
                    }

                }
                else
                {
                    ErrorPayload errorPayload = new ErrorPayload(requestPayload.UniqueId);
                    GetErrorPayload(isValidPayload, errorPayload);

                    await LogPayloads(new LogPayload(action,errorPayload, chargepointName), chargepointName);
                    return errorPayload.WrappedPayload;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(chargepointName,$"ProcessREquestPayload for action {action}",e);
            }

            _logger.LogDebug($"Exiting Process request payload for {chargepointName}");
            return null;
        }

        private async Task ProcessResponsePayloadAsync(string chargepointName, ResponsePayload responsePayload)
        {
            _logger.LogDebug($"Processing responsePayload for charger {chargepointName}");

            await Task.Delay(1000);
            //Placeholder to process response payloads from charger for CentralSystem initiated commands
            _logger.LogDebug($"Exiting Process response payload for {chargepointName}");
        }

        private async Task ProcessErrorPayloadAsync(string chargepointName, ErrorPayload errorPayload)
        {
            //Placeholder to process error payloads from charger for CentralSystem initiated commands
            await Task.Delay(1000);

            _logger.LogDebug($"Exiting Process error payload for {chargepointName}");
        }

        private async Task RemoveConnectionsAsync(string chargepointName, WebSocket webSocket)
        {
            try
            {
                _logger.LogDebug($"Removing connection for charger {chargepointName}");

                if (activeCharger.TryRemove(chargepointName, out Charger charger))
                    _logger.LogDebug($"Removed charger {chargepointName}");
                else
                    _logger.LogDebug($"Cannot remove charger {chargepointName}");

                await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, StringConstants.ClientRequestedClosureMessage, CancellationToken.None);
                _logger.LogDebug($"Closed websocket for charger {chargepointName}. Remaining active chargers : {activeCharger.Count}");

            }
            catch (Exception e)
            {
                _logger.LogError(chargepointName,"RemoveConnectionsAsync",e);
            }
        }

        private void GetErrorPayload(JsonValidationResponse response, ErrorPayload errorPayload)
        {
            if (response.Errors != null)
                errorPayload.Payload = JObject.FromObject(new { Error = response.Errors });
            else
                errorPayload.Payload = JObject.FromObject(new object());

            errorPayload.ErrorDescription = string.Empty;

            if (response.CustomErrors != null)
                errorPayload.ErrorCode = "NotImplemented";
            else if (response.Errors == null || response.Errors.Count > 1)
                errorPayload.ErrorCode = "GenericError";
            else
                switch (response.Errors[0].ErrorType)
                {

                    case ErrorType.MultipleOf:
                    case ErrorType.Enum:
                        errorPayload.ErrorCode = "PropertyConstraintViolation";
                        break;

                    case ErrorType.Required:
                    case ErrorType.Format:
                    case ErrorType.AdditionalProperties:
                        errorPayload.ErrorCode = "FormationViolation";
                        break;

                    case ErrorType.Type:
                        errorPayload.ErrorCode = "TypeConstraintViolation";
                        break;

                    default:
                        errorPayload.ErrorCode = "GenericError";
                        break;
                }

        }

        private async Task HandlePayloadsAsync(string chargepointName, WebSocket webSocket)
        {
            _logger.LogDebug($"Entering HandlePayloads method for {chargepointName}");

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        string payloadString = await ReceiveDataFromChargerAsync(webSocket, chargepointName);
                        var payload = ProcessPayload(payloadString, chargepointName);

                        if (payload != null)
                        {
                            JArray response = null;

                            //switching based on messageTypeId
                            switch ((int)payload[0])
                            {
                                case 2:
                                    RequestPayload requestPayload = new RequestPayload(payload);
                                    _logger.LogTrace(JsonConvert.SerializeObject(requestPayload));
                                    response = await ProcessRequestPayloadAsync(chargepointName, requestPayload);
                                    break;

                                case 3:
                                    ResponsePayload responsePayload = new ResponsePayload(payload);
                                    await ProcessResponsePayloadAsync(chargepointName, responsePayload);
                                    break;

                                case 4:
                                    ErrorPayload errorPayload = new ErrorPayload(payload);
                                    await ProcessErrorPayloadAsync(chargepointName, errorPayload);
                                    continue;

                                default:
                                    break;
                            }

                            if (response != null)
                            {
                                await SendPayloadToChargerAsync(chargepointName, response, webSocket);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(chargepointName,"HandlePayloads - websocket - open" ,e);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(chargepointName,"HandlePayloads",e);
            }

            _logger.LogDebug($"Exiting HandlePayloads method for {chargepointName}");
        }

        private async Task LogPayloads(LogPayload logPayload, string chargepointName)
        {
            //In case LogURL is not provided
            if(string.IsNullOrEmpty(_logURL))
                return;

            _logger.LogTrace($"Logging payloads from charger {chargepointName}");

            try
            {
                var content = new StringContent(JsonConvert.SerializeObject(logPayload), Encoding.UTF8, StringConstants.RequestContentFormat);

                using (HttpClient client = new HttpClient())
                {
                    var response = await client.PostAsync(_logURL, content);

                    if (response.StatusCode == HttpStatusCode.OK)
                        _logger.LogDebug($"{logPayload.Command} Payload logged successfully for {chargepointName}");
                    else
                        _logger.LogWarning($"{response.StatusCode} received while logging payloads for {chargepointName}.");


                }
            }
            catch (Exception e)
            {
                _logger.LogError(chargepointName,$"LogPayload for {logPayload.Command}",e);
            }
        }
    }
}
