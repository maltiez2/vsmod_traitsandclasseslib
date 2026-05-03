using Vintagestory.API.Datastructures;
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
    public string Name { get; set; } = "";
    public float Priority { get; set; } = 0;
}

public class ExtendedCharacterClass : CharacterClass
{
    public List<string> RequiredTraitsAndClasses { get; set; } = [];
    public List<string> ForbiddenTraitsAndClasses { get; set; } = [];
}
