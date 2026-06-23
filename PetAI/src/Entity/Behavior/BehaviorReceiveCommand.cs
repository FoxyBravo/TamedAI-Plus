using System;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using System.Collections.Generic;

namespace PetAI
{
    public class EntityBehaviorReceiveCommand : EntityBehavior
    {
        private string _simpleCommand;

        protected TaskSelectionGui gui;
        public string SimpleCommand
        {
            get
            {
                if (IsEntityObedient(new Command(EnumCommandType.SIMPLE, _simpleCommand))) return _simpleCommand;
                else return null;

            }
            private set
            {
                _simpleCommand = value;
            }
        }
        public string ComplexCommand
        {
            get
            {
                string commandName = entity.WatchedAttributes.GetString("activeCommand");
                if (IsEntityObedient(new Command(EnumCommandType.COMPLEX, commandName))) return commandName;
                else return null;
            }
            private set
            {
                entity.WatchedAttributes.SetString("activeCommand", value);
            }
        }

        public EnumAggressionLevel AggressionLevel
        {
            get
            {
                if (entity.GetBehavior<EntityBehaviorTameable>().DomesticationLevel == DomesticationLevel.WILD) { return EnumAggressionLevel.AGGRESSIVE; }
                string commandName = entity.WatchedAttributes.GetString("aggressionLevel");
                if (Enum.TryParse<EnumAggressionLevel>(commandName, out EnumAggressionLevel level) && IsEntityObedient(new Command(EnumCommandType.AGGRESSIONLEVEL, commandName)))
                {
                    return level;
                }
                return EnumAggressionLevel.NEUTRAL;
            }

            private set
            {
                entity.WatchedAttributes.SetString("aggressionLevel", value.ToString());
            }
        }
        public Dictionary<Command, float> AvailableCommands { get; private set; } = [];
        public EntityBehaviorReceiveCommand(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            JsonObject[] commands = attributes["availableCommands"]?.AsArray() ?? [];
            foreach (var item in commands)
            {
                string commandName = item["commandName"].AsString();
                Enum.TryParse(item["commandType"].AsString(), out EnumCommandType type);
                float minObedience = item["minObedience"].AsFloat(1f);

                AvailableCommands.Add(new Command(type, commandName), minObedience);
            }
            if (!AvailableCommands.ContainsKey(new Command(EnumCommandType.COMPLEX, "roam")))
                AvailableCommands.Add(new Command(EnumCommandType.COMPLEX, "roam"), 0f);
            if (!AvailableCommands.ContainsKey(new Command(EnumCommandType.COMPLEX, "guard")))
                AvailableCommands.Add(new Command(EnumCommandType.COMPLEX, "guard"), 0.2f);
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
            if (entity.GetBehavior<EntityBehaviorTameable>()?.DomesticationLevel != DomesticationLevel.WILD
                && byEntity is EntityPlayer player
                && player.PlayerUID == entity.GetBehavior<EntityBehaviorTameable>()?.CachedOwner?.PlayerUID
                && byEntity.Controls.Sneak
                && mode == EnumInteractMode.Interact)
            {
            bool isDowned = entity.HasBehavior<EntityBehaviorMortallyWoundable>()
                && entity.GetBehavior<EntityBehaviorMortallyWoundable>().HealthState != EnumEntityHealthState.Normal;
            if (isDowned)
            {
                if (entity.Api is ICoreClientAPI capi)
                    capi.ShowChatMessage(Lang.Get("petai:gui-pet-downed"));
                return;
            }
            handled = EnumHandling.PreventSubsequent;
            if (entity.Api.Side == EnumAppSide.Client)
            {
                if (gui == null) { gui = new TaskSelectionGui(entity.Api as ICoreClientAPI, player, entity as EntityAgent); }
                else { gui.ComposeGui(); }
                gui.TryOpen();
            }
            }
        }
        public void SetCommand(Command command, EntityPlayer byPlayer)
        {
            if (command == null) return;
            if (byPlayer == null
                || entity.GetBehavior<EntityBehaviorTameable>()?.CachedOwner == null
                || entity.GetBehavior<EntityBehaviorTameable>().CachedOwner.PlayerUID == byPlayer.PlayerUID)
            {
                if (command.Type == EnumCommandType.COMPLEX)
                {
                    if (command.CommandName == "roam")
                    {
                        ComplexCommand = null;
                        entity.WatchedAttributes.RemoveAttribute("staylocation");
                        entity.WatchedAttributes.RemoveAttribute("guardlocation");
                        return;
                    }

                    ComplexCommand = command.CommandName;

                    ITreeAttribute location = new TreeAttribute();
                    location.SetDouble("x", entity.Pos.X);
                    location.SetDouble("y", entity.Pos.Y);
                    location.SetDouble("z", entity.Pos.Z);

                    entity.WatchedAttributes.SetAttribute(command.CommandName + "location", location);
                }
                if (command.Type == EnumCommandType.SIMPLE)
                {
                    SimpleCommand = command.CommandName;
                }
                if (command.Type == EnumCommandType.AGGRESSIONLEVEL)
                {
                    if (Enum.TryParse(command.CommandName, out EnumAggressionLevel level))
                    {
                        AggressionLevel = level;
                    }
                }
            }
        }

        public override string PropertyName()
        {
            return "receivecommand";
        }

        private bool IsEntityObedient(Command command)
        {
            if (command == null || command.CommandName == null) return true;
            if (entity.HasBehavior<EntityBehaviorTameable>() && AvailableCommands.ContainsKey(command))
            {
                return entity.GetBehavior<EntityBehaviorTameable>().Obedience >= AvailableCommands[command];
            }
            return true;
        }
        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
        {
            bool isDowned = entity.HasBehavior<EntityBehaviorMortallyWoundable>()
                && entity.GetBehavior<EntityBehaviorMortallyWoundable>().HealthState != EnumEntityHealthState.Normal;
            if (entity.Alive && !isDowned && entity.GetBehavior<EntityBehaviorTameable>()?.OwnerId == player.PlayerUID)
            {
                return
                [
                    new WorldInteraction()
                    {
                        ActionLangCode = "petai:interact-command",
                        HotKeyCode = "sneak",
                        MouseButton = EnumMouseButton.Right,
                    }
                ];
            }
            else
            {
                return [];
            }
        }
    }
}
