using Vintagestory.GameContent;

namespace TraitsAndClassesLib;


public class ExtendedTrait : Trait
{
    public bool PersistsOnDeath { get; set; } = true;
    public bool PersistsOnWorldReload { get; set; } = true;
}

public class ClassCategory
{
    public string Code { get; set; } = "";
    public float Order { get; set; } = 0;
}

public class ExtendedCharacterClass : CharacterClass
{
    public string Category { get; set; } = "vanilla";
    public List<string> RequiredTraitsAndClasses { get; set; } = [];
    public List<string> ForbiddenTraitsAndClasses { get; set; } = [];
}

public class TraitsAndClassesFile
{
    public List<ClassCategory> ClassCategories { get; set; } = [];
    public List<ExtendedCharacterClass> Classes { get; set; } = [];
    public List<ExtendedTrait> Traits { get; set; } = [];
}
