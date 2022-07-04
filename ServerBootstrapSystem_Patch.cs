using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Stunlock.Network;
using Unity.Entities;

namespace troublemaker;

[HarmonyPatch]
public static class InitializePlayer
{
    [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
    [HarmonyPostfix]
    public static void OnUserConnected_Patch(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
    {
        var userIndex = __instance._NetEndPointToApprovedUserIndex[netConnectionId];
        var serverClient = __instance._ApprovedUsersLookup[userIndex];
        var userEntity = serverClient.UserEntity;

        var user = __instance.EntityManager.GetComponentData<User>(userEntity);


        if (user.CharacterName.IsEmpty)
        {
            // no name yet, we'll send in TryIsNameValid
            return;
        }

        Messaging.getInstance().SendMessage(user, ServerChatMessageType.Lore, $"<color=\"white\">Welcome</color> <color=\"red\">{user.CharacterName}</color>, <color=\"yellow\">Troublemaker</color> <color=\"white\">is</color> <color=\"red\">enabled</color> <color=\"white\">on this server!</color>");
    }

    [HarmonyPatch(typeof(HandleCreateCharacterEventSystem), nameof(HandleCreateCharacterEventSystem.TryIsNameValid))]
    [HarmonyPostfix]
    public static void TryIsNameValid_Patch(bool __result, HandleCreateCharacterEventSystem __instance, Entity userEntity, string characterNameString)
    {
        if (!__result) return;

        Messaging.getInstance().SendMessage(userEntity, ServerChatMessageType.Lore, $"<color=\"white\">Welcome</color> <color=\"red\">{characterNameString}</color>, <color=\"yellow\">Troublemaker</color> <color=\"white\">is</color> <color=\"red\">enabled</color> <color=\"white\">on this server!</color>");
    }
}
