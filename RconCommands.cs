using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
using Wetstone.API;

namespace troublemaker;

[RconHandler]
public class RconCommands
{

    [RconCommand("tm_message", Usage = "tm_message <steamid> <message>", Description = "Message Command")]
    public string MessageCommand(ulong steamId, string[] message)
    {

        var query = new[] {
            ComponentType.ReadOnly(Il2CppType.Of<User>()),
        };

        var system = VWorld.Server.EntityManager.CreateEntityQuery(query);

        foreach (var entity in system.ToEntityArray(Allocator.Temp))
        {
            var user = VWorld.Server.EntityManager.GetComponentData<User>(entity);

            if (user.PlatformId != steamId || !user.IsConnected) continue;

            Messaging.getInstance().SendMessage(user, ServerChatMessageType.Lore, string.Join(" ", message));
            return $"{{\"message\":\"{string.Join(" ", message)}\"}}";
        }

        return "{\"error\":\"User not found\"}";
    }

    [RconCommand("tm_message_global", Usage = "tm_message_global <message>", Description = "Global Message Command")]
    public string GlobalMessageCommand(string[] message)
    {
        List<string> msgs = message.ToList();
        ServerChatMessageType t;
        string msg;
        if(!Enum.TryParse<ServerChatMessageType>(message[0], true, out t)) {
            t = ServerChatMessageType.Lore;
            msg = string.Join(" ", msgs);
        } else {
            msgs.Shift();
            msg = string.Join(" ", msgs);
        }


        var query = new[] {
            ComponentType.ReadOnly(Il2CppType.Of<User>()),
        };

        var system = VWorld.Server.EntityManager.CreateEntityQuery(query);

        foreach (var entity in system.ToEntityArray(Allocator.Temp))
        {
            var user = VWorld.Server.EntityManager.GetComponentData<User>(entity);

            if (!user.IsConnected) continue;

            //user.SendSystemMessage(string.Join(" ", message));
            Messaging.getInstance().SendMessage(user, t, msg);
        }

        return $"{{\"message\":\"{string.Join(" ", message)}\"}}";
    }

    [RconCommand("tm_health", Usage = "tm_healh <steamid> <health%>", Description = "Health Command")]
    public string HealthCommand(ulong steamId, int health)
    {

        var query = new[] {
            ComponentType.ReadOnly(Il2CppType.Of<User>()),
        };

        var system = VWorld.Server.EntityManager.CreateEntityQuery(query);

        foreach (var entity in system.ToEntityArray(Allocator.Temp))
        {
            var user = VWorld.Server.EntityManager.GetComponentData<User>(entity);

            if (user.PlatformId != steamId || !user.IsConnected) continue;

            var character = user.LocalCharacter;
            var ent = character._Entity;

            ent.WithComponentData((ref Health hp) =>
            {
                float restore_hp = (hp.MaxHealth / 100) * health;

                hp.Value = restore_hp;
            });

            return $"{{\"player\":\"{user.CharacterName}\",\"health\":{health}}}";
        }

        return "{\"error\":\"User not found\"}";
    }

    [RconCommand("tm_blood", Usage = "tm_blood <steamid> <BloodType> <Quality%> <Quantity%>", Description = "Message Command")]
    public string BloodCommand(ulong steamId, string BloodType, float quality, float quantity)
    {
        var query = new[] {
            ComponentType.ReadOnly(Il2CppType.Of<User>()),
        };

        var system = VWorld.Server.EntityManager.CreateEntityQuery(query);

        foreach (var entity in system.ToEntityArray(Allocator.Temp))
        {
            var user = VWorld.Server.EntityManager.GetComponentData<User>(entity);

            if (user.PlatformId != steamId || !user.IsConnected) continue;

            var character = user.LocalCharacter;
            var ent = character._Entity;

            ent.WithComponentData((ref Blood blood) =>
            {
                blood.Value = quantity;
                blood.Quality = quality;
                if (System.Enum.TryParse(BloodType, true, out BloodTypes bloodType))
                {
                    blood.BloodType = new PrefabGUID((int)bloodType);
                }
            });

            return $"{{\"player\":\"{user.CharacterName}\",\"bloodtype\":\"{BloodType}\",\"quality\":{quality},\"quantity\":{quantity}}}";
        }

        return "{\"error\":\"User not found\"}";
    }

