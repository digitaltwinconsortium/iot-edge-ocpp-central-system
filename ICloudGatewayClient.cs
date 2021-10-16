/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

using OCPPCentralStation.schemas.dtdl;

namespace ProtocolGateway
{
    public interface ICloudGatewayClient
    {
        public EVChargingStation Telemetry { get; set; }
    }
}