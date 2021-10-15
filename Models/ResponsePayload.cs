/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

using Newtonsoft.Json.Linq;

namespace ProtocolGateway.Models
{
    public class ResponsePayload : BasePayload
    {
        public ResponsePayload(string uniqueId, object payload)
        {
            MessageTypeId = 3;
            UniqueId = uniqueId;
            Payload = JObject.FromObject(payload);
            WrappedPayload = new JArray { MessageTypeId, UniqueId, Payload };
        }

        public ResponsePayload(JArray payload)
        {
            MessageTypeId = int.Parse(payload[0].ToString());
            UniqueId = payload[1].ToString();
            Payload = (JObject)payload[2];
        }
    }
}
