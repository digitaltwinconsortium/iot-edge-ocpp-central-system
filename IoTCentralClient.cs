/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using OCPPCentralSystem.Schemas.DTDL;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace OCPPCentralSystem
{
    public class IoTCentralClient : ICloudGatewayClient
    {
        private ModuleClient _client;
        private Timer _trigger;

        public ConcurrentDictionary<string, OCPPChargePoint> ChargePoints { get; set; }

        public IoTCentralClient()
        {
            ChargePoints = new ConcurrentDictionary<string, OCPPChargePoint>();

            _trigger = new Timer(new TimerCallback(SendTelemetryAsync));
            int interval = 15000; // default to 15 seconds
            try
            {
                interval = int.Parse(Environment.GetEnvironmentVariable("Publishing_Interval"));
            }
            catch (Exception)
            {
                // do nothing
            }

            _trigger.Change(interval, interval);
        }

        private async void SendTelemetryAsync(object state)
        {
            try
            {
                string serializedMessage = JsonConvert.SerializeObject(ChargePoints);

                if (_client == null)
                {
                    _client = await ModuleClient.CreateFromEnvironmentAsync().ConfigureAwait(false);
                }

                await _client.SendEventAsync(new Message(Encoding.UTF8.GetBytes(serializedMessage))).ConfigureAwait(false);
                Console.WriteLine("Sent to Central: " + serializedMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
