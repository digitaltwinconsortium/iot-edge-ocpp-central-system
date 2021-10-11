using OCPPCentralStation.Models;
using System;
using System.Xml.Linq;

namespace OCPPCentralStation.Controllers
{
    public class CSService : ICSService
    {
            public string Test(string s)
            {
                Console.WriteLine("Test Method Executed!");
                return s;
            }

            public void XmlMethod(XElement xml)
            {
                Console.WriteLine(xml.ToString());
            }

            public WeatherForecast TestCustomModel(WeatherForecast customModel)
            {
                return customModel;
            }
    }
}
