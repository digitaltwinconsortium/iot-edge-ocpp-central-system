﻿using OCPP15;
using System;
using System.Collections.Generic;

namespace OCPPCentralStation.Controllers
{
    public class SOAPMiddlewareOCPP15 : I_OCPP_CentralSystemService_15
    {
        private List<int> _currentTransactions = new List<int>();
        private int _transactionNumber = 0;

        public AuthorizeResponse Authorize(AuthorizeRequest request)
        {
            Console.WriteLine("Authorization requested on chargepoint " + request.chargeBoxIdentity + "  and badge ID " + request.idTag);

            IdTagInfo info = new IdTagInfo
            {
                expiryDateSpecified = false,
                status = AuthorizationStatus.Accepted
            };

            return new AuthorizeResponse(info);
        }

        public BootNotificationResponse BootNotification(BootNotificationRequest request)
        {
            Console.WriteLine("Chargepoint with identity: " + request.chargeBoxIdentity + " booted! Chargepoint#: " + request.chargePointSerialNumber);

            return new BootNotificationResponse(RegistrationStatus.Accepted, DateTime.Now, 60);
        }

        public DataTransferResponse DataTransfer(DataTransferRequest request)
        {
            throw new NotImplementedException();
        }

        public DiagnosticsStatusNotificationResponse DiagnosticsStatusNotification(DiagnosticsStatusNotificationRequest request)
        {
            throw new NotImplementedException();
        }

        public FirmwareStatusNotificationResponse FirmwareStatusNotification(FirmwareStatusNotificationRequest request)
        {
            throw new NotImplementedException();
        }

        public HeartbeatResponse Heartbeat(HeartbeatRequest request)
        {
            Console.WriteLine("Heartbeat received from: " + request.chargeBoxIdentity);

            return new HeartbeatResponse(DateTime.Now);
        }

        public MeterValuesResponse MeterValues(MeterValuesRequest request)
        {
            Console.WriteLine("Meter values for transaction " + request.transactionId + " on chargepoint " + request.chargeBoxIdentity + ":");

            foreach (MeterValue meterValue in request.values)
            {
                foreach (MeterValueValue meterValueValue in meterValue.value)
                {
                    Console.WriteLine("Value: " + meterValueValue.Value + " " + meterValueValue.unit.ToString());
                }
            }

            return new MeterValuesResponse();
        }

        public StartTransactionResponse StartTransaction(StartTransactionRequest request)
        {
            _transactionNumber++;
            _currentTransactions.Add(_transactionNumber);

            Console.WriteLine("Start transaction " + _transactionNumber.ToString() + " from " + request.timestamp + " on chargepoint " + request.chargeBoxIdentity + " on connector " + request.connectorId + " with badge ID " + request.idTag + " and meter reading at start " + request.meterStart);

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

            IdTagInfo info = new IdTagInfo
            {
                expiryDateSpecified = false,
                status = AuthorizationStatus.Accepted
            };

            _currentTransactions.Remove(request.transactionId);

            return new StopTransactionResponse(info);
        }

        public StatusNotificationResponse StatusNotification(StatusNotificationRequest request)
        {
            Console.WriteLine("Chargepoint " + request.chargeBoxIdentity + " and connector " + request.connectorId + " status#: " + request.status.ToString());

            return new StatusNotificationResponse();
        }
    }
}