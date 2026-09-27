// ---------------------------------------------------------------------------
// LOCAL BUILD/TEST SHIM — NOT THE REAL SDK.
//
// This reproduces the public attribute surface of the official
// "OutSystems.ExternalLibraries.SDK" NuGet package (namespace, attribute
// names, and the properties documented at
// https://success.outsystems.com/.../external_libraries_sdk_readme/ and
// .../external_libraries_sdk_reference/) so the connector project can be
// compiled and unit-tested in an environment without access to nuget.org.
//
// It is used ONLY by build-shim/*.csproj, which the real
// OutSystems.JevConnector.csproj does NOT reference — that project references
// the genuine "OutSystems.ExternalLibraries.SDK" NuGet package. Do not upload
// anything built against this shim to the ODC Portal; build with the real
// package (`dotnet restore` on a machine with normal internet access) first.
// ---------------------------------------------------------------------------

namespace OutSystems.ExternalLibraries.SDK;

[AttributeUsage(AttributeTargets.Interface)]
public sealed class OSInterfaceAttribute : Attribute
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? IconResourceName { get; set; }
    public string? OriginalName { get; set; }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class OSActionAttribute : Attribute
{
    public string? Description { get; set; }
    public string? IconResourceName { get; set; }
    public object? ReturnType { get; set; }
    public string? ReturnName { get; set; }
    public string? OriginalName { get; set; }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class OSParameterAttribute : Attribute
{
    public object? DataType { get; set; }
    public string? Description { get; set; }
    public string? OriginalName { get; set; }

    public OSParameterAttribute()
    {
    }

    public OSParameterAttribute(string description)
    {
        Description = description;
    }
}

[AttributeUsage(AttributeTargets.Struct)]
public sealed class OSStructureAttribute : Attribute
{
    public string? Description { get; set; }
    public string? OriginalName { get; set; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class OSStructureFieldAttribute : Attribute
{
    public object? DataType { get; set; }
    public string? Description { get; set; }
    public int Length { get; set; }
    public int Decimals { get; set; }
    public bool IsMandatory { get; set; }
    public string? OriginalName { get; set; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class OSIgnoreAttribute : Attribute
{
}
