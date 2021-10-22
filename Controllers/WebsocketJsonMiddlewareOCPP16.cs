/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using OCPPCentralSystem.Models;
using OCPPCentralSystem.Schemas.DTDL;
using OCPPCentralSystem.Schemas.OCPP16;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OCPPCentralSystem.Controllers
{
    public class WebsocketJsonMiddlewareOCPP16
    {
        private readonly RequestDelegate _next;

        private List<string> knownChargers = new List<string>();
        private static ConcurrentDictionary<string, Charger> activeCharger = new ConcurrentDictionary<string, Charger>();
        private ICloudGatewayClient _gatewayClient;
        private int _transactionNumber = 0;

        public WebsocketJsonMiddlewareOCPP16(RequestDelegate next, ICloudGatewayClient gatewayClient)
        {
            _next = next;
            _gatewayClient = gatewayClient;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            try
            {
                if (httpContext.WebSockets.IsWebSocketRequest)
                {
                    await HandleWebsockets(httpContext);
                    return;
                }

                // passed on to next middleware
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine(ex.StackTrace);

                httpContext.Response.StatusCode=StatusCodes.Status500InternalServerError;
                await httpContext.Response.WriteAsync("Something went wrong!!. Please check with the Central system admin");
            }
        }

        private async Task<bool> CheckProtocolAsync(HttpContext httpContext, string chargepointName)
        {
            var errorMessage = string.Empty;

            var chargerProtocols = httpContext.WebSockets.WebSocketRequestedProtocols;
            if (chargerProtocols.Count == 0)
            {
                errorMessage = StringConstants.NoProtocolHeaderMessage;
            }
            else
            {
                if (chargerProtocols.Contains(StringConstants.RequiredProtocol)) //Allow only ocpp1.6
                {
                    errorMessage = StringConstants.SubProtocolNotSupportedMessage;
                }
                else
                {
                    return true;
                }
            }

            Console.WriteLine($"Protocol conflict for {chargepointName}");

            //Websocket request with Protcols that are not supported are accepted and closed
            var socket = await httpContext.WebSockets.AcceptWebSocketAsync();
            await socket.CloseOutputAsync(WebSocketCloseStatus.ProtocolError, errorMessage, CancellationToken.None);

            return false;
        }

        private async Task HandleWebsockets(HttpContext httpContext)
        {
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
                    Console.WriteLine($"No. of active chargers : {activeCharger.Count}");
                }
                else
                {
                    try
                    {
                        var oldSocket = activeCharger[chargepointName].WebSocket;
                        activeCharger[chargepointName].WebSocket = socket;
                        if (oldSocket != null)
                        {
                            Console.WriteLine($"New websocket request received for {chargepointName}");
                            if (oldSocket != socket && oldSocket.State != WebSocketState.Closed)
                            {
                                Console.WriteLine($"Closing old websocket for {chargepointName}");

                                await oldSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, StringConstants.ClientInitiatedNewWebsocketMessage, CancellationToken.None);
                            }
                        }
                        Console.WriteLine($"Websocket replaced successfully for {chargepointName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception: " + ex.Message);
                        Console.WriteLine(ex.StackTrace);
                    }
                }

                if (socket.State == WebSocketState.Open)
                {
                    await HandleActiveConnection(socket, chargepointName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private async Task HandleActiveConnection(WebSocket webSocket, string chargepointName)
        {
            try
            {
                if (webSocket.State == WebSocketState.Open)
                    await HandlePayloadsAsync(chargepointName, webSocket);

                if (webSocket.State != WebSocketState.Open && activeCharger.ContainsKey(chargepointName) && activeCharger[chargepointName].WebSocket == webSocket)
                    await RemoveConnectionsAsync(chargepointName, webSocket);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private async Task<string> ReceiveDataFromChargerAsync(WebSocket webSocket, string chargepointName)
        {
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

                return payloadString;
            }
            catch (WebSocketException websocex)
            {
                if (webSocket != activeCharger[chargepointName].WebSocket)
                {
                    Console.WriteLine($"WebsocketException occured in the old socket while receiving payload from charger {chargepointName}. Error : {websocex.Message}");
                }
                else
                {
                    Console.WriteLine("Exception: " + websocex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            return null;
        }

        private async Task SendPayloadToChargerAsync(string chargepointName, object payload, WebSocket webSocket)
        {
            var charger = activeCharger[chargepointName];

            try
            {
                charger.WebsocketBusy = true;

                var settings = new JsonSerializerSettings { DateFormatString = StringConstants.DateTimeFormat, NullValueHandling = NullValueHandling.Ignore };
                var serializedPayload = JsonConvert.SerializeObject(payload, settings);

                ArraySegment<byte> data = Encoding.UTF8.GetBytes(serializedPayload);

                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            charger.WebsocketBusy = false;
        }

        private JArray ProcessPayload(string payloadString, string chargepointName)
        {
            try
            {
                if (payloadString != null)
                {
                    var basePayload = JsonConvert.DeserializeObject<JArray>(payloadString);
                    return basePayload;
                }
                else
                {
                    Console.WriteLine($"Null payload received for {chargepointName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            return null;
        }

        private JsonValidationResponse JsonValidation(JObject payload, string action, string chargepointName)
        {
            JsonValidationResponse response = new JsonValidationResponse { Valid = false };

            try
            {
                if (action != null)
                {
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
                {
                    Console.WriteLine("JsonValidation: Action is null");
                }
            }
            catch (FileNotFoundException)
            {
                response.CustomErrors = StringConstants.NotImplemented;
            }
            catch (JsonReaderException jsre)
            {
                Console.WriteLine("Exception: " + jsre.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            return response;
        }

        private Task<JArray> ProcessRequestPayloadAsync(string chargepointName, RequestPayload requestPayload)
        {
            string action = string.Empty;

            try
            {
                action = requestPayload.Action;

                var isValidPayload = JsonValidation(requestPayload.Payload, action, chargepointName);

                if (isValidPayload.Valid)
                {
                    object responsePayload = null;
                    string url = string.Empty;

                    //switching based on OCPP action name
                    switch (action)
                    {
                        case "Authorize":
                        {
                            AuthorizeRequest request = requestPayload.Payload.ToObject<AuthorizeRequest>();

                            Console.WriteLine("Authorization requested on chargepoint " + request.chargeBoxIdentity + "  and badge ID " + request.idTag);

                            // always authorize any badge for now
                            IdTagInfo info = new IdTagInfo
                            {
                                expiryDateSpecified = false,
                                status = AuthorizationStatus.Accepted
                            };

                            AuthorizeResponse response = new AuthorizeResponse(info);
                            responsePayload = new ResponsePayload(requestPayload.UniqueId, response);
                            break;
                        }
                        case "BootNotification":
                        {
                            BootNotificationRequest request = requestPayload.Payload.ToObject<BootNotificationRequest>();

                            Console.WriteLine("Chargepoint with identity: " + request.chargeBoxIdentity + " booted!");

                            if (!_gatewayClient.ChargePoints.ContainsKey(request.chargeBoxIdentity))
                            {
                                _gatewayClient.ChargePoints.TryAdd(request.chargeBoxIdentity, new OCPPChargePoint());
                                _gatewayClient.ChargePoints[request.chargeBoxIdentity].ID = request.chargeBoxIdentity;
                            }

                            BootNotificationResponse response = new BootNotificationResponse(RegistrationStatus.Accepted, DateTime.UtcNow, 60);
                            responsePayload = new ResponsePayload(requestPayload.UniqueId, response);
                            break;
                        }
                        case "Heartbeat":
                        {
                            HeartbeatRequest request = requestPayload.Payload.ToObject<HeartbeatRequest>();

                            Console.WriteLine("Heartbeat received from: " + request.chargeBoxIdentity);

                            if (!_gatewayClient.ChargePoints.ContainsKey(request.chargeBoxIdentity))
                            {
                                _gatewayClient.ChargePoints.TryAdd(request.chargeBoxIdentity, new OCPPChargePoint());
                                _gatewayClient.ChargePoints[request.chargeBoxIdentity].ID = request.chargeBoxIdentity;
                            }

                            HeartbeatResponse response = new HeartbeatResponse(DateTime.UtcNow);
                            responsePayload = new ResponsePayload(requestPayload.UniqueId, response);
                            break;
                        }
                        case "MeterValues":
                        {
                            MeterValuesRequest request = requestPayload.Payload.ToObject<MeterValuesRequest>();

                            Console.WriteLine("Meter values for connector ID " + request.connectorId + " on chargepoint " + request.chargeBoxIdentity + ":");

                            if (!_gatewayClient.ChargePoints.ContainsKey(request.chargeBoxIdentity))
                            {
                                _gatewayClient.ChargePoints.TryAdd(request.chargeBoxIdentity, new OCPPChargePoint());
                                _gatewayClient.ChargePoints[request.chargeBoxIdentity].ID = request.chargeBoxIdentity;
                            }

                            if (!_gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors.ContainsKey(request.connectorId))
                            {
                                _gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors.TryAdd(request.connectorId, new Connector(request.connectorId));
                            }

                            foreach (MeterValue meterValue in request.meterValue)
                            {
                                foreach (SampledValue sampledValue in meterValue.sampledValue)
                                {
                                    Console.WriteLine("Value: " + sampledValue.value + " " + sampledValue.unit.ToString());
                                    int prasedInt = 0;
                                    if (int.TryParse(sampledValue.value, out prasedInt))
                                    {
                                        MeterReading reading = new MeterReading();
                                        reading.MeterValue = int.Parse(sampledValue.value);
                                        if (sampledValue.unitSpecified)
                                        {
                                            reading.MeterValueUnit = sampledValue.unit.ToString();
                                        }
                                        reading.Timestamp = meterValue.timestamp;
                                        _gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors[request.connectorId].MeterReadings.Add(reading);
                                        if (_gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors[request.connectorId].MeterReadings.Count > 10)
                                        {
                                            _gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors[request.connectorId].MeterReadings.RemoveAt(0);
                                        }
                                    }
                                }
                            }

                            MeterValuesResponse response = new MeterValuesResponse();
                            responsePayload = new ResponsePayload(requestPayload.UniqueId, response);
                            break;
                        }
                        case "StartTransaction":
                        {
                            StartTransactionRequest request = requestPayload.Payload.ToObject<StartTransactionRequest>();

                            Console.WriteLine("Start transaction " + _transactionNumber.ToString() + " from " + request.timestamp + " on chargepoint " + request.chargeBoxIdentity + " on connector " + request.connectorId + " with badge ID " + request.idTag + " and meter reading at start " + request.meterStart);

                            if (!_gatewayClient.ChargePoints.ContainsKey(request.chargeBoxIdentity))
                            {
                                _gatewayClient.ChargePoints.TryAdd(request.chargeBoxIdentity, new OCPPChargePoint());
                                _gatewayClient.ChargePoints[request.chargeBoxIdentity].ID = request.chargeBoxIdentity;
                            }

                            if (!_gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors.ContainsKey(request.connectorId))
                            {
                                _gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors.TryAdd(request.connectorId, new Connector(request.connectorId));
                            }
                            _transactionNumber++;
                            Transaction transaction = new Transaction(_transactionNumber)
                            {
                                BadgeID = request.idTag,
                                StartTime = request.timestamp,
                                MeterValueStart = request.meterStart
                            };

                            if (!_gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors[request.connectorId].CurrentTransactions.ContainsKey(_transactionNumber))
                            {
                                _gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors[request.connectorId].CurrentTransactions.TryAdd(_transactionNumber, transaction);
                            }

                            KeyValuePair<int, Transaction>[] transactionsArray = _gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors[request.connectorId].CurrentTransactions.ToArray();
                            for (int i = 0; i < transactionsArray.Length; i++)
                            {
                                if ((transactionsArray[i].Value.StopTime != DateTime.MinValue) && (transactionsArray[i].Value.StopTime < DateTime.UtcNow.Subtract(TimeSpan.FromDays(1))))
                                {
                                    _gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors[request.connectorId].CurrentTransactions.TryRemove(transactionsArray[i].Key, out _);
                                }
                            }

                            IdTagInfo info = new IdTagInfo
                            {
                                expiryDateSpecified = false,
                                status = AuthorizationStatus.Accepted
                            };

                            StartTransactionResponse response = new StartTransactionResponse(_transactionNumber, info);
                            responsePayload = new ResponsePayload(requestPayload.UniqueId, response);
                            break;
                        }
                        case "StopTransaction":
                        {
                            StopTransactionRequest request = requestPayload.Payload.ToObject<StopTransactionRequest>();

                            Console.WriteLine("Stop transaction " + request.transactionId.ToString() + " from " + request.timestamp + " on chargepoint " + request.chargeBoxIdentity + " with badge ID " + request.idTag + " and meter reading at stop " + request.meterStop);

                            if (!_gatewayClient.ChargePoints.ContainsKey(request.chargeBoxIdentity))
                            {
                                _gatewayClient.ChargePoints.TryAdd(request.chargeBoxIdentity, new OCPPChargePoint());
                                _gatewayClient.ChargePoints[request.chargeBoxIdentity].ID = request.chargeBoxIdentity;
                            }

                            // find the transaction
                            KeyValuePair<int, Connector>[] connectorArray = _gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors.ToArray();
                            for (int i = 0; i < connectorArray.Length; i++)
                            {
                                if (_gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors[connectorArray[i].Key].CurrentTransactions.ContainsKey(request.transactionId))
                                {
                                    _gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors[connectorArray[i].Key].CurrentTransactions[request.transactionId].MeterValueFinish = request.meterStop;
                                    _gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors[connectorArray[i].Key].CurrentTransactions[request.transactionId].StopTime = request.timestamp;
                                    break;
                                }
                            }

                            IdTagInfo info = new IdTagInfo
                            {
                                expiryDateSpecified = false,
                                status = AuthorizationStatus.Accepted
                            };

                            StopTransactionResponse response = new StopTransactionResponse(info);
                            responsePayload = new ResponsePayload(requestPayload.UniqueId, response);
                            break;
                        }
                        case "StatusNotification":
                        {
                            StatusNotificationRequest request = requestPayload.Payload.ToObject<StatusNotificationRequest>();

                            Console.WriteLine("Chargepoint " + request.chargeBoxIdentity + " and connector " + request.connectorId + " status#: " + request.status.ToString());

                            if (!_gatewayClient.ChargePoints.ContainsKey(request.chargeBoxIdentity))
                            {
                                _gatewayClient.ChargePoints.TryAdd(request.chargeBoxIdentity, new OCPPChargePoint());
                                _gatewayClient.ChargePoints[request.chargeBoxIdentity].ID = request.chargeBoxIdentity;
                            }

                            if (!_gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors.ContainsKey(request.connectorId))
                            {
                                _gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors.TryAdd(request.connectorId, new Connector(request.connectorId));
                            }

                            _gatewayClient.ChargePoints[request.chargeBoxIdentity].ID = request.chargeBoxIdentity;
                            _gatewayClient.ChargePoints[request.chargeBoxIdentity].Connectors[request.connectorId].Status = request.status.ToString();

                            StatusNotificationResponse response = new StatusNotificationResponse();
                            responsePayload = new ResponsePayload(requestPayload.UniqueId, response);
                            break;
                        }
                        case "DataTransfer":
                        {
                            DataTransferResponse response = new DataTransferResponse(DataTransferStatus.Rejected, string.Empty);
                            responsePayload = new ResponsePayload(requestPayload.UniqueId, response);
                            break;
                        }
                        case "DiagnosticsStatusNotification":
                        {
                            DiagnosticsStatusNotificationResponse response = new DiagnosticsStatusNotificationResponse();
                            responsePayload = new ResponsePayload(requestPayload.UniqueId, response);
                            break;
                        }
                        case "FirmwareStatusNotification":
                        {
                            FirmwareStatusNotificationResponse response = new FirmwareStatusNotificationResponse();
                            responsePayload = new ResponsePayload(requestPayload.UniqueId, response);
                            break;
                        }
                        default:
                        {
                            responsePayload = new ErrorPayload(requestPayload.UniqueId, StringConstants.NotImplemented);
                            break;
                        }
                    }

                    if (responsePayload != null)
                    {
                        if (((BasePayload)responsePayload).MessageTypeId == 3)
                        {
                            ResponsePayload response = (ResponsePayload)responsePayload;
                            return Task.FromResult(response.WrappedPayload);
                        }
                        else
                        {
                            ErrorPayload error = (ErrorPayload)responsePayload;
                            return Task.FromResult(error.WrappedPayload);
                        }
                    }

                }
                else
                {
                    ErrorPayload errorPayload = new ErrorPayload(requestPayload.UniqueId);
                    GetErrorPayload(isValidPayload, errorPayload);
                    return Task.FromResult(errorPayload.WrappedPayload);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            return null;
        }

        private async Task ProcessResponsePayloadAsync(string chargepointName, ResponsePayload responsePayload)
        {
            //Placeholder to process response payloads from charger for CentralSystem initiated commands
            await Task.Delay(1000);
        }

        private async Task ProcessErrorPayloadAsync(string chargepointName, ErrorPayload errorPayload)
        {
            //Placeholder to process error payloads from charger for CentralSystem initiated commands
            await Task.Delay(1000);
        }

        private async Task RemoveConnectionsAsync(string chargepointName, WebSocket webSocket)
        {
            try
            {
                if (activeCharger.TryRemove(chargepointName, out Charger charger))
                {
                    Console.WriteLine($"Removed charger {chargepointName}");
                }
                else
                {
                    Console.WriteLine($"Cannot remove charger {chargepointName}");
                }

                await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, StringConstants.ClientRequestedClosureMessage, CancellationToken.None);
                Console.WriteLine($"Closed websocket for charger {chargepointName}. Remaining active chargers : {activeCharger.Count}");

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
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
                catch (Exception ex)
                {
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }
    }
}
