using Wolfpack.Core.Interfaces.Entities;
using Wolfpack.Core.WebServices.Interfaces.Entities;
using Wolfpack.Core.WebServices.Interfaces.Messages;

namespace Wolfpack.Core.WebServices.Interfaces
{
    public interface IWolfpackWebServicesClient
    {
        StatusResponse GetStatus(); 
        RestConfigurationCatalogue GetCatalogue(GetConfigurationCatalogue request);
        ConfigurationCommandResponse Update(ConfigurationEntry request);
        NotificationEventResponse Deliver(NotificationEvent notification);
        //ConfigurationDeleteResponse Delete(ConfigurationDelete request);
    }
}