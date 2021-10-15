﻿using OCPP16;
using System;

namespace OCPPCentralStation.Controllers
{
    public class SOAPMiddlewareOCPP16 : I_OCPP_CentralSystemService_16
    {
        public AuthorizeResponse Authorize(AuthorizeRequest request)
        {
            IdTagInfo info = new IdTagInfo
            {
                expiryDateSpecified = false,
                status = AuthorizationStatus.Accepted
            };

            return new AuthorizeResponse(info);
        }

        public BootNotificationResponse BootNotification(BootNotificationRequest request)
        {
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
            throw new NotImplementedException();
        }

        public MeterValuesResponse MeterValues(MeterValuesRequest request)
        {
            throw new NotImplementedException();
        }

        public StartTransactionResponse StartTransaction(StartTransactionRequest request)
        {
            throw new NotImplementedException();
        }

        public StatusNotificationResponse StatusNotification(StatusNotificationRequest request)
        {
            throw new NotImplementedException();
        }

        public StopTransactionResponse StopTransaction(StopTransactionRequest request)
        {
            throw new NotImplementedException();
        }
    }
}