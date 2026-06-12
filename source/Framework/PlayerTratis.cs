using Vintagestory.API.Datastructures;

namespace TraitsAndClassesLib;


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