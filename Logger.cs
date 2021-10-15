/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

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
