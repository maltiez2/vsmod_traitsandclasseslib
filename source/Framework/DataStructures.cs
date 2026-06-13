using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace TraitsAndClassesLib;


public class ExtendedTrait : Trait
{
    public bool PersistsOnDeath { get; set; } = true;
    public bool PersistsOnWorldReload { get; set; } = true;

    public override string ToString() => Code;
}

public class ClassCategory
{
    public const string VanillaCategoryCode = "game:vanilla";

    public string Code { get; set; } = "";
    public float Order { get; set; } = 0;

    public override string ToString() => Code;
}

public class ExtendedCharacterClass : CharacterClass
{
    public string Category { get; set; } = ClassCategory.VanillaCategoryCode;
    public List<string> RequiredTraitsAndClasses { get; set; } = [];
    public List<string> ForbiddenTraitsAndClasses { get; set; } = [];
    public JsonItemStack[] AdditionalGear { get; set; } = [];

    public override string ToString() => $"{Code} ({Category})";
}

public class TraitsAndClassesFile
{
    public List<ClassCategory> ClassCategories { get; set; } = [];
    public List<ExtendedCharacterClass> Classes { get; set; } = [];
    public List<ExtendedTrait> Traits { get; set; } = [];
}

public class TraitsAndClassesFileDebug
{
    public List<ClassCategory> ClassCategories { get; set; } = [];
    public List<ExtendedCharacterClass> Classes { get; set; } = [];
    public List<ExtendedTrait> Traits { get; set; } = [];
}
