using System;
using HarmonyLib;
using ProjectM;
using Il2CppSystem.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Linq;

using OList = System.Collections.Generic.List<System.Object>;
using System.Threading;

namespace troublemaker;

[HarmonyPatch(typeof(RconListenerSystem))]
public class RconListenerSystem_Patches
{
    [HarmonyPostfix]
    [HarmonyPatch("OnCreate")]
    public static unsafe void OnCreate(RconListenerSystem __instance)
    {
        if (!SettingsManager.ServerHostSettings.Rcon.Enabled)
            return;

        foreach (var command in VRcon.GetExecutors())
        {
            if (command.executeMethod == null) continue;


            var method = command.executeMethod;
            var parameters = method.GetParameters();

            foreach (var param in parameters)
            {
                if (param.ParameterType.IsArray && param.Position != parameters.Count() - 1)
                {
                    throw new Exception($"Arrays can only be the last argument for {method.Name}");
                }
            }

            __instance._Server.CommandManager.Add(command.Name, command.Usage, command.Description, new Func<string, IList<string>, string>((cmd, _args) =>
            {
                var args = _args.Cast<List<string>>();
                System.Collections.Generic.List<string> values = new System.Collections.Generic.List<string>();
                foreach (var arg in args) values.Add(arg);
                Plugin.Logger?.LogInfo(command.Name + " " + string.Join(" ", values));

                OList paramValues = new OList();

                foreach (var param in parameters)
                {
                    if (param.ParameterType.IsArray)
                    {
                        OList subArray = new OList();
                        foreach (var val in values)
                        {
                            subArray.Add(SmartCast(param.ParameterType.GetElementType(), val));
                        }
                        paramValues.Add(SmartArrayCast(param.ParameterType.GetElementType(), subArray.ToArray()));
                    }
                    else
                    {
                        if (values.Count() == 0)
                        {
                            paramValues.Add(param.DefaultValue);
                            continue;
                        }
                        paramValues.Add(SmartCast(param.ParameterType, values.Shift()));
                    }
                }

                var a = Troublemaker.Instance?.queue(new Func<string>(() =>
                {
                    try
                    {
                        return command.executeMethod?.Invoke(command.commandContainer, paramValues.ToArray()).ToString() ?? "";
                    }
                    catch (Exception e)
                    {
                        Plugin.Logger?.LogError(e.ToString());
                        return command.Usage;
                    }
                }));

                var timeout = 500;
                while (timeout > 0)
                {
                    if (a?.Response != null)
                        return a.Response;
                    Thread.Sleep(1);
                    timeout--;
                }

                return "Timeout";
            }));
        }
    }

    private static Array SmartArrayCast(Type t, object[] input)
    {
        Array destinationArray = Array.CreateInstance(t, input.Length);
        Array.Copy(input, destinationArray, input.Length);

        return destinationArray;
    }

    private static object SmartCast(Type t, string input)
    {
        try
        {
            if (t == typeof(string))
            {
                return input;
            }
            else if (t == typeof(int))
            {
                return int.Parse(input);
            }
            else if (t == typeof(float))
            {
                return float.Parse(input);
            }
            else if (t == typeof(bool))
            {
                return bool.Parse(input);
            }
            else if (t == typeof(double))
            {
                return double.Parse(input);
            }
            else if (t == typeof(long))
            {
                return long.Parse(input);
            }
            else if (t == typeof(ulong))
            {
                return ulong.Parse(input);
            }
            else if (t == typeof(short))
            {
                return short.Parse(input);
            }
            else if (t == typeof(ushort))
            {
                return ushort.Parse(input);
            }
            else if (t == typeof(byte))
            {
                return byte.Parse(input);
            }
            else if (t == typeof(sbyte))
            {
                return sbyte.Parse(input);
            }
            else if (t == typeof(char))
            {
                return char.Parse(input);
            }
            else if (t == typeof(decimal))
            {
                return decimal.Parse(input);
            }
            else
            {
                Plugin.Logger?.LogWarning("Unable to auto-cast " + input + " to " + t.Name);
                return input;
            }
        }
        catch (Exception)
        {
            Plugin.Logger?.LogError("Unable to auto-cast " + input + " to " + t.Name);
            return input;
        }
    }


}