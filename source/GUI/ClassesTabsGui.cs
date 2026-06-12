using PlayerModelLib;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

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
            if (category.Code == ClassCategory.VanillaCategoryCode) continue;
            
            AddTabToDialog(dialog, category);
        }
    }

    private static void AddTabToDialog(GuiDialogCreateCustomCharacter dialog, ClassCategory category)
    {
        dialog.Tabs.Add(category.Code,
            (GuiDialogCreateCustomCharacter dialog, GuiComposer composer, double yPosition, double padding, double slotSize, ElementBounds backgroundBounds, ElementBounds dialogBounds) =>
                ComposeTab(category, dialog, composer, yPosition, padding, slotSize, backgroundBounds, dialogBounds));
        dialog.TabsEnabled.Add(category.Code, true);
        dialog.OnRenderIntoTab.Add(category.Code, OnRenderPlayerModelToTab);
    }

    private static void ComposeTab(ClassCategory category, GuiDialogCreateCustomCharacter dialog, GuiComposer composer, double yPosition, double padding, double slotSize, ElementBounds backgroundBounds, ElementBounds dialogBounds)
    {
        EntityShapeRenderer? renderer = dialog.Api.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
        renderer?.TesselateShape();

        yPosition -= 25;

        ElementBounds leftColBounds = ElementBounds.Fixed(0, yPosition, 0, GuiDialogCreateCustomCharacter.DlgHeight - 23).FixedGrow(padding, padding);
        ElementBounds prevButtonBounds = ElementBounds.Fixed(0, yPosition + 23, 35, slotSize - 4).WithFixedPadding(2).FixedRightOf(leftColBounds, -10);
        ElementBounds centerTextBounds = ElementBounds.Fixed(0, yPosition + 25, 432, slotSize - 4 - 8).FixedRightOf(prevButtonBounds, 10);
        ElementBounds charclasssInset = centerTextBounds.ForkBoundingParent(4, 4, 4, 4);
        ElementBounds nextButtonBounds = ElementBounds.Fixed(0, yPosition + 23, 35, slotSize - 4).WithFixedPadding(2).FixedRightOf(charclasssInset, 9);

        CairoFont font = CairoFont.WhiteMediumText();
        centerTextBounds.fixedY += (centerTextBounds.fixedHeight - font.GetFontExtents().Height / RuntimeEnv.GUIScale) / 2;

        int visibleHeight = (int)Math.Max(120, GuiDialogCreateCustomCharacter.DlgHeight - (yPosition + 25) - 62);
        ElementBounds charTextBounds = ElementBounds.Fixed(0, 0, 498, visibleHeight)
            .FixedUnder(prevButtonBounds, 15)
            .FixedRightOf(leftColBounds, -10);

        ElementBounds bgBounds = charTextBounds.ForkBoundingParent(6, 6, 6, 6);
        ElementBounds clipBounds = charTextBounds.FlatCopy().FixedGrow(6, 11).WithFixedOffset(0, -6);
        ElementBounds scrollbarBounds = charTextBounds.CopyOffsetedSibling(charTextBounds.fixedWidth + 7, -6, 0, 12).WithFixedWidth(20);


        dialog.InsetSlotBounds = ElementBounds.Fixed(0, yPosition + 25, 193, leftColBounds.fixedHeight - 2 * padding - 30).FixedRightOf(nextButtonBounds, 11);

        composer
            .AddInset(dialog.InsetSlotBounds, 2)
            .AddIconButton("left", (on) => dialog.ChangeClass(-1), prevButtonBounds.FlatCopy())
            .AddInset(charclasssInset, 2)
            .AddDynamicText("Commoner", font.Clone().WithOrientation(EnumTextOrientation.Center), centerTextBounds, "className")
            .AddIconButton("right", (on) => dialog.ChangeClass(1), nextButtonBounds.FlatCopy())

            .BeginChildElements(bgBounds)
                .AddInset(bgBounds.FlatCopy(), 3)
                .BeginClip(clipBounds)
                    .AddRichtext("", CairoFont.WhiteDetailText(), charTextBounds, "characterDesc")
                .EndClip()
                .AddVerticalScrollbar(dialog.OnNewScrollbarValue, scrollbarBounds, "scrollbar")
            .EndChildElements()

            .AddSmallButton(Lang.Get("Confirm Class"), dialog.OnNextImpl,
                ElementBounds.Fixed(11, GuiDialogCreateCustomCharacter.DlgHeight - 24).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(12, 6),
                EnumButtonStyle.Normal)
        ;

        dialog.ClipHeight = (float)clipBounds.fixedHeight;
        composer.GetScrollbar("scrollbar").SetHeights(
            dialog.ClipHeight,
            dialog.ClipHeight
        );
        composer.GetScrollbar("scrollbar").SetScrollbarPosition(0);

        ChangeClass(dialog, 0);

        dialog.OnToggleDressOnOff(false);
    }

    public static void ChangeClass(GuiDialogCreateCustomCharacter dialog, int dir)
    {
        PlayerSkinBehavior? skinMod = dialog.Api.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        CustomModelsSystem system = dialog.Api.ModLoader.GetModSystem<CustomModelsSystem>();

        if (skinMod == null) return;

        List<CharacterClass> availableClasses = dialog.GetAvailableClasses(system, skinMod.CurrentModelCode);

        dialog.CurrentClassIndex = GameMath.Mod(dialog.CurrentClassIndex + dir, availableClasses.Count);

        CharacterClass chclass = availableClasses[dialog.CurrentClassIndex];
        dialog.Composers["createcharacter"]?.GetDynamicText("className").SetNewText(Lang.Get("characterclass-" + chclass.Code));

        StringBuilder fulldesc = new();

        fulldesc.AppendLine();
        fulldesc.AppendLine(Lang.Get("characterdesc-" + chclass.Code));
        fulldesc.AppendLine();
        fulldesc.AppendLine(Lang.Get("traits-title"));


        IOrderedEnumerable<Trait> chartraitsExtra = dialog.CustomModelsSystem.CustomModels[skinMod.CurrentModelCode].ExtraTraits
            .Where(dialog.CharacterSystem.TraitsByCode.ContainsKey)
            .Select(code => dialog.CharacterSystem.TraitsByCode[code])
            .OrderBy(trait => (int)trait.Type);
        IOrderedEnumerable<Trait> chartraits = chclass.Traits
            .Where(code => !dialog.CustomModelsSystem.CustomModels[skinMod.CurrentModelCode].ExtraTraits.Contains(code))
            .Where(dialog.CharacterSystem.TraitsByCode.ContainsKey)
            .Select(code => dialog.CharacterSystem.TraitsByCode[code])
            .OrderBy(trait => (int)trait.Type);

        GuiDialogCreateCustomCharacter.AppendTraits(fulldesc, chartraits);

        if (chclass.Traits.Length == 0)
        {
            fulldesc.AppendLine(Lang.Get("No positive or negative traits"));
        }

        if (chartraitsExtra.Any())
        {
            fulldesc.AppendLine();
            fulldesc.AppendLine(Lang.Get("model-traits-title"));

            GuiDialogCreateCustomCharacter.AppendTraits(fulldesc, chartraitsExtra);
        }

        fulldesc.AppendLine();

        dialog.Composers["createcharacter"].GetRichtext("characterDesc").SetNewText(fulldesc.ToString(), CairoFont.WhiteDetailText());

        dialog.Composer?.GetScrollbar("scrollbar")?.SetHeights(
            dialog.ClipHeight,
            (float)(Math.Max(dialog.ClipHeight, dialog.Composer.GetRichtext("characterDesc")?.TotalHeight ?? dialog.ClipHeight))
        );
        dialog.Composer?.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);

        dialog.CharacterSystem.setCharacterClass(dialog.Api.World.Player.Entity, chclass.Code, true);

        dialog.ReTesselate();
    }

    private static void OnRenderPlayerModelToTab(GuiDialogCreateCustomCharacter dialog, float deltaTime)
    {
        double pad = GuiElement.scaled(GuiElementItemSlotGridBase.unscaledSlotPadding);

        dialog.Api.Render.RenderEntityToGui(
            deltaTime,
            dialog.Api.World.Player.Entity,
            dialog.InsetSlotBounds.renderX + pad - GuiElement.scaled(111),
            dialog.InsetSlotBounds.renderY + pad - GuiElement.scaled(-7),
            (float)GuiElement.scaled(230),
            dialog.Yaw,
            (float)GuiElement.scaled(205),
            ColorUtil.WhiteArgb);
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
            if (!system.ClassesCategories.Any(tab => tab.Code == tabCode)) continue;
            
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