using UnhollowerRuntimeLib;
using Unity.Collections;
using Unity.Entities;
using Wetstone.API;
using ProjectM.Network;
using Unity.Mathematics;
using ProjectM;

namespace troublemaker;

internal static class Helpers
{

    internal static bool TryGetUserCharacter(ulong SteamID, out User User, out Entity UserEntity, out Entity Character)
    {
        var userQuery = VWorld.Server.EntityManager.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<User>()));
        var users = userQuery.ToEntityArray(Allocator.Temp);
        foreach (var entity in users)
        {
            var user = VWorld.Server.EntityManager.GetComponentData<User>(entity);

            if (user.PlatformId != SteamID || !user.IsConnected) continue;

            User = user;
            UserEntity = entity;
            Character = user.LocalCharacter._Entity;
            return true;
        }

        User = new User();
        UserEntity = default;
        Character = default;

        return false;
    }

    internal static bool TryGetPrefabGUID(string PrefabName, out string Name, out PrefabGUID PrefabGUID)
    {
        var prefabCollectionSystem = VWorld.Server.GetExistingSystem<PrefabCollectionSystem>();
        foreach (var kv in prefabCollectionSystem._PrefabGuidToNameMap)
        {
            if (kv.Value.ToString().ToLower() != PrefabName.ToLower()) continue;
            Name = kv.Value.ToString();
            PrefabGUID = kv.Key;
            return true;
        }

        Name = "";
        PrefabGUID = default;
        return false;
    }

    internal static bool IsWalkable(float2 pos)
    {
        // This method needs to return false if the player can not walk when teleported to the given coordinates, and true if the posisiton is a location that the player can walk away from.

        // Wheb pos is a location inside a building or castle, it should return true.

        return true;
    }

    internal static float2 OffsetToWalkable(float2 pos)
    {
        // This method needs to give back a new set of coordinates where the player can walk if the given coordinates are not walkable, otherwise give back the same coordinates.
        // The theory is that `PathfindingUtility.TryFindFirstWalkable` will give back a set of coordinates nearest to the input coordiates where the player is able to move around.
        // eg, when `pos` is in a body of water, in a clove, or outside of the map bounds the method gives back a set of coordinates on the edge instead.

        // This needs to also include if the player is stuck inside an entity, eg, The eye of twilight.

        return pos;
    }

    internal static bool Teleport(Entity userEntity, Entity character, float2 position, bool ignoreCollision = false)
    {

        if (!ignoreCollision)
        {
            if (!Helpers.IsWalkable(position))
            {
                position = Helpers.OffsetToWalkable(position);

                if (!Helpers.IsWalkable(position))
                {
                    return false; // even after offsetting, still not walkable, return false.
                }
            }
        }

        var _entity = VWorld.Server.EntityManager.CreateEntity(
            ComponentType.ReadWrite<FromCharacter>(),
            ComponentType.ReadWrite<PlayerTeleportDebugEvent>()
        );

        VWorld.Server.EntityManager.SetComponentData<FromCharacter>(_entity, new FromCharacter()
        {
            Character = character,
            User = userEntity
        });

        VWorld.Server.EntityManager.SetComponentData<PlayerTeleportDebugEvent>(_entity, new()
        {
            Position = position,
            Target = PlayerTeleportDebugEvent.TeleportTarget.Self
        });

        return true;
    }
}