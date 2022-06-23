using System;

namespace troublemaker.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class RconCommandAttribute : Attribute
{
    public string Name { get; private set; } = "";
    public string Description { get; init; } = "";
    public string Usage { get; init; } = "";

    public RconCommandAttribute(string Name) {
        this.Name = Name;
    }
}