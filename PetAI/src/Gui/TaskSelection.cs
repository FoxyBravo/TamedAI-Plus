using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;


namespace TamedAIPlus
{
    public class TaskSelectionGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        readonly private EntityAgent targetEntity;

        readonly private EntityPlayer player;
        private int currentX = 0;
        private int currentY = 0;
        List<Command> availableCommands;

        public TaskSelectionGui(ICoreClientAPI capi, EntityPlayer player, EntityAgent targetEntity = null) : base(capi)
        {
            this.targetEntity = targetEntity;
            this.player = player;
            ComposeGui();
        }

        public void ComposeGui()
        {
            currentY = 20;
            currentX = 0;
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            if (targetEntity == null
                || !targetEntity.HasBehavior<EntityBehaviorTameable>()
                || targetEntity.GetBehavior<EntityBehaviorReceiveCommand>().AvailableCommands.Keys.Count == 0)
            {
                ComposeStaticDialogue(dialogBounds, bgBounds);
            }
            else
            {
                ComposeDynamicDialogue(dialogBounds, bgBounds);
            }
        }

        private void ComposeDynamicDialogue(ElementBounds dialogBounds, ElementBounds bgBounds)
        {
            availableCommands = [.. targetEntity.GetBehavior<EntityBehaviorReceiveCommand>().AvailableCommands.Keys];
            SingleComposer = capi.Gui.CreateCompo("CommandDialog-", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("tamedaiplus:gui-command-title"), () => TryClose())
                .BeginChildElements(bgBounds);

            AddGuiRow(EnumCommandType.SIMPLE, "tamedaiplus:gui-command-simple");
            AddGuiRow(EnumCommandType.COMPLEX, "tamedaiplus:gui-command-complex");
            AddGuiRow(EnumCommandType.AGGRESSIONLEVEL, "tamedaiplus:gui-command-aggressionlevel");

            SingleComposer.AddIconButton("necklace", OnToggleProfile, ElementBounds.FixedOffseted(EnumDialogArea.RightTop, 0, 20, 30, 30))
                .EndChildElements()
            .Compose();
        }

        private void AddGuiRow(EnumCommandType type, string headline)
        {
            if (availableCommands.Exists(command => command.Type == type))
            {
                SingleComposer.AddStaticText(Lang.Get(headline), CairoFont.WhiteSmallishText().WithFontSize(16), ElementBounds.Fixed(0, currentY, 300, 18));
                currentY += 23;
                currentX = 0;

                foreach (var command in availableCommands.FindAll(command => command.Type == type))
                {
                    bool isActive = IsCommandActive(command);
                    if (isActive)
                    {
                        SingleComposer.AddInset(ElementBounds.Fixed(currentX, currentY, 112, 37), 1, 3);
                    }
                    var buttonBounds = ElementBounds.Fixed(currentX + (isActive ? 1 : 0), currentY + (isActive ? 1 : 0), 110, 35);
                    string tooltip = BuildCommandTooltip(command);
                    SingleComposer.AddButton(
                        Lang.Get(string.Format("tamedaiplus:gui-command-{0}", command.CommandName.ToLower())),
                        () => OnCommandClick(command),
                        buttonBounds,
                        CairoFont.ButtonText().WithFontSize(16),
                        EnumButtonStyle.Normal,
                        EnumTextOrientation.Center);
                    if (tooltip != null)
                    {
                        SingleComposer.AddHoverText(tooltip, CairoFont.WhiteSmallText(), 300, buttonBounds.FlatCopy());
                    }
                    currentX += 120;
                }
                currentY += 40;
            }
        }

        private string BuildCommandTooltip(Command command)
        {
            if (command.Type == EnumCommandType.SIMPLE)
            {
                return null;
            }
            if (command.Type == EnumCommandType.AGGRESSIONLEVEL)
            {
                return Lang.Get($"tamedaiplus:gui-tooltip-{command.CommandName.ToLower()}");
            }
            if (command.Type == EnumCommandType.COMPLEX)
            {
                return Lang.Get($"tamedaiplus:gui-tooltip-{command.CommandName.ToLower()}-base");
            }
            return null;
        }

        private bool IsCommandActive(Command command)
        {
            var behavior = targetEntity?.GetBehavior<EntityBehaviorReceiveCommand>();
            if (behavior == null) return false;

            switch (command.Type)
            {
                case EnumCommandType.COMPLEX:
                    string active = behavior.ComplexCommand;
                    if (command.CommandName == "roam" && string.IsNullOrEmpty(active)) return true;
                    return active == command.CommandName;
                case EnumCommandType.SIMPLE:
                    return behavior.SimpleCommand == command.CommandName;
                case EnumCommandType.AGGRESSIONLEVEL:
                    return behavior.AggressionLevel.ToString().ToLower() == command.CommandName.ToLower();
                default:
                    return false;
            }
        }

