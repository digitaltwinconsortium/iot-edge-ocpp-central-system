/*****************************************************************************************************************
 * Author: Ajantha Dhanasekaran
 * Date: 07-Sept-2020
 * Purpose: Model to hold string constants use in the ChargerPointOperator
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

namespace ChargePointOperator.Models
{
    public class StringConstants
    {
        public static string NotImplemented = "NotImplmented";
        public static string RequestPath = "/ocpp";
        public static string RequiredProtocol = "ocpp1.6";
        public static string NoProtocolHeaderMessage = "No sub-protocol header";
        public static string SubProtocolNotSupportedMessage = "Sub-protocol not supported";
        public static string ClientInitiatedNewWebsocketMessage = "Client sent new websocket request";
        public static string ChargerNewWebRequestMessage = "New websocket request received for this charger";
        public static string RequestContentFormat = "application/json";
        public static string StationChargerTag = "StationChargerId";
        public static string ClientRequestedClosureMessage = "Client requested closure";
        public static string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    }
}