using System.ServiceModel;

namespace OCPPCentralStation.Models
{
    [ServiceContract]
    public interface ICSService
    {
        [OperationContract]
        string Test(string s);

        [OperationContract]
        void XmlMethod(System.Xml.Linq.XElement xml);
        
        [OperationContract]
        WeatherForecast TestCustomModel(WeatherForecast inputModel);
    }
}

