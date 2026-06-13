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

        /*new Harmony(HarmonyId).Patch(
                typeof(CharacterSystem).GetMethod("onCharacterSelection", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(Patches), nameof(CharacterSystem_onCharacterSelection)))
            );*/
    }

    public static void Unpatch()
    {
        new Harmony(HarmonyId).Unpatch(typeof(CharacterSystem).GetMethod("HasTrait", AccessTools.all), HarmonyPatchType.Postfix, HarmonyId);
        //new Harmony(HarmonyId).Unpatch(typeof(CharacterSystem).GetMethod("onCharacterSelection", AccessTools.all), HarmonyPatchType.Prefix, HarmonyId);
    }

    private static void CharacterSystem_HasTrait(CharacterSystem __instance, ref bool __result, IPlayer player, string trait)
    {
        __result = __result || player.Entity.GetTraits().Any(playerTrait => playerTrait.Code == trait);
    }

    private static bool CharacterSystem_onCharacterSelection(CharacterSystem __instance, IServerPlayer fromPlayer, CharacterSelectionPacket p)
    {
        bool didSelectBefore = fromPlayer.GetModData<bool>("createCharacter", false);
        bool allowSelect = !didSelectBefore || fromPlayer.Entity.WatchedAttributes.GetBool("allowcharselonce") || fromPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;

        if (!allowSelect)
        {
            fromPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
            fromPlayer.BroadcastPlayerData(true);
            return false;
        }

        if (p.DidSelect)
        {
            fromPlayer.SetModData<bool>("createCharacter", true);

            //__instance.setCharacterClass(fromPlayer.Entity, p.CharacterClass, !didSelectBefore || fromPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative);

            var bh = fromPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            bh.ApplyVoice(p.VoiceType, p.VoicePitch, false);

            foreach (var skinpart in p.SkinParts)
            {
                bh.selectSkinPart(skinpart.Key, skinpart.Value, false);
            }

            var date = DateTime.UtcNow;
            fromPlayer.ServerData.LastCharacterSelectionDate = date.ToShortDateString() + " " + date.ToShortTimeString();

            // allow players that just joined to immediately re select the class
            var allowOneFreeClassChange = fromPlayer.Entity.Api.World.Config.GetBool("allowOneFreeClassChange");
            if (!didSelectBefore && allowOneFreeClassChange)
            {
                fromPlayer.ServerData.LastCharacterSelectionDate = null;
            }
            else
            {
                fromPlayer.Entity.WatchedAttributes.RemoveAttribute("allowcharselonce");
            }
        }
        fromPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
        fromPlayer.BroadcastPlayerData(true);

        return false;
    }
}
