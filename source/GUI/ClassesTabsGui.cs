using PlayerModelLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace TraitsAndClassesLib;

public static class ClassesTabsGui
{
    public static void Init(ICoreClientAPI api)
    {
        _api = api;
        GuiDialogCreateCustomCharacter.OnCreated += OnDialogCreated;
        GuiDialogCreateCustomCharacter.BeforeComposed += BeforeDialogComposed;
    }

    public static void Dispose()
    {
        _api = null;
    }



    private static ICoreClientAPI? _api;


    private static void OnDialogCreated(GuiDialogCreateCustomCharacter dialog)
    {
        TraitsAndClassesLibSystem? system = _api?.ModLoader.GetModSystem<TraitsAndClassesLibSystem>();
        if (system == null) return;

        foreach (ClassCategory category in system.ClassesCategories)
        {
            AddTabToDialog(dialog, category);
        }
    }

    private static void AddTabToDialog(GuiDialogCreateCustomCharacter dialog, ClassCategory category)
    {
        dialog.Tabs.Add(category.Code,
            (GuiDialogCreateCustomCharacter dialog, GuiComposer composer, double yPosition, double padding, double slotSize, ElementBounds backgroundBounds, ElementBounds dialogBounds) =>
                ComposeTab(category, dialog, composer, yPosition, padding, slotSize, backgroundBounds, dialogBounds));
        dialog.TabsEnabled.Add(category.Code, false);
    }

    private static void ComposeTab(ClassCategory category, GuiDialogCreateCustomCharacter dialog, GuiComposer composer, double yPosition, double padding, double slotSize, ElementBounds backgroundBounds, ElementBounds dialogBounds)
    {

    }


    private static void BeforeDialogComposed(GuiDialogCreateCustomCharacter dialog)
    {
        ReenableTabs(dialog);
    }

    private static void ReenableTabs(GuiDialogCreateCustomCharacter dialog)
    {
        EntityPlayer? player = _api?.World.Player?.Entity;
        TraitsAndClassesLibSystem? system = _api?.ModLoader.GetModSystem<TraitsAndClassesLibSystem>();
        if (player == null || system == null) return;

        GetAvailableClasses(system, player, out HashSet<string> availableCategories, out _);

        foreach ((string tabCode, _) in dialog.TabsEnabled)
        {
            if (availableCategories.Contains(tabCode))
            {
                dialog.TabsEnabled[tabCode] = true;
            }
            else
            {
                dialog.TabsEnabled[tabCode] = false;
            }
        }
    }

    private static void GetAvailableClasses(TraitsAndClassesLibSystem system, EntityPlayer player, out HashSet<string> availableCategories, out HashSet<string> availableClasses)
    {
        IEnumerable<string> traits = player.GetTraits(system).Select(trait => trait.Code);

        availableClasses = [];
        availableCategories = [];

        foreach ((string categoryCode, List<ExtendedCharacterClass> categoryClasses) in system.Classes)
        {
            foreach (ExtendedCharacterClass playerClass in categoryClasses)
            {
                if (ClassAvailable(playerClass, traits))
                {
                    availableClasses.Add(playerClass.Code);
                    availableCategories.Add(categoryCode);
                }
            }
        }
    }

    private static bool ClassAvailable(ExtendedCharacterClass playerClass, IEnumerable<string> traits)
    {
        foreach (string requiredTrait in playerClass.RequiredTraitsAndClasses)
        {
            if (!traits.Contains(requiredTrait))
            {
                return false;
            }
        }

        foreach (string requiredTrait in playerClass.ForbiddenTraitsAndClasses)
        {
            if (traits.Contains(requiredTrait))
            {
                return false;
            }
        }

        return true;
    }
}