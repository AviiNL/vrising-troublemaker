using UnhollowerRuntimeLib;
using Unity.Collections;
using Unity.Entities;
using Wetstone.API;
using ProjectM.Network;
using Unity.Mathematics;
using ProjectM;

namespace troublemaker;

internal static class Helpers {

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

    internal static bool Teleport(Entity userEntity, Entity character, float2 position, bool ignoreCollision = false) {
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