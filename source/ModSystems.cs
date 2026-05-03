using System.Collections.Immutable;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace TraitsAndClassesLib;


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