    [RconCommand("tm_give", Usage = "tm_give <steamid> <item> <quantity>", Description = "Add item(s) to players inventory")]
    public string GiveCommand(ulong steamId, string item, int quantity) // TODO: Check if inventory is full, if so, drop item on ground
    {
        var query = new[] {
            ComponentType.ReadOnly(Il2CppType.Of<User>()),
        };

        var system = VWorld.Server.EntityManager.CreateEntityQuery(query);

        foreach (var entity in system.ToEntityArray(Allocator.Temp))
        {
            var user = VWorld.Server.EntityManager.GetComponentData<User>(entity);

            if (user.PlatformId != steamId || !user.IsConnected) continue;

            var character = user.LocalCharacter;
            var ent = character._Entity;

            PrefabGUID guid = GetGuidFromPrefabName(item);
            if (guid.GuidHash == 0)
            {
                return "{\"error\":\"Item not found\"}";
            }

            unsafe
            {
                // Thanks Nopey & molenzwiebel
                var gameDataSystem = VWorld.Server.GetExistingSystem<GameDataSystem>();
                var bytes = stackalloc byte[Marshal.SizeOf<FakeNull>()];
                var bytePtr = new System.IntPtr(bytes);
                Marshal.StructureToPtr<FakeNull>(new()
                {
                    value = 7,
                    has_value = true
                }, bytePtr, false);
                var boxedBytePtr = System.IntPtr.Subtract(bytePtr, 0x10);
                var hack = new Il2CppSystem.Nullable<int>(boxedBytePtr);
                InventoryUtilitiesServer.TryAddItem(VWorld.Server.EntityManager, gameDataSystem.ItemHashLookupMap, ent, guid, quantity, out _, out Entity e, default, hack, true, false, false);
            }

            return $"{{\"player\":\"{user.CharacterName}\",\"item\":\"{item}\",\"quantity\":\"{quantity}\"}}";
        }

        return "{\"error\":\"User not found\"}";
    }

    [RconCommand("tm_spawn", Usage = "tm_spawn <npc> <x> <z>", Description = "Spawn npc")]
    public string SpawnCommand(string npc, float x, float z)
    {
        var prefabCollectionSystem = VWorld.Server.GetExistingSystem<PrefabCollectionSystem>();
        foreach (var kv in prefabCollectionSystem._PrefabGuidToNameMap)
        {
            if (kv.Value.ToString().ToLower() != npc.ToLower()) continue;
            VWorld.Server.GetExistingSystem<UnitSpawnerUpdateSystem>().SpawnUnit(empty_entity, kv.Key, new float3(x, 0, z), 1, 1, 2, -1);
            return $"{{\"npc\":\"{kv.Value}\",\"x\":{x},\"z\":{z}}}";
        }
        return "{\"error\":\"NPC not found\"}";
    }

