/*
Copyright 2021 Microsoft Corporation
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OCPPCentralSystem.Schemas.DTDL
{
    public class OCPPChargePoint
    {
        public OCPPChargePoint()
        {
            // init data
            ID = string.Empty;
            Status = string.Empty;
            Connectors = new Dictionary<int, Connector>();
        }

        public string ID { get; set; }

        public string Status { get; set; }

        public Dictionary<int, Connector> Connectors { get; set; }
    }

    public class Connector
    {
        public Connector(int id)
        {
            // init data
            ID = id;
            MeterReadings = new List<MeterReading>();
            CurrentTransactions = new ConcurrentDictionary<int, Transaction>();
        }

        public int ID { get; set; }

        public List<MeterReading> MeterReadings { get; set; }

        public ConcurrentDictionary<int, Transaction> CurrentTransactions { get; set; }
    }

    public class MeterReading
    {
        public MeterReading()
        {
            // init data
            MeterValue = -1;
            MeterValueUnit = "Wh";
            Timestamp = DateTime.UtcNow;
        }

        public int MeterValue { get; set; }

        public string MeterValueUnit { get; set; }

        public DateTime Timestamp { get; set; }
    }

    public class Transaction
    {
        public Transaction(int id)
        {
            // init data
            ID = id;
            BadgeID = string.Empty;
            StartTime = DateTime.UtcNow;
            StopTime = DateTime.MinValue;
            MeterValueStart = -1;
            MeterValueFinish = -1;
        }

        public int ID { get; set; }

        public string BadgeID { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime StopTime { get; set; }

        public int MeterValueStart { get; set; }

        public int MeterValueFinish { get; set; }
    }
}
