using Newtonsoft.Json.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace TraitsAndClassesLib;

public class CharacterSystem : ModSystem
{
    public List<CharacterClass> characterClasses = new List<CharacterClass>();
    public List<Trait> traits = new List<Trait>();
    public Dictionary<string, CharacterClass> characterClassesByCode = new Dictionary<string, CharacterClass>();
    public Dictionary<string, Trait> TraitsByCode = new Dictionary<string, Trait>();
    public bool PrintLoadWarnings = true;

    

    public override void Start(ICoreAPI api)
    {
        this.api = api;

        api.Network
            .RegisterChannel("charselection")
            .RegisterMessageType<CharacterSelectionPacket>()
            .RegisterMessageType<CharacterSelectedState>()
        ;

        api.Event.MatchesGridRecipe += Event_MatchesGridRecipe;
        api.Event.MatchesRecipe += Event_MatchesRecipe;
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        this.capi = api;

        api.Event.BlockTexturesLoaded += onLoadedUniversal;

        api.Network.GetChannel("charselection")
            .SetMessageHandler<CharacterSelectedState>(onSelectedState)
        ;

        api.Event.IsPlayerReady += Event_IsPlayerReady;
        api.Event.PlayerJoin += Event_PlayerJoin;

        this.api.ChatCommands.Create("charsel")
            .WithDescription("Open the character selection menu")
            .HandleWith(onCharSelCmd);

        api.Event.BlockTexturesLoaded += loadCharacterClasses;

        charDlg = api.Gui.LoadedGuis.Find(dlg => dlg is GuiDialogCharacterBase) as GuiDialogCharacterBase;
        charDlg.Tabs.Add(new GuiTab() { Name = Lang.Get("charactertab-traits"), DataInt = 1 });
        charDlg.RenderTabHandlers.Add(composeTraitsTab);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;

        api.Network.GetChannel("charselection")
            .SetMessageHandler<CharacterSelectionPacket>(onCharacterSelection)
        ;

        api.Event.PlayerJoin += Event_PlayerJoinServer;
        api.Event.ServerRunPhase(EnumServerRunPhase.ModsAndConfigReady, loadCharacterClasses);
    }

    public void SetCharacterClass(EntityPlayer player, string classCode, bool initializeGear = true)
    {
        if (initializeGear) player.GiveClassEquipment();
        player.UpdaitTraits();
    }

    public void ClientSelectionDone(IInventory characterInv, string characterClass, bool didSelect)
    {
        List<ClothStack> clothesPacket = new List<ClothStack>();
        for (int i = 0; i < characterInv.Count; i++)
        {
            ItemSlot slot = characterInv[i];
            if (slot.Itemstack == null) continue;

            clothesPacket.Add(new ClothStack()
            {
                Code = slot.Itemstack.Collectible.Code.ToShortString(),
                SlotNum = i,
                Class = slot.Itemstack.Class
            });
        }

        Dictionary<string, string> skinParts = new Dictionary<string, string>();
        var bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();

        var applied = bh.AppliedSkinParts;
        foreach (var val in applied)
        {
            skinParts[val.PartCode] = val.Code;
        }
        if (didSelect) storePreviousSelection(skinParts);

        capi.Network.GetChannel("charselection").SendPacket(new CharacterSelectionPacket()
        {
            Clothes = clothesPacket.ToArray(),
            DidSelect = didSelect,
            SkinParts = skinParts,
            CharacterClass = characterClass,
            VoicePitch = bh.VoicePitch,
            VoiceType = bh.VoiceType
        });

        capi.Network.SendPlayerNowReady();

        createCharDlg = null;

        capi.Event.PushEvent("finishcharacterselection");
    }


    private ICoreAPI? api;
    private ICoreClientAPI? capi;
    private ICoreServerAPI? sapi;


    private void onLoadedUniversal()
    {
        randomizerConstraints = api.Assets.Get("config/seraphrandomizer.json").ToObject<SeraphRandomizerConstraints>();
    }