    [RconCommand("tm_teleport", Usage = "tm_teleport <steamid> <x> <z>", Description = "Teleport player")]
    public string TeleportCommand(ulong steamId, float x, float z)
    {
        var query = new[] {
            ComponentType.ReadOnly(Il2CppType.Of<User>()),
        };

        var system = VWorld.Server.EntityManager.CreateEntityQuery(query);

        foreach (var entity in system.ToEntityArray(Allocator.Temp))
        {
            var user = VWorld.Server.EntityManager.GetComponentData<User>(entity);

            if (user.PlatformId != steamId || !user.IsConnected) continue;

            var ent = user.LocalCharacter._Entity;

            var _entity = VWorld.Server.EntityManager.CreateEntity(
                ComponentType.ReadWrite<FromCharacter>(),
                ComponentType.ReadWrite<PlayerTeleportDebugEvent>()
            );

            VWorld.Server.EntityManager.SetComponentData<FromCharacter>(_entity, new FromCharacter()
            {
                Character = ent,
                User = entity
            });

            var f2pos = new float2(x, z);

            VWorld.Server.EntityManager.SetComponentData<PlayerTeleportDebugEvent>(_entity, new()
            {
                Position = f2pos,
                Target = PlayerTeleportDebugEvent.TeleportTarget.Self
            });

            return $"{{\"player\":\"{user.CharacterName}\",\"x\":{f2pos.x},\"z\":{f2pos.y}}}";
        }

        return $"{{\"error\":\"User not found\"}}";
    }

    [RconCommand("tm_get_player_pos", Usage = "tm_get_player_pos <steamid>", Description = "Get player position")]
    public string GetPlayerPosCommand(ulong steamId)
    {
        var query = new[] {
            ComponentType.ReadOnly(Il2CppType.Of<User>()),
        };

        var system = VWorld.Server.EntityManager.CreateEntityQuery(query);

        foreach (var entity in system.ToEntityArray(Allocator.Temp))
        {
            var user = VWorld.Server.EntityManager.GetComponentData<User>(entity);

            if (user.PlatformId != steamId || !user.IsConnected) continue;

            var character = user.LocalCharacter;
            var ent = character._Entity;

            var component = VWorld.Server.EntityManager.GetComponentData<LocalToWorld>(ent);
            var pos = new float2(component.Position.x, component.Position.z);

            return $"{{\"player\":\"{user.CharacterName}\",\"x\":{pos.x},\"z\":{pos.y}}}";
        }

        return $"{{\"error\":\"User not found\"}}";
    }

    [RconCommand("tm_get_player_home", Usage = "tm_get_player_home <steamid>", Description = "Get player home")]
    public string GetPlayerHomeCommand(ulong steamId)
    {
        var coffinQuery = VWorld.Server.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<RespawnPoint>(),
                        ComponentType.ReadOnly<UserOwner>());
        var coffins = coffinQuery.ToEntityArray(Allocator.Temp);

        foreach (var coffin in coffins)
        {
            var respawnPoint = VWorld.Server.EntityManager.GetComponentData<RespawnPoint>(coffin);
            var ownerUser = VWorld.Server.EntityManager.GetComponentData<User>(respawnPoint.RespawnPointOwner._Entity);
            if (ownerUser.PlatformId != steamId || !ownerUser.IsConnected) continue;

            var transform = VWorld.Server.EntityManager.GetComponentData<LocalToWorld>(coffin);
            var respawnOffset = respawnPoint.SpawnExitOffset;

            // Apply transforms rotation to respawnOffset
            var rot = transform.Rotation.value;
            var rotQuat = new Quaternion(rot.x, rot.y, rot.z, rot.w);
            var rotQuatMat = new float3x3(rotQuat);
            var respawnOffsetRotated = math.mul(rotQuatMat, respawnOffset);
            var respawnPos = transform.Position + respawnOffsetRotated;

            return this.TeleportCommand(steamId, respawnPos.x, respawnPos.z);
        }

