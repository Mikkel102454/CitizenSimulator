using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Property.Components;

namespace CitizenSimulator.Business.Services;

public class BusinessService(IEntityStore store, IEventBus events) : IBusinessService
{
    public bool CreateBusiness(ICommandBuffer commands, EntityRef entityRef, string typeId, string name, CitizenComponent owner, PropertyComponent? property,
        out BusinessComponent outBusiness)
    {
        if (store.HasComponent<PropertyComponent>(entityRef))
        {
            outBusiness = null;
            return false;
        }
        
        outBusiness = commands.AddComponent(entityRef, new BusinessComponent());

        outBusiness.Name = name;
        outBusiness.Owner = owner;
        outBusiness.Property = property;
        return true;
    }
}