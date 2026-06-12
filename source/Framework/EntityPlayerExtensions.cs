using OverhaulLib.Utils;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace TraitsAndClassesLib;

public static class EntityPlayerExtensions
{
    public static IEnumerable<ExtendedTrait> GetTraits(this EntityPlayer player, TraitsAndClassesLibSystem? system = null)
    {
        PlayerTratis traits = player.GetPlayerTraits(system);
        return traits.GetTraits();
    }

    public static PlayerTratis GetPlayerTraits(this EntityPlayer player, TraitsAndClassesLibSystem? system = null)
    {
        system ??= player.Api.ModLoader.GetModSystem<TraitsAndClassesLibSystem>();

        if (system == null)
        {
            return PlayerTratis.FromAttributes(player.WatchedAttributes);
        }

        if (system.PlayerTraitsCache.TryGetValue(player.PlayerUID, out PlayerTratis? traits))
        {
            return traits;
        }
        else
        {
            traits = PlayerTratis.FromAttributes(player.WatchedAttributes);
            system.PlayerTraitsCache.Add(player.PlayerUID, traits);
            return traits;
        }
    }

    public static void ApplyTraitsAttributes(this EntityPlayer player, TraitsAndClassesLibSystem? system = null)
    {
        system ??= player.Api.ModLoader.GetModSystem<TraitsAndClassesLibSystem>();

        CharacterSystem? characterSystem = player.Api.ModLoader.GetModSystem<CharacterSystem>();

        string? classCode = player.WatchedAttributes.GetString("characterClass");
        if (characterSystem == null || classCode == null || classCode == "") return;
        CharacterClass? characterClass = characterSystem.characterClasses?.Find(c => c.Code == classCode);

        if (characterClass == null)
        {
            Log.Error(player.Api, typeof(EntityPlayerExtensions), $"Character class with code '{classCode}' not found when trying to apply class traits for player '{player.Player?.PlayerName ?? player.GetName()}'.");
            return;
        }

        // Reset 
        foreach ((_, EntityFloatStats stats) in player.Stats)
        {
            foreach ((string stat, _) in stats.ValuesByKey)
            {
                if (stat == "trait")
                {
                    stats.Remove(stat);
                    break;
                }
            }
        }

        IEnumerable<string> libraryTraits = player.GetTraits(system).Select(trait => trait.Code);
        string[] extraTraits = player.WatchedAttributes.GetStringArray("extraTraits") ?? [];
        IEnumerable<string> allTraits = extraTraits == null ? characterClass.Traits : characterClass.Traits.Concat(libraryTraits).Concat(extraTraits).Distinct();

        // Aggregate stats values
        Dictionary<string, double> statValues = [];
        foreach (string traitCode in allTraits)
        {
            if (!characterSystem.TraitsByCode.TryGetValue(traitCode, out Trait? trait)) continue;

            foreach ((string attributeCode, double attributeValue) in trait.Attributes)
            {
                if (statValues.ContainsKey(attributeCode))
                {
                    statValues[attributeCode] += attributeValue;
                }
                else
                {
                    statValues[attributeCode] = attributeValue;
                }
            }
        }

        // Apply aggregated values
        foreach ((string stat, double value) in statValues)
        {
            player.Stats.Set(stat, "trait", (float)value, true);
        }

        player.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();
    }
}