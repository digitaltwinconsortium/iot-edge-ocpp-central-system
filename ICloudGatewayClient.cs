/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

using OCPPCentralSystem.Schemas.DTDL;
using System.Collections.Concurrent;

namespace OCPPCentralSystem
{
    public interface ICloudGatewayClient
    {
        public ConcurrentDictionary<string, OCPPChargePoint> ChargePoints { get; set; }
    }
}