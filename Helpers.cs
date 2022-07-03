using UnhollowerRuntimeLib;
using Unity.Collections;
using Unity.Entities;
using ProjectM.Network;
using Unity.Mathematics;
using ProjectM;
using ProjectM.Tiles;
using ProjectM.Terrain;
using UnityEngine;
using System;

namespace troublemaker;

internal static class Helpers
{
    private static TileMapCollisionMath.MapData? _mapDataValue;
    private static TileMapCollisionMath.MapData _mapData
    {
        get
        {
            if (_mapDataValue == null)
            {
                var terrainManager = Plugin.ServerWorld.GetExistingSystem<TerrainManager>();
                var terrainChunks = terrainManager.GetChunksAndComplete();
                var getTileCollision = Plugin.ServerWorld.EntityManager.GetBufferFromEntity<ChunkTileCollision>();
                var getGameplayHeights = Plugin.ServerWorld.EntityManager.GetBufferFromEntity<ChunkGameplayHeights>();
                _mapDataValue = TileCollisionHelper.CreateMapData(TileCollisionHelper.CreateLinePolygon(), terrainChunks, getTileCollision, getGameplayHeights);
            }
            return _mapDataValue;
        }
    }

    internal static bool TryGetUserCharacter(ulong SteamID, out User User, out Entity UserEntity, out Entity Character)
    {
        var userQuery = Plugin.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<User>()));
        var users = userQuery.ToEntityArray(Allocator.Temp);
        foreach (var entity in users)
        {
            var user = Plugin.ServerWorld.EntityManager.GetComponentData<User>(entity);

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
        var prefabCollectionSystem = Plugin.ServerWorld.GetExistingSystem<PrefabCollectionSystem>();
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
        return IsWalkable(pos, 1);
    }

    /// <summary>
    /// Checks a circle area with a specified radius to to if there is any collision for normal movement
    /// </summary>
    /// <param name="pos">The position that is being checked</param>
    /// <param name="radius">The radius used for the check</param>
    /// <returns>True if the position is valid for normal movement</returns>
    internal static bool IsWalkable(float2 pos, int radius)
    {
        return !TileMapCollisionMath.CheckStaticCircle(_mapData, pos, radius, (byte)MapCollisionFlags.CollideNormalMovement);
    }

    internal static float2 OffsetToWalkable(float2 pos)
    {
        return OffsetToWalkable(pos, 1, 12, 10);
    }

    /// <summary>
    /// Performs multiple checks around the position to find a valid normal movement position
    /// </summary>
    /// <param name="pos">The position that is being checked</param>
    /// <param name="distance">The distance checked in each loop of the checks</param>
    /// <param name="steps">The number of checks to perform in each loop</param>
    /// <param name="distanceChecks">The number of times to icrement the distance and check around the position again</param>
    /// <returns></returns>
    internal static float2 OffsetToWalkable(float2 pos, int distance, int steps, int distanceChecks)
    {
        var stepAngle = Mathf.PI * 2f / steps;

        for (var d = 1; d < distanceChecks + 1; d++)
        {
            for (var r = 0f; r < Mathf.PI * 2; r += stepAngle)
            {
                var newPos = pos + new float2(Mathf.Cos(r), Mathf.Sin(r)) * distance * (int)Math.Pow(2, d);
                if (IsWalkable(newPos))
                    return newPos;
            }
        }

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

        var _entity = Plugin.ServerWorld.EntityManager.CreateEntity(
            ComponentType.ReadWrite<FromCharacter>(),
            ComponentType.ReadWrite<PlayerTeleportDebugEvent>()
        );

        Plugin.ServerWorld.EntityManager.SetComponentData<FromCharacter>(_entity, new FromCharacter()
        {
            Character = character,
            User = userEntity
        });

        Plugin.ServerWorld.EntityManager.SetComponentData<PlayerTeleportDebugEvent>(_entity, new()
        {
            Position = position,
            Target = PlayerTeleportDebugEvent.TeleportTarget.Self
        });

        return true;
    }
}