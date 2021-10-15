/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

using ProtocolGateway.Models;
using System.Threading.Tasks;

namespace ProtocolGateway
{
    public interface ICloudGatewayClient
    {
        Task SendTransactionMessageAsync(RequestPayload requestPayload,string chargepointName);

        Task SendTelemetryAsync(object requestPayload,string chargepointName);

        Task CloseAsync();
    }
}