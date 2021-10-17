/*
Copyright 2021 Microsoft Corporation
*/

using OCPPCentralSystem.Schemas.DTDL;
using OCPPCentralSystem.Schemas.OCPP16;
using System;
using System.Collections.Generic;

namespace OCPPCentralSystem.Controllers
{
    public class SOAPMiddlewareOCPP16 : I_OCPP_CentralSystemService_16
    {
        private int _transactionNumber = 0;
        private ICloudGatewayClient _gatewayClient;

        public SOAPMiddlewareOCPP16(ICloudGatewayClient gatewayClient)
        {
            _gatewayClient = gatewayClient;
        }

        public AuthorizeResponse Authorize(AuthorizeRequest request)
        {
            Console.WriteLine("Authorization requested on chargepoint " + request.chargeBoxIdentity + "  and badge ID " + request.idTag);

            // always authorize any badge for now
            IdTagInfo info = new IdTagInfo
            {
                expiryDateSpecified = false,
                status = AuthorizationStatus.Accepted
            };

            return new AuthorizeResponse(info);
        }

        public BootNotificationResponse BootNotification(BootNotificationRequest request)
        {
            Console.WriteLine("Chargepoint with identity: " + request.chargeBoxIdentity + " booted!");
            _gatewayClient.Telemetry.ID = request.chargeBoxIdentity;
            _gatewayClient.Telemetry.Status = ChargePointStatus.Available.ToString();

            return new BootNotificationResponse(RegistrationStatus.Accepted, DateTime.UtcNow, 60);
        }

        public HeartbeatResponse Heartbeat(HeartbeatRequest request)
        {
            Console.WriteLine("Heartbeat received from: " + request.chargeBoxIdentity);

            return new HeartbeatResponse(DateTime.UtcNow);
        }

        public MeterValuesResponse MeterValues(MeterValuesRequest request)
        {
            Console.WriteLine("Meter values for connector ID " + request.connectorId + " on chargepoint " + request.chargeBoxIdentity + ":");

            if (!_gatewayClient.Telemetry.Connectors.ContainsKey(request.connectorId))
            {
                _gatewayClient.Telemetry.Connectors.Add(request.connectorId, new Connector(request.connectorId));
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
                        _gatewayClient.Telemetry.Connectors[request.connectorId].MeterReadings.Add(reading);
                        if (_gatewayClient.Telemetry.Connectors[request.connectorId].MeterReadings.Count > 10)
                        {
                            _gatewayClient.Telemetry.Connectors[request.connectorId].MeterReadings.RemoveAt(0);
                        }
                    }
                }
            }

            return new MeterValuesResponse();
        }

        public StartTransactionResponse StartTransaction(StartTransactionRequest request)
        {
            Console.WriteLine("Start transaction " + _transactionNumber.ToString() + " from " + request.timestamp + " on chargepoint " + request.chargeBoxIdentity + " on connector " + request.connectorId + " with badge ID " + request.idTag + " and meter reading at start " + request.meterStart);
            if (!_gatewayClient.Telemetry.Connectors.ContainsKey(request.connectorId))
            {
                _gatewayClient.Telemetry.Connectors.Add(request.connectorId, new Connector(request.connectorId));
            }
            _transactionNumber++;
            Transaction transaction = new Transaction(_transactionNumber)
            {
                BadgeID = request.idTag,
                StartTime = request.timestamp,
                MeterValueStart = request.meterStart
            };

            if (!_gatewayClient.Telemetry.Connectors[request.connectorId].CurrentTransactions.ContainsKey(_transactionNumber))
            {
                _gatewayClient.Telemetry.Connectors[request.connectorId].CurrentTransactions.TryAdd(_transactionNumber, transaction);
            }

            KeyValuePair<int, Transaction>[] transactionsArray = _gatewayClient.Telemetry.Connectors[request.connectorId].CurrentTransactions.ToArray();
            for (int i = 0; i < transactionsArray.Length; i++)
            {
                if ((transactionsArray[i].Value.StopTime != DateTime.MinValue) && (transactionsArray[i].Value.StopTime < DateTime.UtcNow.Subtract(TimeSpan.FromDays(1))))
                {
                    _gatewayClient.Telemetry.Connectors[request.connectorId].CurrentTransactions.TryRemove(transactionsArray[i].Key, out _);
                }
            }

            IdTagInfo info = new IdTagInfo
            {
                expiryDateSpecified = false,
                status = AuthorizationStatus.Accepted
            };

            return new StartTransactionResponse(_transactionNumber, info);
        }

        public StopTransactionResponse StopTransaction(StopTransactionRequest request)
        {
            Console.WriteLine("Stop transaction " + request.transactionId.ToString() + " from " + request.timestamp + " on chargepoint " + request.chargeBoxIdentity + " with badge ID " + request.idTag + " and meter reading at stop " + request.meterStop);

            for (int i = 0; i < _gatewayClient.Telemetry.Connectors.Count; i++)
            {
                for (int j = 0; j < _gatewayClient.Telemetry.Connectors[i].CurrentTransactions.Count; j++)
                {
                    if (_gatewayClient.Telemetry.Connectors[i].CurrentTransactions[j].ID == request.transactionId)
                    {
                        _gatewayClient.Telemetry.Connectors[i].CurrentTransactions[j].MeterValueFinish = request.meterStop;
                        _gatewayClient.Telemetry.Connectors[i].CurrentTransactions[j].StopTime = request.timestamp;
                        break;
                    }
                }
            }

            IdTagInfo info = new IdTagInfo
            {
                expiryDateSpecified = false,
                status = AuthorizationStatus.Accepted
            };

            return new StopTransactionResponse(info);
        }

        public StatusNotificationResponse StatusNotification(StatusNotificationRequest request)
        {
            Console.WriteLine("Chargepoint " + request.chargeBoxIdentity + " and connector " + request.connectorId + " status#: " + request.status.ToString());

            _gatewayClient.Telemetry.ID = request.chargeBoxIdentity;
            _gatewayClient.Telemetry.Status = request.status.ToString();

            return new StatusNotificationResponse();
        }

        public DataTransferResponse DataTransfer(DataTransferRequest request)
        {
            return new DataTransferResponse(DataTransferStatus.Rejected, string.Empty);
        }

        public DiagnosticsStatusNotificationResponse DiagnosticsStatusNotification(DiagnosticsStatusNotificationRequest request)
        {
            return new DiagnosticsStatusNotificationResponse();
        }

        public FirmwareStatusNotificationResponse FirmwareStatusNotification(FirmwareStatusNotificationRequest request)
        {
            return new FirmwareStatusNotificationResponse();
        }
    }
}
