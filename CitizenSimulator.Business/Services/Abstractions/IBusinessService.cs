using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Property.Components;

namespace CitizenSimulator.Business.Services.Abstractions;

public interface IBusinessService
{
    bool CreateBusiness(ICommandBuffer commands, string typeId, string name, CitizenComponent owner, PropertyComponent property, 
        out BusinessComponent? outBusiness);
}