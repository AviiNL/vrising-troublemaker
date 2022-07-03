using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using troublemaker.Attributes;
using UnhollowerRuntimeLib;
using Unity.Entities;
using UnityEngine;

namespace troublemaker;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    public static ManualLogSource? Logger;
    
    public static World ServerWorld {
        get {
            return _serverWorld!;
        }
    }
    public static bool IsServer => Application.productName == "VRisingServer";
    
    private Harmony _myHook;
    private Component? _myInjected;
    private static World? _serverWorld;
    
    public static void Init() {
        if (IsServer) {
            _serverWorld = GetWorld("Server");
            if (_serverWorld == null) {
                Plugin.Logger?.LogError("Failed to initialize Troublemaker. World not found.");
                return;
            }
            Plugin.Logger?.LogInfo("Found server world.");
        }
    }

    public Plugin()
    {
        Logger = this.Log;
        _myHook = new Harmony(PluginInfo.PLUGIN_GUID);
    }

    public override void Load()
    {
        if (!IsServer) return;
        Logger = this.Log;

        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsDefined(typeof(RconHandlerAttribute)));

        foreach (var type in types)
        {
            VRcon.Register(type);
        }

        ClassInjector.RegisterTypeInIl2Cpp<Troublemaker>();
        _myInjected = AddComponent<Troublemaker>();

        _myHook.PatchAll();

        Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    public override bool Unload()
    {
        if (!IsServer) return true;

        this._myHook.UnpatchSelf();
        if (_myInjected != null)
            UnityEngine.Object.Destroy(_myInjected);

        return true;
    }

    private static World? GetWorld(string name)
    {
        foreach (var world in World.s_AllWorlds)
        {
            if (world.Name == name)
            {
                return world;
            }
        }

        return null;
    }
}
