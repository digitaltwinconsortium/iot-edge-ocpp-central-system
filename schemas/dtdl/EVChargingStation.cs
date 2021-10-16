/*
Copyright 2021 Microsoft Corporation
*/

using System;
using System.Collections.Generic;

namespace OCPPCentralStation.schemas.dtdl
{
    public class EVChargingStation
    {
        public EVChargingStation()
        {
            // init data
            ID = string.Empty;
            Status = string.Empty;
            Connectors = new List<Connector>();
        }

        public string ID { get; set; }

        public string Status { get; set; }

        public List<Connector> Connectors { get; set; }
    }

    public class Connector
    {
        public Connector()
        {
            // init data
            ID = string.Empty;
            MeterValue = 0;
            MeterValueUnit = "Wh";
            CurrentTransactions = new List<Transaction>();
        }

        public string ID { get; set; }

        public int MeterValue { get; set; }

        public string MeterValueUnit { get; set; }

        public List<Transaction> CurrentTransactions { get; set; }
    }

    public class Transaction
    {
        public Transaction()
        {
            // init data
            ID = string.Empty;
            BadgeID = string.Empty;
            StartTime = DateTime.Now;
            MeterValueStart = 0;
        }

        public string ID { get; set; }

        public string BadgeID { get; set; }

        public DateTime StartTime { get; set; }

        public int MeterValueStart { get; set; }
    }
}
