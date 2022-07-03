using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ProjectM;
using ProjectM.Gameplay.Systems;
using ProjectM.Network;
using troublemaker.Attributes;
using UnhollowerRuntimeLib;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace troublemaker;

[RconHandler]
public class RconCommands
{
    private static Entity empty_entity = new Entity();

    [RconCommand("tm_save", Usage = "tm_save", Description = "Saves the current game state")]
    public static string Save(string[] args)
    {
        // current time to string
        var time = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        Plugin.ServerWorld.GetExistingSystem<TriggerPersistenceSaveSystem>().TriggerSave(SaveReason.ManualSave, $"TM_SAVE_{time}");
        Plugin.Logger?.LogInfo("Saved game");

        return "{\"result\": \"Saved Game\"}";
    }

    [RconCommand("tm_message", Usage = "tm_message <steamid> <message>", Description = "Message Command")]
    public string MessageCommand(ulong steamId, string[] message)
    {
        if (!Helpers.TryGetUserCharacter(steamId, out var user, out var userEntity, out var character))
        {
            return "{\"result\": \"User not found\"}";
        }

        var msg = string.Join(" ", message);
        msg = msg.Replace("{player_name}", user.CharacterName.ToString());

        Messaging.getInstance().SendMessage(user, ServerChatMessageType.Lore, string.Join(" ", message));
        return $"{{\"message\":\"{string.Join(" ", message)}\"}}";
    }

    [RconCommand("tm_message_global", Usage = "tm_message_global [steamid] <message>", Description = "Global Message Command")]
    public string GlobalMessageCommand(string[] message)
    {
        List<string> msgs = message.ToList();
        ServerChatMessageType t = ServerChatMessageType.Lore;
        string msg;
        bool hasSteamId = false;
        if (!ulong.TryParse(message[0], out var steamId))
        {
            msg = string.Join(" ", msgs);
        }
        else
        {
            hasSteamId = true;
            msgs.Shift();
            msg = string.Join(" ", msgs);
        }

        if (hasSteamId && Helpers.TryGetUserCharacter(steamId, out var user, out var userEntity, out var character))
        {
            msg = msg.Replace("{player_name}", user.CharacterName.ToString());
        }

        var query = new[] {
            ComponentType.ReadOnly(Il2CppType.Of<User>()),
        };

        var system = Plugin.ServerWorld.EntityManager.CreateEntityQuery(query);

        foreach (var entity in system.ToEntityArray(Allocator.Temp))
        {
            var u = Plugin.ServerWorld.EntityManager.GetComponentData<User>(entity);

            if (!u.IsConnected) continue;

            //user.SendSystemMessage(string.Join(" ", message));
            Messaging.getInstance().SendMessage(u, t, msg);
        }

        return $"{{\"message\":\"{msg}\"}}";
    }

    [RconCommand("tm_health", Usage = "tm_healh <steamid> <health%>", Description = "Health Command")]
    public string HealthCommand(ulong steamId, int health)
    {
        if (!Helpers.TryGetUserCharacter(steamId, out var user, out var userEntity, out var character))
        {
            return "{\"result\": \"User not found\"}";
        }

        var hp = Plugin.ServerWorld.EntityManager.GetComponentData<Health>(character);
        float restore_hp = ((hp.MaxHealth / 100) * health) - hp.Value;

        var healthEvent = new ChangeHealthDebugEvent()
        {
            Amount = (int)restore_hp
        };

        Plugin.ServerWorld.GetExistingSystem<DebugEventsSystem>().ChangeHealthEvent(user.Index, ref healthEvent);

        return $"{{\"player\":\"{user.CharacterName}\",\"health\":{health}}}";
    }

    [RconCommand("tm_blood", Usage = "tm_blood <steamid> <BloodType> <Quality%> <BloodAddAmount>", Description = "Message Command")]
    public string BloodCommand(ulong steamId, string BloodType, float quality, int quantity)
    {
        if (!Helpers.TryGetUserCharacter(steamId, out var user, out var userEntity, out var character))
        {
            return "{\"result\": \"User not found\"}";
        }

        if (!System.Enum.TryParse(BloodType, true, out BloodTypes bloodType))
        {
            return "{\"error\":\"BloodType not found\"}";
        }

        var bloodEvent = new ChangeBloodDebugEvent()
        {
            Amount = quantity,
            Quality = quality,
            Source = new PrefabGUID((int)bloodType),
        };

        Plugin.ServerWorld.GetExistingSystem<DebugEventsSystem>().ChangeBloodEvent(user.Index, ref bloodEvent);

        return $"{{\"player\":\"{user.CharacterName}\",\"bloodtype\":\"{BloodType}\",\"quality\":{quality},\"quantity\":{quantity}}}";
    }

