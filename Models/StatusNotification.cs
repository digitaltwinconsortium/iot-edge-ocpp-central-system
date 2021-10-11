﻿/*****************************************************************************************************************
 * Author: Ajantha Dhanasekaran
 * Date: 07-Sept-2020
 * Purpose: Model for OCPP StatusNotification 
 * Change History:
 * Name                         Date                    Change description
 * Ajantha Dhanasekaran         07-Sept-2020              Initial version
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

using ChargePointOperator.Models.OCPP.Enums;
using Newtonsoft.Json;
using System;

namespace ChargePointOperator.Models.OCPP
{
   
        public class StatusNotification
        {
            [JsonProperty("connectorId")]
            public int ConnectorId { get; set; }
            [JsonProperty("errorCode")]
            public ChargerErrorCode ErrorCode { get; set; }
            [JsonProperty("info")]
            public string Info { get; set; }
            [JsonProperty("status")]
            public ChargerStatusType Status { get; set; }
            [JsonProperty("timestamp")]
            public DateTime Timestamp { get; set; }
            [JsonProperty("vendorId")]
            public string VendorId { get; set; }
            [JsonProperty("vendorErrorCode")]
            public string VendorErrorCode { get; set; }

        }

    
}