        private void ComposeStaticDialogue(ElementBounds dialogBounds, ElementBounds bgBounds)
        {
            SingleComposer = capi.Gui.CreateCompo("CommandDialog-", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("tamedaiplus:gui-command-title"), () => TryClose())
                .BeginChildElements(bgBounds)
                    .AddStaticText(Lang.Get("tamedaiplus:gui-command-simple"), CairoFont.WhiteSmallishText().WithFontSize(16), ElementBounds.Fixed(0, 20, 300, 18))
                    .AddButton(Lang.Get("tamedaiplus:gui-command-sit"), () => OnCommandClick(new Command(EnumCommandType.SIMPLE, "sit")), ElementBounds.Fixed(0, 43, 110, 35), CairoFont.ButtonText().WithFontSize(16), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                    .AddButton(Lang.Get("tamedaiplus:gui-command-lay"), () => OnCommandClick(new Command(EnumCommandType.SIMPLE, "lay")), ElementBounds.Fixed(120, 43, 110, 35), CairoFont.ButtonText().WithFontSize(16), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                    .AddButton(Lang.Get("tamedaiplus:gui-command-speak"), () => OnCommandClick(new Command(EnumCommandType.SIMPLE, "speak")), ElementBounds.Fixed(240, 43, 110, 35), CairoFont.ButtonText().WithFontSize(16), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                    .AddStaticText(Lang.Get("tamedaiplus:gui-command-complex"), CairoFont.WhiteSmallishText().WithFontSize(16), ElementBounds.Fixed(0, 83, 300, 18))
                    .AddButton(Lang.Get("tamedaiplus:gui-command-followmaster"), () => OnCommandClick(new Command(EnumCommandType.COMPLEX, "followmaster")), ElementBounds.Fixed(0, 106, 110, 35), CairoFont.ButtonText().WithFontSize(16), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                    .AddButton(Lang.Get("tamedaiplus:gui-command-stay"), () => OnCommandClick(new Command(EnumCommandType.COMPLEX, "stay")), ElementBounds.Fixed(120, 106, 110, 35), CairoFont.ButtonText().WithFontSize(16), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                    .AddButton(Lang.Get("tamedaiplus:gui-command-roam"), () => OnCommandClick(new Command(EnumCommandType.COMPLEX, "roam")), ElementBounds.Fixed(240, 106, 110, 35), CairoFont.ButtonText().WithFontSize(16), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                    .AddButton(Lang.Get("tamedaiplus:gui-command-guard"), () => OnCommandClick(new Command(EnumCommandType.COMPLEX, "guard")), ElementBounds.Fixed(360, 106, 110, 35), CairoFont.ButtonText().WithFontSize(16), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                    .AddStaticText(Lang.Get("tamedaiplus:gui-command-aggressionlevel"), CairoFont.WhiteSmallishText().WithFontSize(16), ElementBounds.Fixed(0, 146, 300, 18))
                    .AddButton(Lang.Get("tamedaiplus:gui-command-neutral"), () => OnCommandClick(new Command(EnumCommandType.AGGRESSIONLEVEL, EnumAggressionLevel.NEUTRAL.ToString())), ElementBounds.Fixed(0, 169, 110, 35), CairoFont.ButtonText().WithFontSize(16), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                    .AddButton(Lang.Get("tamedaiplus:gui-command-protective"), () => OnCommandClick(new Command(EnumCommandType.AGGRESSIONLEVEL, EnumAggressionLevel.PROTECTIVE.ToString())), ElementBounds.Fixed(120, 169, 110, 35), CairoFont.ButtonText().WithFontSize(16), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                    .AddButton(Lang.Get("tamedaiplus:gui-command-aggressive"), () => OnCommandClick(new Command(EnumCommandType.AGGRESSIONLEVEL, EnumAggressionLevel.AGGRESSIVE.ToString())), ElementBounds.Fixed(240, 169, 110, 35), CairoFont.ButtonText().WithFontSize(16), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                    .AddButton(Lang.Get("tamedaiplus:gui-command-passive"), () => OnCommandClick(new Command(EnumCommandType.AGGRESSIONLEVEL, EnumAggressionLevel.PASSIVE.ToString())), ElementBounds.Fixed(360, 169, 110, 35), CairoFont.ButtonText().WithFontSize(16), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                    .AddStaticText(Lang.Get("tamedaiplus:gui-command-attackorder"), CairoFont.WhiteSmallishText().WithFontSize(16), ElementBounds.Fixed(0, 209, 300, 18))
                    .AddButton(Lang.Get("tamedaiplus:gui-command-settarget"), () => OnCommandClick(new Command(EnumCommandType.ATTACKORDER, "settarget")), ElementBounds.Fixed(0, 232, 110, 35), CairoFont.ButtonText().WithFontSize(16), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                    .AddButton(Lang.Get("tamedaiplus:gui-command-removetarget"), () => OnCommandClick(new Command(EnumCommandType.ATTACKORDER, "removetarget")), ElementBounds.Fixed(120, 232, 110, 35), CairoFont.ButtonText().WithFontSize(16), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                .EndChildElements()
                .Compose();
        }
        private bool OnCommandClick(Command command)
        {
            var message = new PetCommandMessage
            {
                commandName = command.CommandName,
                commandType = command.Type.ToString(),
                playerUID = player.PlayerUID
            };
            if (targetEntity != null)
            {
                message.targetEntityUID = targetEntity.EntityId;
            }

            if (targetEntity != null
                && targetEntity.HasBehavior<EntityBehaviorTameable>()
                && command.CommandName != "dropgear"
                && targetEntity.GetBehavior<EntityBehaviorReceiveCommand>().AvailableCommands[command] > targetEntity.GetBehavior<EntityBehaviorTameable>().Obedience)
            {
                capi.ShowChatMessage(Lang.Get("tamedaiplus:gui-animal-disobey", Math.Round(targetEntity.GetBehavior<EntityBehaviorReceiveCommand>().AvailableCommands[command] * 100, 2)));
                return true;
            }

            TryClose();

            capi.Network.GetChannel("tamedaiplus-network").SendPacket<PetCommandMessage>(message);
            return true;
        }

        private void OnToggleProfile(bool value)
        {
            var gui = new PetProfileGUI(capi, targetEntity.EntityId);
            gui.TryClose();
            gui.TryOpen();
            TryClose();
        }
    }
}