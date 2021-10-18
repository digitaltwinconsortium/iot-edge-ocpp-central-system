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

            _gatewayClient.ChargePoint.ID = request.chargeBoxIdentity;

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

            if (!_gatewayClient.ChargePoint.Connectors.ContainsKey(request.connectorId))
            {
                _gatewayClient.ChargePoint.Connectors.TryAdd(request.connectorId, new Connector(request.connectorId));
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

                        _gatewayClient.ChargePoint.Connectors[request.connectorId].MeterReadings.Add(reading);

                        // only keep the last 10 meter readings
                        if (_gatewayClient.ChargePoint.Connectors[request.connectorId].MeterReadings.Count > 10)
                        {
                            _gatewayClient.ChargePoint.Connectors[request.connectorId].MeterReadings.RemoveAt(0);
                        }
                    }
                }
            }

            return new MeterValuesResponse();
        }

        public StartTransactionResponse StartTransaction(StartTransactionRequest request)
        {
            Console.WriteLine("Start transaction " + _transactionNumber.ToString() + " from " + request.timestamp + " on chargepoint " + request.chargeBoxIdentity + " on connector " + request.connectorId + " with badge ID " + request.idTag + " and meter reading at start " + request.meterStart);

            if (!_gatewayClient.ChargePoint.Connectors.ContainsKey(request.connectorId))
            {
                _gatewayClient.ChargePoint.Connectors.TryAdd(request.connectorId, new Connector(request.connectorId));
            }

            _transactionNumber++;
            Transaction transaction = new Transaction(_transactionNumber)
            {
                BadgeID = request.idTag,
                StartTime = request.timestamp,
                MeterValueStart = request.meterStart
            };

            // only add if the transaction doesn't exist yet
            if (!_gatewayClient.ChargePoint.Connectors[request.connectorId].CurrentTransactions.ContainsKey(_transactionNumber))
            {
                _gatewayClient.ChargePoint.Connectors[request.connectorId].CurrentTransactions.TryAdd(_transactionNumber, transaction);
            }

            // remove all transactions that have completed and are more than a day old
            KeyValuePair<int, Transaction>[] transactionsArray = _gatewayClient.ChargePoint.Connectors[request.connectorId].CurrentTransactions.ToArray();
            for (int i = 0; i < transactionsArray.Length; i++)
            {
                if ((transactionsArray[i].Value.StopTime != DateTime.MinValue) && (transactionsArray[i].Value.StopTime < DateTime.UtcNow.Subtract(TimeSpan.FromDays(1))))
                {
                    _gatewayClient.ChargePoint.Connectors[request.connectorId].CurrentTransactions.TryRemove(transactionsArray[i].Key, out _);
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

            // find the transaction
            KeyValuePair<int, Connector>[] connectorArray = _gatewayClient.ChargePoint.Connectors.ToArray();
            for (int i = 0; i < connectorArray.Length; i++)
            {
                if (_gatewayClient.ChargePoint.Connectors[connectorArray[i].Key].CurrentTransactions.ContainsKey(request.transactionId))
                {
                    _gatewayClient.ChargePoint.Connectors[connectorArray[i].Key].CurrentTransactions[request.transactionId].MeterValueFinish = request.meterStop;
                    _gatewayClient.ChargePoint.Connectors[connectorArray[i].Key].CurrentTransactions[request.transactionId].StopTime = request.timestamp;
                    break;
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

            if (!_gatewayClient.ChargePoint.Connectors.ContainsKey(request.connectorId))
            {
                _gatewayClient.ChargePoint.Connectors.TryAdd(request.connectorId, new Connector(request.connectorId));
            }

            _gatewayClient.ChargePoint.ID = request.chargeBoxIdentity;
            _gatewayClient.ChargePoint.Connectors[request.connectorId].Status = request.status.ToString();

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