    [RconCommand("tm_give", Usage = "tm_give <steamid> <item> <quantity>", Description = "Add item(s) to players inventory")]
    public string GiveCommand(ulong steamId, string item, int quantity)
    {
        if (!Helpers.TryGetUserCharacter(steamId, out var user, out var userEntity, out var character))
        {
            return "{\"result\": \"User not found\"}";
        }

        if (!Helpers.TryGetPrefabGUID(item, out string Name, out var guid))
        {
            return $"{{\"error\":\"Prefab with name {item} could not be found\"}}";
        }

        unsafe
        {
            // Thanks Nopey & molenzwiebel
            var gameDataSystem = Plugin.ServerWorld.GetExistingSystem<GameDataSystem>();
            var bytes = stackalloc byte[Marshal.SizeOf<FakeNull>()];
            var bytePtr = new System.IntPtr(bytes);
            Marshal.StructureToPtr<FakeNull>(new()
            {
                value = 0,
                has_value = true
            }, bytePtr, false);
            var boxedBytePtr = System.IntPtr.Subtract(bytePtr, 0x10);
            var hack = new Il2CppSystem.Nullable<int>(boxedBytePtr);
            if (!InventoryUtilitiesServer.TryAddItem(Plugin.ServerWorld.EntityManager, gameDataSystem.ItemHashLookupMap, character, guid, quantity, out _, out Entity e, default, hack, true, false, false))
            {
                // Adding item failed, drop it on the ground instead
                InventoryUtilitiesServer.CreateDropItem(Plugin.ServerWorld.EntityManager, character, guid, quantity, empty_entity);
                return $"{{\"player\":\"{user.CharacterName}\",\"item\":\"{item}\",\"quantity\":\"{quantity}\",\"destination\":\"ground\"}}";
            }
        }

        return $"{{\"player\":\"{user.CharacterName}\",\"item\":\"{item}\",\"quantity\":\"{quantity}\",\"destination\":\"inventory\"}}";
    }

    [RconCommand("tm_spawn", Usage = "tm_spawn <npc> <x> <z>", Description = "Spawn npc")]
    public string SpawnCommand(string npc, float x, float z)
    {
        if (!Helpers.TryGetPrefabGUID(npc, out string Name, out var guid))
        {
            return $"{{\"error\":\"Prefab with name {npc} could not be found\"}}";
        }

        Plugin.ServerWorld.GetExistingSystem<UnitSpawnerUpdateSystem>().SpawnUnit(empty_entity, guid, new float3(x, 0, z), 1, 1, 2, -1);

        return $"{{\"npc\":\"{Name}\",\"x\":{x},\"z\":{z}}}";
    }

    [RconCommand("tm_teleport", Usage = "tm_teleport <steamid> <x> <z>", Description = "Teleport player")]
    public string TeleportCommand(ulong steamId, float x, float z)
    {
        if (!Helpers.TryGetUserCharacter(steamId, out var user, out var userEntity, out var character))
        {
            return "{\"result\": \"User not found\"}";
        }

        if (!Helpers.Teleport(userEntity, character, new float2(x, z)))
        {
            return "{\"error\":\"Collision detected\"}";
        }

        return $"{{\"player\":\"{user.CharacterName}\",\"x\":{x},\"z\":{z}}}";
    }

    [RconCommand("tm_get_player_pos", Usage = "tm_get_player_pos <steamid>", Description = "Get player position")]
    public string GetPlayerPosCommand(ulong steamId)
    {
        if (!Helpers.TryGetUserCharacter(steamId, out var user, out var userEntity, out var character))
        {
            return "{\"result\": \"User not found\"}";
        }

        var component = Plugin.ServerWorld.EntityManager.GetComponentData<LocalToWorld>(character);
        var pos = new float2(component.Position.x, component.Position.z);

        return $"{{\"player\":\"{user.CharacterName}\",\"x\":{pos.x},\"z\":{pos.y}}}";
    }

    [RconCommand("tm_get_player_home", Usage = "tm_get_player_home <steamid>", Description = "Get player home")]
    public string GetPlayerHomeCommand(ulong steamId)
    {
        var coffinQuery = Plugin.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<RespawnPoint>(),
                        ComponentType.ReadOnly<UserOwner>());
        var coffins = coffinQuery.ToEntityArray(Allocator.Temp);

        foreach (var coffin in coffins)
        {
            var respawnPoint = Plugin.ServerWorld.EntityManager.GetComponentData<RespawnPoint>(coffin);
            var ownerUser = Plugin.ServerWorld.EntityManager.GetComponentData<User>(respawnPoint.RespawnPointOwner._Entity);
            if (ownerUser.PlatformId != steamId || !ownerUser.IsConnected) continue;

            var transform = Plugin.ServerWorld.EntityManager.GetComponentData<LocalToWorld>(coffin);
            var respawnOffset = respawnPoint.SpawnExitOffset;

            // Apply transforms rotation to respawnOffset
            var rot = transform.Rotation.value;
            var rotQuat = new Quaternion(rot.x, rot.y, rot.z, rot.w);
            var rotQuatMat = new float3x3(rotQuat);
            var respawnOffsetRotated = math.mul(rotQuatMat, respawnOffset);
            var respawnPos = transform.Position + respawnOffsetRotated;

            var f2pos = new float2(respawnPos.x, respawnPos.z);

            Helpers.Teleport(respawnPoint.RespawnPointOwner.GetEntityOnServer(), respawnPoint.RespawnPointOwner._Entity, f2pos, true);

            return $"{{\"player\":\"{ownerUser.CharacterName}\",\"x\":{respawnPos.x},\"z\":{respawnPos.z}}}";
        }

