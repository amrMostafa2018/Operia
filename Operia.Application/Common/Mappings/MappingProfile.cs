using System.Reflection;
using AutoMapper;

namespace Operia.Application.Common.Mappings;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        ApplyMappingsFromAssembly(Assembly.GetExecutingAssembly());
    }

    private void ApplyMappingsFromAssembly(Assembly assembly)
    {
        var mapFromTypes = assembly.GetExportedTypes()
            .Where(type => type.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMapFrom<>)))
            .ToList();

        foreach (var type in mapFromTypes)
        {
            var mappingMethod = type.GetMethod(
                "Mapping",
                BindingFlags.Public | BindingFlags.Static);

            mappingMethod?.Invoke(null, [this]);
        }
    }
}
