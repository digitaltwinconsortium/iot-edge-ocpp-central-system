/*****************************************************************************************************************
 * Author: Ajantha Dhanasekaran
 * Date: 07-Sept-2020
 * Purpose: Model to hold meter values details
 * Change History:
 * Name                         Date                    Change description
 * Ajantha Dhanasekaran         07-Sept-2020              Initial version
 * Ajantha Dhanasekaran         07-Sept-2020              Removed unwanted methods
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

using Newtonsoft.Json;
using System;

namespace ChargePointOperator.Models.Internal
{
    public class MeterValueRequest
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Wh { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string W { get; set; }
        
        public string StationChargerId { get; set; }
        
        public DateTime Capturetime { get; set; }
        
        public int ConnectorId { get; set; }

        public MeterValueRequest(SampledValue sampledValue, string chargingpointId, int connectorId)
        {
            StationChargerId = chargingpointId;
            ConnectorId = connectorId;
            Capturetime = DateTime.UtcNow;

            switch (sampledValue.unit)
            {
                case UnitOfMeasure.W:
                    W = sampledValue.value;
                    break;

                case UnitOfMeasure.Wh:
                    Wh = sampledValue.value;
                    break;

                case UnitOfMeasure.kWh:
                    Wh = (decimal.Parse(sampledValue.value) * 1000).ToString();
                    break;

                case UnitOfMeasure.kW:
                    W = (decimal.Parse(sampledValue.value) * 1000).ToString();
                    break;

                default:
                    break;
            }
        }
    }
}