        return $"{{\"error\":\"User not found or has no coffin\"}}";
    }

    [RconCommand("tm_time", Usage = "tm_time [set|add] [minutes] [affect servants]", Description = "Get or Set Time")]
    public string TimeCommand(string type = "get", int time = int.MaxValue, bool affectservats = false)
    {
        if (time <= 0) {
            return $"{{\"error\":\"Unable to go back in time.\"}}";
        }

        if (time != int.MaxValue)
        {
            if (!Enum.TryParse<SetTimeOfDayEvent.SetTimeType>(type, true, out var setTimeType)) {
                setTimeType = SetTimeOfDayEvent.SetTimeType.Add;
            }

            var setTimeEntity = VWorld.Server.EntityManager.CreateEntity(ComponentType.ReadOnly<SetTimeOfDayEvent>());
            VWorld.Server.EntityManager.SetComponentData<SetTimeOfDayEvent>(setTimeEntity, new SetTimeOfDayEvent() {
                Day = 0,
                Hour = 0,
                Minute = time,
                Month = 0,
                Year = 0,
                Type = setTimeType
            });
            //Created Entity will be p

            // NOTE: Do we want to advance coffinStations and missions etc?
            if (affectservats) {
                var servantCoffinQuery = VWorld.Server.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<ServantCoffinstation>());
                var coffinEntities = servantCoffinQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

                foreach (var coffinEntity in coffinEntities) {
                    var coffinStation = VWorld.Server.EntityManager.GetComponentData<ServantCoffinstation>(coffinEntity);

                    //Check if the coffin is currently converting an npc
                    if (coffinStation.State == ServantCoffinState.Converting) {
                        //Increase the conversion progress. As it's a struct, this makes a copy and does not modify the original
                        var newProgress = Math.Max(0, coffinStation.ConvertionProgress + (float)time);
                        coffinStation.ConvertionProgress = newProgress;
                        //Set the modified component back on the enity so the value change is used
                        VWorld.Server.EntityManager.SetComponentData<ServantCoffinstation>(coffinEntity, coffinStation);
                    }
                }

                // //Progress Servant mission timers
                // //Query all Entity with an associated ActiveServantMission. This component contains data for active servant missions
                var servantMissonQuery = VWorld.Server.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<ActiveServantMission>());
                var missionEntities = servantMissonQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

                foreach (var missionEntity in missionEntities) {
                    //ActiveServantMission is an IBufferElementData so it has to be gotten as a buffer rather than component
                    var missionBuffer = VWorld.Server.EntityManager.GetBuffer<ActiveServantMission>(missionEntity);

                    //A buffer can contain multiple things and must therefore be looped through
                    for (int i = 0; i < missionBuffer.Length; i++) {
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

        var system = VWorld.Server.GetExistingSystem<HandleGameplayEventsSystem>();

        var e = system._DayNightCycle.GetSingletonEntity();
        var dnc = VWorld.Server.EntityManager.GetComponentData<DayNightCycle>(e);
        var now = dnc.GameDateTimeNow;

        var year = string.Format("{0:0000}", now.Year);
        var month = string.Format("{0:00}", now.Month);
        var day = string.Format("{0:00}", now.Day);
        var hour = string.Format("{0:00}", now.Hour);
        var minute = string.Format("{0:00}", now.Minute);

        var dayStartHour = dnc.DayTimeStartInSeconds / (dnc.DayDurationInSeconds / 24);
        var dayEndHour = dayStartHour + (dnc.DayTimeDurationInSeconds / (dnc.DayDurationInSeconds / 24));

        return $"{{\"day\":{day},\"month\":{month},\"year\":{year},\"hour\":{hour},\"minute\":{minute},\"dayStartHour\":{dayStartHour},\"dayEndHour\":{dayEndHour}}}";
    }

#region HELPERS
    private static Entity empty_entity = new Entity();
    private PrefabGUID GetGuidFromPrefabName(string name)
    {
        var gameDataSystem = VWorld.Server.GetExistingSystem<GameDataSystem>();
        var managed = gameDataSystem.ManagedDataRegistry;
        
        foreach (var entry in gameDataSystem.ItemHashLookupMap)
        {
            try
            {
                var item = managed.GetOrDefault<ManagedItemData>(entry.Key);
                var dbname = item.PrefabName.ToString().ToLower().Trim();
                // remove all special characters from dbname
                dbname = Regex.Replace(dbname, "[^a-zA-Z0-9_ ]", "");
                if (dbname == name.ToLower().Trim())
                {
                    return entry.Key;
                }
            }
            catch { }
        }

        return new PrefabGUID(0);
    }
#endregion
}