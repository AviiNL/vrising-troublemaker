using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Stunlock.Network;
using Wetstone.API;

namespace troublemaker;

[HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
public class ServerBootstrapSystem_Patch
{
    public static void Postfix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
    {
        var em = VWorld.Server.EntityManager;
        var userIndex = __instance._NetEndPointToApprovedUserIndex[netConnectionId];
        var serverClient = __instance._ApprovedUsersLookup[userIndex];
        var userEntity = serverClient.UserEntity;
        var userComponent = em.GetComponentData<User>(userEntity);

        var name = string.IsNullOrEmpty(userComponent.CharacterName.ToString()) ? "Vampire" : userComponent.CharacterName;

        Messaging.getInstance().SendMessage(userComponent, ServerChatMessageType.Lore, $"<color=\"white\">Welcome</color> <color=\"red\">{name}</color>, <color=\"yellow\">Troublemaker</color> <color=\"white\">is</color> <color=\"red\">enabled</color> <color=\"white\">on this server!</color>");
    }
}
