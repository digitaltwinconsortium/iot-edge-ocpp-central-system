/*****************************************************************************************************************
 * Author: Ajantha Dhanasekaran
 * Date: 07-Sept-2020
 * Purpose: Enum for  OCPP StatusErrorCode
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

namespace ChargePointOperator.Models.OCPP.Enums
{
    public enum ChargerErrorCode
    {
        ConnectorLockFailure=1,
        EVCommunicationError=2,
        GroundFailure=3,
        HighTemperature=4,
        InternalError=5,
        LocalListConflict=6,
        NoError=7,
        OtherError=8,
        OverCurrentFailure=9,
        OverVoltage=10,
        PowerMeterFailure=11,
        PowerSwitchFailure=12,
        ReaderFailure=13,
        ResetFailure=14,
        UnderVoltage=15,
        WeakSignal=16,
        Available=17,
        Preparing=18,
        Charging=19,
        SuspendedEVSE=20,
        SuspendedEV=21,
        Finishing=22,
        Reserved=23,
        Unavailable=24,
        Faulted=25
    }
}
