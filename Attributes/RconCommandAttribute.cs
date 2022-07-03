using System;

namespace troublemaker.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class RconCommandAttribute : Attribute
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Usage { get; set; } = "";

    public RconCommandAttribute(string Name) {
        this.Name = Name;
    }
}