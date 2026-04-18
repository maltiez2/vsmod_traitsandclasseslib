using System.Collections.Immutable;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace TraitsAndClassesLib;

public class ClassCategory
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public float Priority { get; set; } = 0;
}

public class ExtendedCharacterClass : CharacterClass
{
    public List<string> RequiredTraitsAndClasses { get; set; } = [];
    public List<string> ForbiddenTraitsAndClasses { get; set; } = [];
}

public class PlayerTratis
{
    public void AddTrait(string category, ExtendedTrait trait)
    {
        if (TraitsByCategories.TryGetValue(category, out List<ExtendedTrait>? traits))
        {
            if (!traits.Any(existingTrait => existingTrait.Code == trait.Code))
            {
                traits.Add(trait);
            }
        }
        else
        {
            TraitsByCategories[category] = [trait];
        }
    }

    public void RemoveTrait(string category, string trait)
    {
        if (!TraitsByCategories.TryGetValue(category, out List<ExtendedTrait>? traits))
        {
            return;
        }

        for (int traitIndex = 0; traitIndex < traits.Count; traitIndex++)
        {
            if (traits[traitIndex].Code == trait)
            {
                traits.RemoveAt(traitIndex);
                return;
            }
        }
    }

    public void RemoveAllTraits(string category)
    {
        TraitsByCategories.Remove(category);
    }

    public IEnumerable<ExtendedTrait> GetTraits()
    {
        HashSet<string> returnedTraits = [];
        foreach ((string category, List<ExtendedTrait> traits) in TraitsByCategories)
        {
            foreach (ExtendedTrait trait in traits)
            {
                if (returnedTraits.Contains(trait.Code))
                {
                    continue;
                }

                returnedTraits.Add(trait.Code);
                yield return trait;
            }
        }
    }

    public void RemoveTraitsOnDeath()
    {
        foreach ((_, List<ExtendedTrait> traits) in TraitsByCategories)
        {
            for (int traitIndex = traits.Count - 1; traitIndex >= 0; traitIndex--)
            {
                if (!traits[traitIndex].PersistsOnDeath)
                {
                    traits.RemoveAt(traitIndex);
                }
            }
        }
    }

    public void RemoveTraitsOnReload()
    {
        foreach ((_, List<ExtendedTrait> traits) in TraitsByCategories)
        {
            for (int traitIndex = traits.Count - 1; traitIndex >= 0; traitIndex--)
            {
                if (!traits[traitIndex].PersistsOnWorldReload)
                {
                    traits.RemoveAt(traitIndex);
                }
            }
        }
    }

    public void ReadFromAttributes(TreeAttribute tree, Dictionary<string, ExtendedTrait> traits)
    {
        TraitsByCategories.Clear();

        if (tree["traitCategories"] is not TreeAttribute categoriesTree)
        {
            RemoveTraitsOnReload();
            return;
        }

        foreach (KeyValuePair<string, IAttribute> categoryEntry in categoriesTree)
        {
            string category = categoryEntry.Key;

            if (categoryEntry.Value is not StringArrayAttribute traitCodes)
            {
                continue;
            }

            List<ExtendedTrait> newTraits = [];

            foreach (string code in traitCodes.value)
            {
                if (traits.TryGetValue(code, out ExtendedTrait? trait))
                {
                    newTraits.Add(trait);
                }
            }

            if (traits.Count > 0)
            {
                TraitsByCategories[category] = newTraits;
            }
        }

        RemoveTraitsOnReload();
    }

    public void WriteToAttributes(TreeAttribute tree)
    {
        TreeAttribute categoriesTree = new();

        foreach ((string category, List<ExtendedTrait> traits) in TraitsByCategories)
        {
            if (traits.Count == 0)
            {
                continue;
            }

            string[] codes = traits
                .Select(trait => trait.Code)
                .ToArray();

            categoriesTree[category] = new StringArrayAttribute(codes);
        }

        tree["traitCategories"] = categoriesTree;
    }

    public static PlayerTratis FromAttributes(TreeAttribute tree, Dictionary<string, ExtendedTrait> traits)
    {
        PlayerTratis result = new();
        result.ReadFromAttributes(tree, traits);
        return result;
    }

    protected Dictionary<string, List<ExtendedTrait>> TraitsByCategories { get; set; } = [];
}

public class ExtendedTrait : Trait
{
    public bool PersistsOnDeath { get; set; } = true;
    public bool PersistsOnWorldReload { get; set; } = true;
}

public sealed class TraitsAndClassesLibSystem : ModSystem
{
    public List<ClassCategory> ClassesCategories { get; private set; } = [];
    public Dictionary<string, List<ExtendedCharacterClass>> Classes { get; private set; } = [];
    public Dictionary<string, ExtendedTrait> Traits { get; private set; } = [];


    public void RegisterClassCategory(ClassCategory category)
    {
        ClassesCategories.Add(category);
    }
    public void RegisterClass(string category, ExtendedCharacterClass characterCLass)
    {
        if (Classes.TryGetValue(category, out List<ExtendedCharacterClass>? classes))
        {
            classes.Add(characterCLass);
        }
        else
        {
            Classes[category] = [characterCLass];
        }
    }
    public void RegisterTrait(ExtendedTrait trait)
    {
        Traits[trait.Code] = trait;
    }


    public override void Start(ICoreAPI api)
    {
        _api = api;
    }

    public override void AssetsFinalize(ICoreAPI api)
    {

    }


    internal Dictionary<string, PlayerTratis> _playerTraitsCache = [];
    private ICoreAPI? _api;
}

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

        if (system._playerTraitsCache.TryGetValue(player.PlayerUID, out PlayerTratis traits))
        {
            return traits;
        }
        else
        {
            traits = PlayerTratis.FromAttributes(player.WatchedAttributes);
            system._playerTraitsCache.Add(player.PlayerUID, traits);
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
            LoggerUtil.Error(player.Api, typeof(EntityPlayerExtensions), $"Character class with code '{classCode}' not found when trying to apply class traits for player '{player.Player?.PlayerName ?? player.GetName()}'.");
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