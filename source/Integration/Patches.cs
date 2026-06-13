using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace TraitsAndClassesLib;

public static class Patches
{
    public const string HarmonyId = "TraitsAndClassesLib";

    public static void Patch(ICoreAPI api)
    {
        new Harmony(HarmonyId).Patch(
                typeof(CharacterSystem).GetMethod("HasTrait", AccessTools.all),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(Patches), nameof(CharacterSystem_HasTrait)))
            );
    }

    public static void Unpatch()
    {
        new Harmony(HarmonyId).Unpatch(typeof(CharacterSystem).GetMethod("HasTrait", AccessTools.all), HarmonyPatchType.Postfix, HarmonyId);
    }

    private static void CharacterSystem_HasTrait(ref bool __result, IPlayer player, string trait)
    {
        __result = __result || player.Entity.GetTraits().Any(playerTrait => playerTrait.Code == trait);
    }
}
