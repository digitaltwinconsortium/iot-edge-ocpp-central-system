/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

using OCPPCentralStation.schemas.dtdl;
using System.Threading.Tasks;

namespace ProtocolGateway
{
    public interface ICloudGatewayClient
    {
        Task SendTelemetryAsync(EVChargingStation telemetry);

        Task CloseAsync();
    }
}