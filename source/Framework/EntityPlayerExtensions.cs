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
    public static IEnumerable<ExtendedCharacterClass> GetClasses(this EntityPlayer player, TraitsAndClassesLibSystem? system = null)
    {
        PlayerClasses classes = player.GetPlayerClasses(system);
        return classes.GetClasses();
    }
    public static IEnumerable<ExtendedTrait> GetExtraTraits(this EntityPlayer player, TraitsAndClassesLibSystem? system = null)
    {
        system ??= player.Api.ModLoader.GetModSystem<TraitsAndClassesLibSystem>();
        string[] extraTraitsCodes = player.WatchedAttributes.GetStringArray("extraTraits") ?? [];
        return extraTraitsCodes.Where(code => system.Traits.ContainsKey(code)).Select(code => system.Traits[code]);
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
    public static PlayerClasses GetPlayerClasses(this EntityPlayer player, TraitsAndClassesLibSystem? system = null)
    {
        system ??= player.Api.ModLoader.GetModSystem<TraitsAndClassesLibSystem>();

        if (system == null)
        {
            return PlayerClasses.FromAttributes(player.WatchedAttributes);
        }

        if (system.PlayerClassesCache.TryGetValue(player.PlayerUID, out PlayerClasses? playerClasses))
        {
            return playerClasses;
        }
        else
        {
            playerClasses = PlayerClasses.FromAttributes(player.WatchedAttributes);
            system.PlayerClassesCache.Add(player.PlayerUID, playerClasses);
            return playerClasses;
        }
    }

    public static void UpdaitTraits(this EntityPlayer player, TraitsAndClassesLibSystem? system = null)
    {
        system ??= player.Api.ModLoader.GetModSystem<TraitsAndClassesLibSystem>();
        ReaplyClassesTraits(player, system);
        ApplyTraitsAttributes(player, system);
    }
    public static void ApplyTraitsAttributes(this EntityPlayer player, TraitsAndClassesLibSystem? system = null)
    {
        system ??= player.Api.ModLoader.GetModSystem<TraitsAndClassesLibSystem>();

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

        IEnumerable<ExtendedTrait> libraryTraits = player.GetTraits(system);
        IEnumerable<ExtendedTrait> extraTraits = player.GetExtraTraits(system);
        IEnumerable<ExtendedTrait> allTraits = libraryTraits.Concat(extraTraits).Distinct();

        // Aggregate stats values
        Dictionary<string, double> statValues = [];
        foreach (ExtendedTrait trait in allTraits)
        {
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
    public static void ReaplyClassesTraits(this EntityPlayer player, TraitsAndClassesLibSystem? system = null)
    {
        system ??= player.Api.ModLoader.GetModSystem<TraitsAndClassesLibSystem>();
        if (system == null) return;

        PlayerTratis traits = PlayerTratis.FromAttributes(player.WatchedAttributes);
        PlayerClasses classes = PlayerClasses.FromAttributes(player.WatchedAttributes);

        system.AddClassesTraits(traits, classes);

        traits.WriteToAttributes(player.WatchedAttributes);
        player.WatchedAttributes.MarkPathDirty(PlayerTratis.AttributeCode);
    }
}