        return $"{{\"error\":\"User not found or has no coffin\"}}";
    }

    [RconCommand("tm_time", Usage = "tm_time [set|add] [minutes] [affect servants]", Description = "Get or Set Time")]
    public string TimeCommand(string type = "get", int time = int.MaxValue, bool affectservats = false)
    {
        if (time <= 0)
        {
            return $"{{\"error\":\"Unable to go back in time.\"}}";
        }

        if (time != int.MaxValue)
        {
            if (!Enum.TryParse<SetTimeOfDayEvent.SetTimeType>(type, true, out var setTimeType))
            {
                setTimeType = SetTimeOfDayEvent.SetTimeType.Add;
            }

            var setTimeEntity = Plugin.ServerWorld.EntityManager.CreateEntity(ComponentType.ReadOnly<SetTimeOfDayEvent>());
            Plugin.ServerWorld.EntityManager.SetComponentData<SetTimeOfDayEvent>(setTimeEntity, new SetTimeOfDayEvent()
            {
                Day = 0,
                Hour = 0,
                Minute = time,
                Month = 0,
                Year = 0,
                Type = setTimeType
            });
            //Created Entity will be p

            // NOTE: Do we want to advance coffinStations and missions etc?
            if (affectservats)
            {
                var servantCoffinQuery = Plugin.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<ServantCoffinstation>());
                var coffinEntities = servantCoffinQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

                foreach (var coffinEntity in coffinEntities)
                {
                    var coffinStation = Plugin.ServerWorld.EntityManager.GetComponentData<ServantCoffinstation>(coffinEntity);

                    //Check if the coffin is currently converting an npc
                    if (coffinStation.State == ServantCoffinState.Converting)
                    {
                        //Increase the conversion progress. As it's a struct, this makes a copy and does not modify the original
                        var newProgress = Math.Max(0, coffinStation.ConvertionProgress + (float)time);
                        coffinStation.ConvertionProgress = newProgress;
                        //Set the modified component back on the enity so the value change is used
                        Plugin.ServerWorld.EntityManager.SetComponentData<ServantCoffinstation>(coffinEntity, coffinStation);
                    }
                }

                // //Progress Servant mission timers
                // //Query all Entity with an associated ActiveServantMission. This component contains data for active servant missions
                var servantMissonQuery = Plugin.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<ActiveServantMission>());
                var missionEntities = servantMissonQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

                foreach (var missionEntity in missionEntities)
                {
                    //ActiveServantMission is an IBufferElementData so it has to be gotten as a buffer rather than component
                    var missionBuffer = Plugin.ServerWorld.EntityManager.GetBuffer<ActiveServantMission>(missionEntity);

                    //A buffer can contain multiple things and must therefore be looped through
                    for (int i = 0; i < missionBuffer.Length; i++)
                    {
                        //Get mission at current index
                        var mission = missionBuffer[i];
                        //Reduce mission length, causing it to finish sooner
                        var newMissionLength = Math.Max(0, mission.MissionLength - (float)time);
                        mission.MissionLength = newMissionLength;
                        //Set the mission at the current index to the now modified mission
                        missionBuffer[i] = mission;
                    }
                }
            }
        }

        var system = Plugin.ServerWorld.GetExistingSystem<HandleGameplayEventsSystem>();

        var e = system._DayNightCycle.GetSingletonEntity();
        var dnc = Plugin.ServerWorld.EntityManager.GetComponentData<DayNightCycle>(e);
        var now = dnc.GameDateTimeNow;

        // this format is not json parsable,
        // var year = string.Format("{0:0000}", now.Year);
        // var month = string.Format("{0:00}", now.Month);
        // var day = string.Format("{0:00}", now.Day);
        // var hour = string.Format("{0:00}", now.Hour);
        // var minute = string.Format("{0:00}", now.Minute);

        var year = now.Year;
        var month = now.Month;
        var day = now.Day;
        var hour = now.Hour;
        var minute = now.Minute;

        var dayStartHour = dnc.DayTimeStartInSeconds / (dnc.DayDurationInSeconds / 24);
        var dayEndHour = dayStartHour + (dnc.DayTimeDurationInSeconds / (dnc.DayDurationInSeconds / 24));

        return $"{{\"day\":{day},\"month\":{month},\"year\":{year},\"hour\":{hour},\"minute\":{minute},\"dayStartHour\":{dayStartHour},\"dayEndHour\":{dayEndHour}}}";
    }
}
