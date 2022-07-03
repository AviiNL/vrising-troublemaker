using HarmonyLib;
using ProjectM;

namespace troublemaker;

[HarmonyPatch]
internal static class Initialization
{
    [HarmonyPatch("ProjectM.GameBootstrap", "Start")]
    [HarmonyPostfix]
    public static void Start_Postfix(GameBootstrap __instance)
    {
        Plugin.Init();
    }
}