    private void composeTraitsTab(GuiComposer compo)
    {
        compo
            .AddRichtext(getClassTraitText(), CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15), ElementBounds.Fixed(0, 25, 385, 200));
    }

    private string getClassTraitText()
    {
        string charClass = capi.World.Player.Entity.WatchedAttributes.GetString("characterClass");
        CharacterClass chclass = characterClasses.FirstOrDefault(c => c.Code == charClass);

        StringBuilder fulldesc = new StringBuilder();
        StringBuilder attributes = new StringBuilder();

        var chartraits = chclass.Traits.Select(code => TraitsByCode[code]).OrderBy(trait => (int)trait.Type);

        foreach (var trait in chartraits)
        {
            attributes.Clear();
            foreach (var val in trait.Attributes)
            {
                if (attributes.Length > 0) attributes.Append(", ");
                attributes.Append(Lang.Get(string.Format(GlobalConstants.DefaultCultureInfo, "charattribute-{0}-{1}", val.Key, val.Value)));
            }

            if (attributes.Length > 0)
            {
                fulldesc.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + trait.Code), attributes));
            }
            else
            {
                string desc = Lang.GetIfExists("traitdesc-" + trait.Code);
                if (desc != null)
                {
                    fulldesc.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + trait.Code), desc));
                }
                else
                {
                    fulldesc.AppendLine(Lang.Get("trait-" + trait.Code));
                }
            }
        }

        if (chclass.Traits.Length == 0)
        {
            fulldesc.AppendLine(Lang.Get("No positive or negative traits"));
        }

        return fulldesc.ToString();
    }

    private void loadCharacterClasses()
    {
        onLoadedUniversal();
        LoadTraits();
        LoadClasses();

        foreach (var trait in traits)
        {
            TraitsByCode[trait.Code] = trait;
        }

        foreach (var charclass in characterClasses)
        {
            characterClassesByCode[charclass.Code] = charclass;

            foreach (var jstack in charclass.Gear)
            {
                if (!jstack.Resolve(api.World, "character class gear", false))
                {
                    api.World.Logger.Warning("Unable to resolve character class gear " + jstack.Type + " with code " + jstack.Code + " item/block does not seem to exist. Will ignore.");
                }
            }
        }
    }

    private void LoadTraits()
    {
        traits = [];
        Dictionary<AssetLocation, JToken> files = api.Assets.GetMany<JToken>(api.Logger, "config/traits");
        int traitQuantity = 0;

        string[] vanillaTraitsInOrder = ["focused", "resourceful", "fleetfooted", "bowyer", "forager", "pilferer", "furtive",
                "precise", "technical", "soldier", "hardy", "clothier", "mender", "merciless", "farsighted", "claustrophobic",
                "frail", "nervous", "ravenous", "nearsighted", "heavyhanded", "kind", "weak", "civil", "improviser", "tinkerer"];
        HashSet<string> vanillaTraits = [.. vanillaTraitsInOrder];

        foreach ((AssetLocation path, JToken fileToken) in files)
        {
            if (fileToken is JObject)
            {
                Trait trait = fileToken.ToObject<Trait>(path.Domain);
                if (traits.Find(element => element.Code == trait.Code) != null)
                {
                    if (PrintLoadWarnings) api.World.Logger.Warning($"Trying to add character trait from domain '{path.Domain}', but character trait with code '{trait.Code}' already exists. Will add it anyway, but it can cause undefined behavior.");
                }
                traits.Add(trait);
                traitQuantity++;
            }
            if (fileToken is JArray fileArray)
            {
                int traitIndex = 0;
                foreach (JToken traitToken in fileArray)
                {
                    Trait trait = traitToken.ToObject<Trait>(path.Domain);
                    if (traits.Find(element => element.Code == trait.Code) != null)
                    {
                        if (PrintLoadWarnings) api.World.Logger.Warning($"Trying to add character trait from domain '{path.Domain}', but character trait with code '{trait.Code}' already exists. Will add it anyway, but it can cause undefined behavior.");
                    }
                    if (path.Domain == "game")
                    {
                        vanillaTraits.Remove(trait.Code);
                        if (!vanillaTraitsInOrder.Contains(trait.Code))
                        {
                            if (PrintLoadWarnings) api.World.Logger.Warning($"Instead of json patching in new traits into vanilla asset, add 'traits.json' into 'config' folder in your mod domain with new traits.");
                        }
                        else if (vanillaTraitsInOrder.IndexOf(trait.Code) != traitIndex)
                        {
                            if (PrintLoadWarnings) api.World.Logger.Warning($"Order of vanilla character traits has changed. Dont remove vanilla character traits or add new traits between or before vanilla traits. That will cause incompatibility with other mods that change traits, that can result in crashes.");
                        }
                    }
                    traits.Add(trait);
                    traitQuantity++;
                    traitIndex++;
                }
            }
        }

        if (vanillaTraits.Count > 0 && PrintLoadWarnings)
        {
            api.World.Logger.Warning($"Failed to find vanilla traits: {vanillaTraits.Aggregate((a, b) => $"{a}, {b}")}, dont remove vanilla traits, it will cause incompatibility with other mods that change traits or classes, that can result in crashes.");
        }

        api.World.Logger.Event($"{traitQuantity} traits loaded from {files.Count} files");
    }

    private void LoadClasses()
    {
        characterClasses = [];
        Dictionary<AssetLocation, JToken> files = api.Assets.GetMany<JToken>(api.Logger, "config/characterclasses");
        int classQuantity = 0;

        string[] vanillaClassesInOrder = ["commoner", "hunter", "malefactor", "clockmaker", "blackguard", "tailor"];
        HashSet<string> vanillaClasses = [.. vanillaClassesInOrder];

        foreach ((AssetLocation path, JToken file) in files)
        {
            if (file is JObject)
            {
                CharacterClass characterClass = file.ToObject<CharacterClass>(path.Domain);
                if (!characterClass.Enabled) continue;
                if (characterClasses.Find(element => element.Code == characterClass.Code) != null)
                {
                    if (PrintLoadWarnings) api.World.Logger.Warning($"Trying to add character class from domain '{path.Domain}', but character class with code '{characterClass.Code}' already exists. Will add it anyway, but it can cause undefined behavior.");
                }
                characterClasses.Add(characterClass);
                classQuantity++;
            }
            if (file is JArray fileArray)
            {
                int classIndex = 0;
                foreach (JToken classToken in fileArray)
                {
                    CharacterClass characterClass = classToken.ToObject<CharacterClass>(path.Domain);
                    if (!characterClass.Enabled) continue;
                    if (characterClasses.Find(element => element.Code == characterClass.Code) != null)
                    {
                        if (PrintLoadWarnings) api.World.Logger.Warning($"Trying to add character class from domain '{path.Domain}', but character class with code '{characterClass.Code}' already exists. Will add it anyway, but it can cause undefined behavior.");
                    }
                    if (path.Domain == "game")
                    {
                        vanillaClasses.Remove(characterClass.Code);
                        if (!vanillaClassesInOrder.Contains(characterClass.Code))
                        {
                            if (PrintLoadWarnings) api.World.Logger.Warning($"Instead of json patching in new classes into vanilla asset, add 'characterclasses.json' into 'config' folder in your mod domain with new classes.");
                        }
                        else if (vanillaClassesInOrder.IndexOf(characterClass.Code) != classIndex)
                        {
                            if (PrintLoadWarnings) api.World.Logger.Warning($"Order of vanilla character classes has changed. Dont remove vanilla character classes (set 'enabled' attribute to 'false' instead) or add new classes between or before vanilla classes. That will cause incompatibility with other mods that change classes, that can result in crashes.");
                        }
                    }
                    characterClasses.Add(characterClass);
                    classQuantity++;
                    classIndex++;
                }
            }
        }

        if (vanillaClasses.Count > 0 && PrintLoadWarnings)
        {
            api.World.Logger.Warning($"Failed to find vanilla classes: {vanillaClasses.Aggregate((a, b) => $"{a}, {b}")}, dont remove vanilla classes (set 'enabled' attribute to 'false' instead), it will cause incompatibility with other mods that change classes, that can result in crashes.");
        }

        api.World.Logger.Event($"{classQuantity} classes loaded from {files.Count} files");
    }

    private void applyTraitAttributes(EntityPlayer eplr)
    {
        string classcode = eplr.WatchedAttributes.GetString("characterClass");
        CharacterClass charclass = characterClasses.FirstOrDefault(c => c.Code == classcode);
        if (charclass == null) throw new ArgumentException("Not a valid character class code!");

        foreach (var stats in eplr.Stats)
        {
            foreach (var statmod in stats.Value.ValuesByKey)
            {
                if (statmod.Key == "trait")
                {
                    stats.Value.Remove(statmod.Key);
                    break;
                }
            }
        }

        string[] extraTraits = eplr.WatchedAttributes.GetStringArray("extraTraits");
        var allTraits = extraTraits == null ? charclass.Traits : charclass.Traits.Concat(extraTraits);

        foreach (var traitcode in allTraits)
        {
            if (TraitsByCode.TryGetValue(traitcode, out Trait trait))
            {
                foreach (var val in trait.Attributes)
                {
                    string attrcode = val.Key;
                    double attrvalue = val.Value;

                    eplr.Stats.Set(attrcode, "trait", (float)attrvalue, true);

                    if ((trait.AttributeBlendTypes != null) && trait.AttributeBlendTypes.TryGetValue(attrcode, out var blend))
                    {
                        eplr.Stats[attrcode].BlendType = blend;
                    }
                }
            }
        }

        eplr.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();
    }

    private TextCommandResult onCharSelCmd(TextCommandCallingArgs textCommandCallingArgs)
    {
        var allowcharselonce = capi.World.Player.Entity.WatchedAttributes.GetBool("allowcharselonce") || capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative;

        if (createCharDlg == null && allowcharselonce)
        {
            createCharDlg = new GuiDialogCreateCharacter(capi, this);
            createCharDlg.PrepAndOpen();
        }
        else if (createCharDlg == null)
        {
            return TextCommandResult.Success(Lang.Get("You don't have permission to change you character and class. An admin needs to grant you allowcharselonce permission"));
        }

        if (!createCharDlg.IsOpened())
        {
            createCharDlg.TryOpen();
        }
        return TextCommandResult.Success();
    }

    private void onSelectedState(CharacterSelectedState p)
    {
        didSelect = p.DidSelect;
    }

    private void Event_PlayerJoin(IClientPlayer byPlayer)
    {
        if (byPlayer.PlayerUID == capi.World.Player.PlayerUID)
        {
            if (!didSelect)
            {
                createCharDlg = new GuiDialogCreateCharacter(capi, this);
                createCharDlg.PrepAndOpen();
                createCharDlg.OnClosed += () => capi.PauseGame(false);
                capi.Event.EnqueueMainThreadTask(() => capi.PauseGame(true), "pausegame");
                capi.Event.PushEvent("begincharacterselection");
            }
            else
            {
                capi.Event.PushEvent("skipcharacterselection");
            }
        }
    }

    private bool Event_IsPlayerReady(ref EnumHandling handling)
    {
        if (didSelect) return true;

        handling = EnumHandling.PreventDefault;
        return false;
    }

    private bool Event_MatchesGridRecipe(IPlayer player, GridRecipe recipe, ItemSlot[] ingredients, int gridWidth)
    {
        if (recipe.RequiresTrait == null) return true;

        string classcode = player.Entity.WatchedAttributes.GetString("characterClass");
        if (classcode == null) return true;

        if (characterClassesByCode.TryGetValue(classcode, out CharacterClass charclass))
        {
            if (charclass.Traits.Contains(recipe.RequiresTrait)) return true;

            string[] extraTraits = player.Entity.WatchedAttributes.GetStringArray("extraTraits");
            if (extraTraits != null && extraTraits.Contains(recipe.RequiresTrait)) return true;
        }

        return false;
    }

    private bool Event_MatchesRecipe(IPlayer player, IRecipeBase recipe, ItemSlot[] ingredients)
    {
        if (recipe.RequiresTrait == null) return true;

        string classcode = player.Entity.WatchedAttributes.GetString("characterClass");
        if (classcode == null) return true;

        if (characterClassesByCode.TryGetValue(classcode, out CharacterClass charclass))
        {
            if (charclass.Traits.Contains(recipe.RequiresTrait)) return true;

            string[] extraTraits = player.Entity.WatchedAttributes.GetStringArray("extraTraits");
            if (extraTraits != null && extraTraits.Contains(recipe.RequiresTrait)) return true;
        }

        return false;
    }

    private void Event_PlayerJoinServer(IServerPlayer byPlayer)
    {
        didSelect = SerializerUtil.Deserialize(byPlayer.GetModdata("createCharacter"), false);

        if (!didSelect)
        {
            SetCharacterClass(byPlayer.Entity, characterClasses[0].Code, false);
        }

        var classChangeMonths = sapi.World.Config.GetDecimal("allowClassChangeAfterMonths", -1);
        var allowOneFreeClassChange = sapi.World.Config.GetBool("allowOneFreeClassChange");

        if (allowOneFreeClassChange && byPlayer.ServerData.LastCharacterSelectionDate == null)
        {
            byPlayer.Entity.WatchedAttributes.SetBool("allowcharselonce", true);
        }
        else if (classChangeMonths >= 0)
        {
            var date = DateTime.UtcNow;
            var lastDateChange = byPlayer.ServerData.LastCharacterSelectionDate ?? byPlayer.ServerData.FirstJoinDate ?? "1/1/1970 00:00 AM";
            var monthsPassed = date.Subtract(DateTimeOffset.Parse(lastDateChange).UtcDateTime).TotalDays / 30.0;
            if (classChangeMonths < monthsPassed)
            {
                byPlayer.Entity.WatchedAttributes.SetBool("allowcharselonce", true);
            }
        }

        sapi.Network.GetChannel("charselection").SendPacket(new CharacterSelectedState() { DidSelect = didSelect }, byPlayer);
    }

    private void onCharacterSelection(IServerPlayer fromPlayer, CharacterSelectionPacket p)
    {
        bool didSelectBefore = fromPlayer.GetModData<bool>("createCharacter", false);
        bool allowSelect = !didSelectBefore || fromPlayer.Entity.WatchedAttributes.GetBool("allowcharselonce") || fromPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;

        if (!allowSelect)
        {
            fromPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
            fromPlayer.BroadcastPlayerData(true);
            return;
        }

        if (p.DidSelect)
        {
            fromPlayer.SetModData<bool>("createCharacter", true);

            SetCharacterClass(fromPlayer.Entity, p.CharacterClass, !didSelectBefore || fromPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative);

            var bh = fromPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            bh.ApplyVoice(p.VoiceType, p.VoicePitch, false);

            foreach (var skinpart in p.SkinParts)
            {
                bh.selectSkinPart(skinpart.Key, skinpart.Value, false);
            }

            var date = DateTime.UtcNow;
            fromPlayer.ServerData.LastCharacterSelectionDate = date.ToShortDateString() + " " + date.ToShortTimeString();

            var allowOneFreeClassChange = sapi.World.Config.GetBool("allowOneFreeClassChange");
            if (!didSelectBefore && allowOneFreeClassChange)
            {
                fromPlayer.ServerData.LastCharacterSelectionDate = null;
            }
            else
            {
                fromPlayer.Entity.WatchedAttributes.RemoveAttribute("allowcharselonce");
            }
        }
        fromPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
        fromPlayer.BroadcastPlayerData(true);
    }
}