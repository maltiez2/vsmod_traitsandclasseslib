using OverhaulLib.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace TraitsAndClassesLib;


public sealed class TraitsAndClassesLibSystem : ModSystem
{
    public List<ClassCategory> ClassesCategories { get; private set; } = [];
    public Dictionary<string, List<ExtendedCharacterClass>> Classes { get; private set; } = [];
    public Dictionary<string, ExtendedTrait> Traits { get; private set; } = [];
    public Dictionary<string, PlayerTratis> PlayerTraitsCache { get; set; } = [];
    public Dictionary<string, PlayerClasses> PlayerClassesCache { get; set; } = [];

    public const string TratisAndClassesFolder = "config/classesandtraits";
    public const string ClassTraitsCategory = "from-character-class";


    public void RegisterClassCategory(ClassCategory category)
    {
        ClassesCategories.Add(category);
    }
    public void RegisterClass(ExtendedCharacterClass characterCLass)
    {
        if (Classes.TryGetValue(characterCLass.Category, out List<ExtendedCharacterClass>? classes))
        {
            classes.Add(characterCLass);
        }
        else
        {
            Classes[characterCLass.Category] = [characterCLass];
        }
    }
    public void RegisterTrait(ExtendedTrait trait)
    {
        Traits[trait.Code] = trait;
    }
    public void AddClassesTraits(PlayerTratis traits, PlayerClasses classes)
    {
        traits.RemoveAllTraits(ClassTraitsCategory);
        foreach (ExtendedCharacterClass playerClass in classes.GetClasses())
        {
            foreach (string trait in playerClass.Traits)
            {
                if (!Traits.ContainsKey(trait))
                {
                    Log.Warn(_api, this, $"Unable to find trait with code '{trait}' specified in class '{playerClass.Code}'");
                    continue;
                }

                traits.AddTrait(ClassTraitsCategory, Traits[trait]);
            }
        }
    }
    public JsonItemStack[] GetClassEquipment(PlayerClasses classes)
    {
        ExtendedCharacterClass? classWithGear = classes.GetClasses()
            .Where(playerClass => playerClass.Gear != null && playerClass.Gear.Length > 0)
            .OrderByDescending(playerClass => ClassesCategories.FirstOrDefault(category => category.Code == playerClass.Category)?.Order ?? 0)
            .FirstOrDefault();

        return classWithGear != null ? classWithGear.Gear : [];
    }


    public override void Start(ICoreAPI api)
    {
        _api = api;
    }
    public override void AssetsFinalize(ICoreAPI api)
    {
        LoadVanillaTraitsAndClasses(api);
        LoadTraitsAndClassesFromAssets(api);

        if (api is ICoreClientAPI clientApi)
        {
            ClassesCategories = ClassesCategories.OrderBy(category => category.Order).ToList();
            ClassesTabsGui.Init(clientApi);
        }
    }
    public override void Dispose()
    {
        ClassesTabsGui.Dispose();
    }



    private ICoreAPI? _api;

    private void LoadVanillaTraitsAndClasses(ICoreAPI api)
    {
        List<IAsset> classesAssets = api.Assets.GetMany("config/characterclasses");
        foreach (IAsset asset in classesAssets)
        {
            LoadVanillaClassesFromFile(api, asset);
        }

        List<IAsset> traitsAssets = api.Assets.GetMany("config/traits");
        foreach (IAsset asset in traitsAssets)
        {
            LoadVanillaTraitsFromFile(api, asset);
        }
    }
    private void LoadVanillaClassesFromFile(ICoreAPI api, IAsset asset)
    {
        List<ExtendedCharacterClass>? playerClasses = ParsingUtils.LoadObjectFromFile<List<ExtendedCharacterClass>>(asset, _api, this);
        if (playerClasses == null) return;

        foreach (ExtendedCharacterClass playerClass in playerClasses)
        {
            FillMissingDomains(asset.Location.Domain, playerClass);
            RegisterClass(playerClass);
        }
    }
    private void LoadVanillaTraitsFromFile(ICoreAPI api, IAsset asset)
    {
        List<ExtendedTrait>? traits = ParsingUtils.LoadObjectFromFile<List<ExtendedTrait>>(asset, _api, this);
        if (traits == null) return;

        foreach (ExtendedTrait trait in traits)
        {
            FillMissingDomains(asset.Location.Domain, trait);
            RegisterTrait(trait);
        }
    }

    private void LoadTraitsAndClassesFromAssets(ICoreAPI api)
    {
        List<IAsset> assets = api.Assets.GetMany(TratisAndClassesFolder);

        foreach (IAsset asset in assets)
        {
            LoadTraitsAndClassesFromAsset(api, asset);
        }
    }
    private void LoadTraitsAndClassesFromAsset(ICoreAPI api, IAsset asset)
    {
        TraitsAndClassesFile? fileConent = ParsingUtils.LoadObjectFromFile<TraitsAndClassesFile>(asset, _api, this);
        if (fileConent == null) return;

        foreach (ClassCategory category in fileConent.ClassCategories)
        {
            FillMissingDomains(asset.Location.Domain, category);
            RegisterClassCategory(category);
        }

        foreach (ExtendedCharacterClass playerClass in fileConent.Classes)
        {
            FillMissingDomains(asset.Location.Domain, playerClass);
            RegisterClass(playerClass);
        }

        foreach (ExtendedTrait trait in fileConent.Traits)
        {
            FillMissingDomains(asset.Location.Domain, trait);
            RegisterTrait(trait);
        }
    }
    private void FillMissingDomains(string domain, ClassCategory category)
    {
        category.Code = AddDomainIfMissing(category.Code, domain);
    }
    private void FillMissingDomains(string domain, ExtendedCharacterClass playerClass)
    {
        playerClass.Code = AddDomainIfMissing(playerClass.Code, domain);
        playerClass.RequiredTraitsAndClasses = playerClass.RequiredTraitsAndClasses.Select(code => AddDomainIfMissing(code, domain)).ToList();
        playerClass.ForbiddenTraitsAndClasses = playerClass.ForbiddenTraitsAndClasses.Select(code => AddDomainIfMissing(code, domain)).ToList();
    }
    private void FillMissingDomains(string domain, ExtendedTrait trait)
    {
        trait.Code = AddDomainIfMissing(trait.Code, domain);
    }
    private static string AddDomainIfMissing(string value, string domain)
    {
        return domain.Contains(':') ? value : domain + ":" + value;
    }
}
