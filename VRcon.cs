using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ProjectM.Network;
using troublemaker.Attributes;
using UnhollowerRuntimeLib;
using Unity.Collections;
using Unity.Entities;
using Wetstone.API;

namespace troublemaker;

internal class RconMessage
{
    public ulong RconId;
    public Func<string>? action;
    public string? Response;
}

internal class RconExecutor
{
    internal string Name;
    internal string Description;
    internal string Usage;

    internal object commandContainer;
    internal MethodInfo? executeMethod;

    public RconExecutor(object commandContainer, MethodInfo executeMethod)
    {
        this.commandContainer = commandContainer;
        this.executeMethod = executeMethod;

        var attribute = executeMethod.GetCustomAttribute<RconCommandAttribute>();
        Name = attribute.Name;
        Description = attribute.Description;
        Usage = attribute.Usage;
    }

    public string ToJson()
    {
        var strb = new StringBuilder();

        strb.Append($"\"{Name}\":{{");
        strb.Append($"\"description\":\"{Description}\",");
        strb.Append($"\"usage\":\"{Usage}\"");
        strb.Append("}");

        return strb.ToString();
    }
}

public static class VRcon
{
    private static List<RconExecutor> _Executors = new List<RconExecutor>();

    public static void Register(Type ty) {
        var attr = ty.GetCustomAttribute<RconHandlerAttribute>();
        if (attr == null) {
            throw new Exception($"Type {ty.Name} is not a RconHandler");
        }

        var commandContainer = Activator.CreateInstance(ty);

        var methods = ty.GetMethods();
        foreach (var method in methods) {
            var attr2 = method.GetCustomAttribute<RconCommandAttribute>();
            if (attr2 == null) {
                continue;
            }

            var command = new RconExecutor(commandContainer, method);
            _Executors.Add(command);
        }
    }

    internal static List<RconExecutor> GetExecutors()
    {
        return _Executors;
    }
}