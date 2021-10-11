/*****************************************************************************************************************
 * Author: Ajantha Dhanasekaran
 * Date: 07-Sept-2020
 * Purpose: Model to hold OCPP error payload
 * Change History:
 * Name                         Date                    Change description
 * Ajantha Dhanasekaran         07-Sept-2020              Initial version
 * Ajantha Dhanasekaran         15-Sept-2020              Removed unnecessary constructors
 * ****************************************************************************************************************/


#region license

/*
Cognizant EV Charging Protocol Gateway 1.0
© 2020 Cognizant. All rights reserved.
"Cognizant EV Charging Protocol Gateway 1.0" by Cognizant  is licensed under Apache License Version 2.0


Copyright 2020 Cognizant


Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at


    http://www.apache.org/licenses/LICENSE-2.0


Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

#endregion

using Newtonsoft.Json.Linq;

namespace  ProtocolGateway.Models
{
     public class ErrorPayload : BasePayload
    {
        public string ErrorCode { get; set; }
        public string ErrorDescription { get; set; }
        public new JArray WrappedPayload => new JArray() { MessageTypeId, UniqueId, ErrorCode, ErrorDescription, Payload };

        public ErrorPayload(string uniqueId)
        {
            MessageTypeId = 4;
            UniqueId = uniqueId;
        }

       


        public ErrorPayload(string uniqueId,string errorCode)
        {
            MessageTypeId = 4;
            UniqueId = uniqueId;
            ErrorCode = errorCode;
            ErrorDescription = "";
            Payload = JObject.FromObject("");
        }

        public ErrorPayload(JArray payload)
        {
            MessageTypeId = int.Parse(payload[0].ToString());
            UniqueId = payload[1].ToString();
            ErrorCode = payload[2].ToString();
            ErrorDescription = payload[3].ToString();
            Payload = (JObject)payload[4];
        }

    }

}