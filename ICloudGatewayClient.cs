/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

using OCPPCentralSystem.Schemas.DTDL;

namespace OCPPCentralSystem
{
    public interface ICloudGatewayClient
    {
        public OCPPChargePoint Telemetry { get; set; }
    }
}