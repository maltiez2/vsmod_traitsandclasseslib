using Vintagestory.API.Datastructures;

namespace TraitsAndClassesLib;


public class PlayerClasses
{
    public const string AttributeCode = "classesCategories";


    public void SetClass(string category, ExtendedCharacterClass playerClass)
    {
        ClassesByCategories[category] = playerClass;
    }

    public void ClearClass(string category)
    {
        ClassesByCategories.Remove(category);
    }

    public ExtendedCharacterClass? GetClass(string category)
    {
        ClassesByCategories.TryGetValue(category, out ExtendedCharacterClass? playerClass);
        return playerClass;
    }

    public IEnumerable<ExtendedCharacterClass> GetClasses()
    {
        HashSet<string> returnedPlayerClasses = [];
        foreach ((_, ExtendedCharacterClass playerClass) in ClassesByCategories)
        {
            if (returnedPlayerClasses.Contains(playerClass.Code))
            {
                continue;
            }

            returnedPlayerClasses.Add(playerClass.Code);
            yield return playerClass;
        }
    }

    public void ReadFromAttributes(TreeAttribute tree, Dictionary<string, List<ExtendedCharacterClass>> registeredClasses)
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

            string classCode = classCodeAttribute.value;

            if (registeredClasses.TryGetValue(category, out List<ExtendedCharacterClass>? playerClasses))
            {
                ClassesByCategories[category] = playerClasses.First(playerClass => playerClass.Code == classCode);
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

    public static PlayerClasses FromAttributes(TreeAttribute tree, Dictionary<string, List<ExtendedCharacterClass>> registeredClasses)
    {
        PlayerClasses result = new();
        result.ReadFromAttributes(tree, registeredClasses);
        return result;
    }

    public static PlayerClasses FromAttributes(TreeAttribute tree, TraitsAndClassesLibSystem system)
    {
        PlayerClasses result = new();
        result.ReadFromAttributes(tree, system.Classes);
        return result;
    }

    protected Dictionary<string, ExtendedCharacterClass> ClassesByCategories { get; set; } = [];
}