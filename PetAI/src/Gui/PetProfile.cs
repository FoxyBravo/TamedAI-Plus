using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace TamedAIPlus
{
    public class PetProfileGUI : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        readonly private long targetEntityId;
        private int currentY = 20;

        string petName;

        bool multiplyAllowed = true;

        bool abandon = false;

        public PetProfileGUI(ICoreClientAPI capi, long targetEntityId) : base(capi)
        {
            this.targetEntityId = targetEntityId;
            var targetEntity = capi.World.GetEntityById(targetEntityId);

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            SingleComposer = capi.Gui.CreateCompo("PetProfileDialog-", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("tamedaiplus:gui-profile-title"), () => TryClose())
                .BeginChildElements(bgBounds);
            SingleComposer.AddStaticText(Lang.Get("tamedaiplus:gui-profile-name"), CairoFont.WhiteSmallishText().WithFontSize(16), ElementBounds.Fixed(0, currentY, 200, 18));
            currentY += 23;
            SingleComposer.AddTextInput(ElementBounds.Fixed(0, currentY, 200, 35), (name) =>
            {
                if (!string.IsNullOrEmpty(name) && name.Length > 50)
                {
                    name = name[..50]; // did not think this would be necessary but here we are
                    SingleComposer.GetTextInput("petName").SetValue(name);
                }
                petName = name;
            }, null, "petName");
            SingleComposer.GetTextInput("petName").SetValue(targetEntity?.GetBehavior<EntityBehaviorNameTag>()?.DisplayName);
            currentY += 55;
            string animalType = Lang.Get("Type: unknown");
            if (targetEntity != null)
            {
                animalType = Lang.Get("Type: " + targetEntity.Code.Path);
            }
            SingleComposer.AddStaticText(animalType, CairoFont.WhiteSmallishText().WithFontSize(16), ElementBounds.Fixed(0, currentY, 300, 18));
            currentY += 23;
            int generation = targetEntity != null ? targetEntity.WatchedAttributes.GetInt("generation", 0) : 0;
            SingleComposer.AddStaticText(Lang.Get("tamedaiplus:gui-profile-generation", generation), CairoFont.WhiteSmallishText().WithFontSize(16), ElementBounds.Fixed(0, currentY, 240, 18));
            currentY += 23;
            var tameable = targetEntity?.GetBehavior<EntityBehaviorTameable>();
            string sizeKey = tameable != null ? "tamedaiplus:gui-animal-nestsize-" + tameable.Size.ToString().ToLower() : null;
            SingleComposer.AddStaticText(Lang.Get("tamedaiplus:gui-profile-size", Lang.Get(sizeKey ?? "tamedaiplus:gui-animal-nestsize-small")), CairoFont.WhiteSmallishText().WithFontSize(16), ElementBounds.Fixed(0, currentY, 240, 18));
            currentY += 40;
            if (targetEntity?.HasBehavior<EntityBehaviorMultiply>() == true)
            {
                var multiply = targetEntity.GetBehavior<EntityBehaviorTameable>().MultiplyAllowed;
                multiplyAllowed = multiply;
                SingleComposer.AddStaticText(Lang.Get("tamedaiplus:gui-profile-multiply"), CairoFont.WhiteSmallishText().WithFontSize(16), ElementBounds.Fixed(0, currentY, 200, 18));
                SingleComposer.AddSwitch(value => multiplyAllowed = value, ElementBounds.Fixed(150, currentY, 200, 18), "multiplyAllowed");
                SingleComposer.GetSwitch("multiplyAllowed").SetValue(multiply);
                currentY += 40;
            }
            SingleComposer.AddStaticText(Lang.Get("tamedaiplus:gui-profile-abandon"), CairoFont.WhiteSmallishText().WithFontSize(16), ElementBounds.Fixed(0, currentY, 200, 18));
            SingleComposer.AddSwitch(value => abandon = value, ElementBounds.Fixed(150, currentY, 200, 18), "abandon");
            currentY += 40;
            SingleComposer.AddButton(Lang.Get("tamedaiplus:gui-profile-ok"), () => OnClick(), ElementBounds.Fixed(0, currentY, 110, 35), CairoFont.ButtonText().WithFontSize(16), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                .AddButton(Lang.Get("tamedaiplus:gui-profile-cancel"), () => TryClose(), ElementBounds.Fixed(120, currentY, 110, 35), CairoFont.ButtonText().WithFontSize(16), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                .EndChildElements()
                .Compose();
        }
        private bool OnClick()
        {
            var message = new PetProfileMessage
            {
                petName = petName,
                multiplyAllowed = multiplyAllowed,
                abandon = abandon,
                targetEntityUID = targetEntityId
            };

            capi.Network.GetChannel("tamedaiplus-network").SendPacket(message);

            TryClose();
            return true;
        }
    }
}
