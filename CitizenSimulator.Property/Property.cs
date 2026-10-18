using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Property.Services;
using CitizenSimulator.Property.Services.Abstractions;

namespace CitizenSimulator.Property;

[Mod("property", "Property", "1.0.0", Dependencies = new[] { "citizen", "calender" })]
public class Property : IMod
{
    public void OnLoad(IModContext context)
    {
        PropertyService propertyService = new PropertyService(context.Store, context.Events);
        
        context.Services.Register<IPropertyService>(propertyService);
        context.Systems.Register(new PropertySystem(context.Events, propertyService));
    }
}