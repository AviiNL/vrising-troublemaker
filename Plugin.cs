using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using troublemaker.Attributes;
using UnhollowerRuntimeLib;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Wetstone.API;
using Wetstone.Hooks;

namespace troublemaker;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("xyz.molenzwiebel.wetstone")]
[Wetstone.API.Reloadable]
public class Plugin : BasePlugin
{
    public static ManualLogSource? Logger;

    private Harmony _myHook;
    private Component? _myInjected;

    public Plugin()
    {
        Logger = this.Log;
        _myHook = new Harmony(PluginInfo.PLUGIN_GUID);
    }

    public override void Load()
    {
        if (!VWorld.IsServer) return;
        Logger = this.Log;

        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsDefined(typeof(RconHandlerAttribute)));
        
        foreach (var type in types) {
            VRcon.Register(type);
        }

        ClassInjector.RegisterTypeInIl2Cpp<Troublemaker>();
        _myInjected = AddComponent<Troublemaker>();

        Wetstone.Hooks.Chat.OnChatMessage += HandleChatMessage;

        _myHook.PatchAll();

        Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    private static void HandleChatMessage(VChatEvent ev)
    {
        if (!ev.Message.StartsWith("!")) return;
        if (ev.Cancelled) return; // ignore messages already processed by some other plugin

        ev.Cancel(); // prevent the command from showing up, since we're handling it

        if (ev.Message.StartsWith("!hp"))
        {
            var hp = float.Parse(ev.Message.Substring(4));

            ev.SenderCharacterEntity.WithComponentData((ref Health health) =>
            {
                health.Value = hp;
            });

            ev.User.SendSystemMessage("Set current HP to <color=#ffff00ff>" + hp + "</color>.");
            return;
        }

        if (ev.Message.StartsWith("!blood"))
        {
            var bl = float.Parse(ev.Message.Substring(7));

            ev.SenderCharacterEntity.WithComponentData((ref Blood blood) =>
            {
                blood.Value = bl;
            });
            
            ev.User.SendSystemMessage("Set current Blood to <color=#ffff00ff>" + bl + "</color>.");
            return;
        }

        if (ev.Message.StartsWith("!test"))
        {

            var value = float.Parse(ev.Message.Substring(6));

            var query = new[] {
                ComponentType.ReadOnly(Il2CppType.Of<User>()),
            };

            var system = VWorld.Server.EntityManager.CreateEntityQuery(query);

            foreach (var entity in system.ToEntityArray(Allocator.Temp))
            {
                var user = VWorld.Server.EntityManager.GetComponentData<User>(entity);
                var localCharacter = user.LocalCharacter;
                var characterEntity = localCharacter._Entity;

                characterEntity.WithComponentData((ref Blood blood) => {
                    blood.Value = value;
                });
                
            }

            return;
        }

        if (ev.Message.StartsWith("!export items")) {
            var gameDataSystem = VWorld.Server.GetExistingSystem<GameDataSystem>();
            var managed = gameDataSystem.ManagedDataRegistry;

            // Open file for writing
            var file = new System.IO.StreamWriter("items.md", false);
            file.WriteLine($"| Key | Name |");
            file.WriteLine($"|---|---|");

            foreach (var entry in gameDataSystem.ItemHashLookupMap)
            {
                try
                {

                    var item = managed.GetOrDefault<ManagedItemData>(entry.Key);
                    
                    var dbname = item.PrefabName.ToString().ToLower().Trim();
                    // remove all special characters from dbname
                    dbname = Regex.Replace(dbname, "[^a-zA-Z0-9_ ]", "");
                    file.WriteLine($"| {dbname} | {item.Name} |");

                    // if (item.Name.ToString().ToLower() == name.ToLower())
                    // {
                    //     return entry.Key;
                    // }

                }
                catch { }
            }
            file.Flush();

            file.Close();
            
            ev.User.SendSystemMessage("<color=#00ff00ff>items.md has been written</color>");
            return;
        }

        if (ev.Message.StartsWith("!export spawnable")) {
            var prefabCollectionSystem = VWorld.Server.GetExistingSystem<PrefabCollectionSystem>();

            // Open file for writing
            var file = new System.IO.StreamWriter("spawnable.md", false);
            foreach (var kv in prefabCollectionSystem._PrefabGuidToNameMap)
            {
                try
                {
                    // dbname = Regex.Replace(dbname, "[^a-zA-Z0-9_ ]", "");
                    if (kv.Value.ToString().StartsWith("CHAR_")) {
                        file.WriteLine($"{kv.Value}");
                    }
                }
                catch { }
            }
            file.Flush();

            file.Close();
            
            ev.User.SendSystemMessage("<color=#00ff00ff>spawnable.md has been written</color>");
            return;
        }

        if (ev.Message.StartsWith("!export user")) {
            var userComponents = VWorld.Server.EntityManager.GetComponentTypes(ev.SenderUserEntity);

            // Open file for writing
            var file = new System.IO.StreamWriter("user_components.md", false);
            foreach (var kv in userComponents)
            {
                try
                {
                    // dbname = Regex.Replace(dbname, "[^a-zA-Z0-9_ ]", "");
                    file.WriteLine($"{kv.GetManagedType().FullName}");
                }
                catch { }
            }
            file.Flush();

            file.Close();
            
            ev.User.SendSystemMessage("<color=#00ff00ff>user_components.md has been written</color>");
            return;
        }
        
        ev.User.SendSystemMessage("<color=#ff0000ff>Unknown command!</color>");
    }

    public override bool Unload()
    {
        if (!VWorld.IsServer) return true;

        Wetstone.Hooks.Chat.OnChatMessage -= HandleChatMessage;

        this._myHook.UnpatchSelf();
        if (_myInjected != null)
            UnityEngine.Object.Destroy(_myInjected);
        
        return true;
    }

    private void Register()
    {
        
    }
}
