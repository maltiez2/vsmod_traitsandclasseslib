using HarmonyLib;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace TraitsAndClassesLib;


public class PlayerClasses
{
    public const string AttributeCode = "classesCategories";


    public void AddClass(string category, ExtendedCharacterClass playerClass)
    {
        ClassesByCategories[category] = playerClass;
    }

    public void RemoveClass(string category)
    {
        ClassesByCategories.Remove(category);
    }

    public IEnumerable<ExtendedCharacterClass> GetClasses()
    {
        HashSet<string> returnedPlayerClasses = [];
        foreach ((string category, ExtendedCharacterClass playerClass) in ClassesByCategories)
        {
            if (returnedPlayerClasses.Contains(playerClass.Code))
            {
                continue;
            }

            returnedPlayerClasses.Add(playerClass.Code);
            yield return playerClass;
        }
    }

    public void ReadFromAttributes(TreeAttribute tree, Dictionary<string, ExtendedCharacterClass> playerClasses)
    {
        ClassesByCategories.Clear();

        if (tree[AttributeCode] is not TreeAttribute categoriesTree)
        {
            return;
        }

        foreach ((string category, IAttribute? categoryValue) in categoriesTree)
        {
            if (categoryValue is not StringAttribute classCodeAttribute)
            {
                continue;
            }

            if (playerClasses.TryGetValue(classCodeAttribute.value, out ExtendedCharacterClass? playerClass))
            {
                ClassesByCategories[category] = playerClass;
            }
        }
    }

    public void WriteToAttributes(TreeAttribute tree)
    {
        TreeAttribute categoriesTree = new();

        foreach ((string category, ExtendedCharacterClass playerClass) in ClassesByCategories)
        {
            categoriesTree[category] = new StringAttribute(playerClass.Code);
        }

        tree[AttributeCode] = categoriesTree;
    }

    public static PlayerClasses FromAttributes(TreeAttribute tree, Dictionary<string, ExtendedCharacterClass> playerClasses)
    {
        PlayerClasses result = new();
        result.ReadFromAttributes(tree, playerClasses);
        return result;
    }

    public static PlayerClasses FromAttributes(TreeAttribute tree)
    {
        PlayerClasses result = new();
        Dictionary<string, ExtendedCharacterClass> playerClasses = [];
        result.ReadFromAttributes(tree, playerClasses);
        return result;
    }

    protected Dictionary<string, ExtendedCharacterClass> ClassesByCategories { get; set; } = [];
}