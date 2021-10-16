/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

using Newtonsoft.Json.Linq;

namespace ProtocolGateway.Models
{
    public class BasePayload
    {
        public int MessageTypeId { get; set; }

        public string UniqueId { get; set; }

        public JObject Payload { get; set; }

        public JArray WrappedPayload { get; set; }
    }
}
