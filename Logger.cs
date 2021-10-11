/*****************************************************************************************************************
 * Author: Ajantha Dhanasekaran
 * Date: 16-Sept-2020
 * Purpose: Common logger to log all error,debug,trace,informaiton & warnings using NLog. 
 * Change History:
 * Name                         Date                    Change description
 * Ajantha Dhanasekaran         16-Sept-2020              Initial version
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

using System;

namespace ProtocolGateway
{
    public class Logger
    {
        private readonly static NLog.Logger _logger;

        static Logger()
        {
            _logger = NLog.LogManager.GetCurrentClassLogger();
        }

        public Logger()
        {
            
        }

        public void LogError(string chargepointName,string methodName,Exception exception)
        {
            _logger.Error($"Error occured in {methodName} for {chargepointName}. Error : {exception.Message}");
        }

        public void LogError(string chargepointName,string methodName,string message)
        {
            _logger.Error($"Error occured in {methodName} for {chargepointName}. Error : {message}");
        }

        public void LogError(string methodName,Exception exception)
        {
            _logger.Error($"Error occured in {methodName}. Error : {exception.Message}");
        }

        public void LogInformation(string information)
        {
            _logger.Info(information);
        }

        public void LogTrace(string trace)
        {
            _logger.Trace(trace);
        }

        public void LogDebug(string debug)
        {
            _logger.Debug(debug);
        }

        public void LogWarning(string warning)
        {
            _logger.Warn(warning);
        }
    }
    
}