using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace TamedAIPlus
{
    class BehaviorChewingBoneAimThrow : CollectibleBehavior
    {
        private const string AimingAttrKey = "tamedaiplus:chewingbone-aiming";

        private const float SpawnForwardOffset = 0.5f;
        private const float PickupSqrDistance = 3.0f;
        private const float DropSqrDistance = 1.0f;

        private static readonly Dictionary<long, EntityItem> dogToyPairs = new Dictionary<long, EntityItem>();
        private static readonly Dictionary<long, ItemStack> dogCarriedToy = new Dictionary<long, ItemStack>();

        private ICoreAPI Api;
        private ChewingBoneCrosshair Crosshair;
        private long tickListenerId;

        public BehaviorChewingBoneAimThrow(CollectibleObject collObj) : base(collObj) { }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            Api = api;

            if (api is ICoreClientAPI capi)
            {
                Crosshair = new ChewingBoneCrosshair(capi);
                capi.Event.RegisterRenderer(Crosshair, EnumRenderStage.Ortho);
            }
            else if (api.Side == EnumAppSide.Server)
            {
                tickListenerId = api.World.RegisterGameTickListener(OnFetchTick, 100);
            }
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            if (Crosshair != null)
            {
                (api as ICoreClientAPI)?.Event.UnregisterRenderer(Crosshair, EnumRenderStage.Ortho);
                Crosshair.Dispose();
                Crosshair = null;
            }
            if (tickListenerId != 0)
            {
                api.World.UnregisterGameTickListener(tickListenerId);
                tickListenerId = 0;
            }
            base.OnUnloaded(api);
        }

        public override void OnHeldInteractStart(
            ItemSlot slot, EntityAgent byEntity,
            BlockSelection blockSel, EntitySelection entitySel,
            bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (!firstEvent) return;
            if (byEntity?.World == null) return;
            if (slot?.Itemstack == null) return;

            byEntity.Attributes.SetInt(AimingAttrKey, 1);

            byEntity.Controls.Sprint = false;
            byEntity.ServerControls.Sprint = false;

            byEntity.StartAnimation("aim");

            handHandling = EnumHandHandling.PreventDefault;
            handling = EnumHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(
            float secondsUsed, ItemSlot slot, EntityAgent byEntity,
            BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (byEntity?.World == null) return false;
            if (byEntity.Attributes.GetInt(AimingAttrKey) != 1) return false;

            handling = EnumHandling.PreventDefault;
            return true;
        }

        public override bool OnHeldInteractCancel(
            float secondsUsed, ItemSlot slot, EntityAgent byEntity,
            BlockSelection blockSel, EntitySelection entitySel,
            EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
        {
            if (byEntity?.World == null) return true;

            byEntity.Attributes.SetInt(AimingAttrKey, 0);
            byEntity.StopAnimation("aim");

            handled = EnumHandling.PreventDefault;
            return true;
        }

        public override void OnHeldInteractStop(
            float secondsUsed, ItemSlot slot, EntityAgent byEntity,
            BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (byEntity?.World == null) return;
            if (slot?.Itemstack == null) return;
            if (byEntity.Attributes.GetInt(AimingAttrKey) != 1)
            {
                handling = EnumHandling.PreventDefault;
                return;
            }

            byEntity.Attributes.SetInt(AimingAttrKey, 0);
            byEntity.StopAnimation("aim");

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                handling = EnumHandling.PreventDefault;
                return;
            }

            ItemStack taken = slot.TakeOut(1);
            if (taken == null) return;
            slot.MarkDirty();

            var dummySlot = new DummySlot(taken);
            taken.Collectible.DamageItem(byEntity.World, byEntity, dummySlot, 1);

            Vec3f viewVec = byEntity.Pos.GetViewVector();
            Vec3d spawnPos = byEntity.Pos.XYZ
                .AddCopy(0d, byEntity.LocalEyePos.Y, 0d)
                .AddCopy(
                    (double)viewVec.X * SpawnForwardOffset,
                    (double)viewVec.Y * SpawnForwardOffset,
                    (double)viewVec.Z * SpawnForwardOffset
                );

            float charge = GameMath.Clamp(0.4f + secondsUsed * 0.3f, 0.4f, 0.7f);
            float speed = 0.55f * charge;
            Vec3d velocity = new Vec3d(
                (double)viewVec.X * speed,
                (double)viewVec.Y * speed,
                (double)viewVec.Z * speed
            );

            EntityItem entity = byEntity.World.SpawnItemEntity(taken, spawnPos, velocity) as EntityItem;

            if (entity != null)
            {
                NotifyDogs(entity);
            }

            handling = EnumHandling.PreventDefault;
        }

        private void OnFetchTick(float dt)
        {
            if (Api?.World == null) return;

            // Phase 1: pickup
            var keys = dogToyPairs.Keys.ToList();
            foreach (var dogId in keys)
            {
                if (!dogToyPairs.TryGetValue(dogId, out var dogToy) || dogToy == null)
                {
                    dogToyPairs.Remove(dogId);
                    continue;
                }
                if (!dogToy.Alive || dogToy.ShouldDespawn)
                {
                    dogToyPairs.Remove(dogId);
                    continue;
                }

                var dog = Api.World.GetEntityById(dogId);
                if (dog == null || !dog.Alive)
                {
                    dogToyPairs.Remove(dogId);
                    continue;
                }

                if (dog.Pos.SquareDistanceTo(dogToy.Pos) >= PickupSqrDistance) continue;

                ItemStack stack = dogToy.Itemstack;
                dogToy.Die(EnumDespawnReason.PickedUp);
                dogToyPairs.Remove(dogId);

                if (stack == null) continue;

                bool carried = false;
                if (dog is EntityAgent agent)
                {
                    carried = TryCarryInMouth(agent, stack) || HasItemInHand(agent, stack);
                }

                if (carried)
                {
                    dogCarriedToy[dogId] = stack;
                }
                else
                {
                    GiveToNearestPlayer(dog, stack);
                }
            }

            // Phase 2: drop near player
            var carriedKeys = dogCarriedToy.Keys.ToList();
            foreach (var dogId in carriedKeys)
            {
                if (!dogCarriedToy.TryGetValue(dogId, out var stack) || stack == null)
                {
                    dogCarriedToy.Remove(dogId);
                    continue;
                }

                var dog = Api.World.GetEntityById(dogId);
                if (dog == null || !dog.Alive)
                {
                    if (dog != null) Api.World.SpawnItemEntity(stack, dog.Pos.XYZ);
                    dogCarriedToy.Remove(dogId);
                    continue;
                }

                var player = Api.World.GetNearestEntity(dog.Pos.XYZ, 3, 3, e => e is EntityPlayer);
                if (player == null) continue;

                if (dog.Pos.SquareDistanceTo(player.Pos) > DropSqrDistance) continue;

                if (dog is EntityAgent agent) ClearMouth(agent);
                Api.World.SpawnItemEntity(stack, dog.Pos.XYZ);
                dogCarriedToy.Remove(dogId);
            }
        }

        private bool TryCarryInMouth(EntityAgent dog, ItemStack stack)
        {
            var left = dog.LeftHandItemSlot;
            if (left != null && left.Itemstack == null)
            {
                left.Itemstack = stack;
                left.MarkDirty();
                return true;
            }
            var right = dog.RightHandItemSlot;
            if (right != null && right.Itemstack == null)
            {
                right.Itemstack = stack;
                right.MarkDirty();
                return true;
            }
            return false;
        }

        private bool HasItemInHand(EntityAgent dog, ItemStack stack)
        {
            if (stack == null) return false;
            var left = dog.LeftHandItemSlot;
            if (left?.Itemstack != null && stack.Equals(Api.World, left.Itemstack)) return true;
            var right = dog.RightHandItemSlot;
            if (right?.Itemstack != null && stack.Equals(Api.World, right.Itemstack)) return true;
            return false;
        }

        private void ClearMouth(EntityAgent dog)
        {
            var left = dog.LeftHandItemSlot;
            if (left != null && left.Itemstack != null)
            {
                left.Itemstack = null;
                left.MarkDirty();
                return;
            }
            var right = dog.RightHandItemSlot;
            if (right != null && right.Itemstack != null)
            {
                right.Itemstack = null;
                right.MarkDirty();
            }
        }

        private void GiveToNearestPlayer(Entity dog, ItemStack stack)
        {
            var nearest = Api.World.GetNearestEntity(dog.Pos.XYZ, 50, 10, e => e is EntityPlayer) as EntityPlayer;
            if (nearest != null)
            {
                var serverPlayer = nearest.World.PlayerByUid(nearest.PlayerUID) as IServerPlayer;
                if (serverPlayer != null)
                {
                    serverPlayer.InventoryManager.TryGiveItemstack(stack);
                    return;
                }
            }
            Api.World.SpawnItemEntity(stack, dog.Pos.XYZ);
        }

        private void NotifyDogs(EntityItem dogToy)
        {
            if (dogToy?.World == null) return;

            var dogs = dogToy.World.GetEntitiesAround(dogToy.Pos.XYZ, 20f, 5f);
            if (dogs == null) return;

            Type playFetchType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("WolfTaming.AiTaskPlayFetch");
                if (t != null) { playFetchType = t; break; }
            }
            if (playFetchType == null) return;

            PropertyInfo dogToyProp = playFetchType.GetProperty("DogToy");
            if (dogToyProp == null) return;

            foreach (var dog in dogs)
            {
                if (dog == null) continue;
                var taskAi = dog.GetBehavior<EntityBehaviorTaskAI>();
                if (taskAi == null) continue;

                var taskManager = taskAi.TaskManager;
                if (taskManager == null) continue;

                var getTask = taskManager.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "GetTask" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
                    ?.MakeGenericMethod(playFetchType);
                if (getTask == null) continue;

                var task = getTask.Invoke(taskManager, null);
                if (task != null)
                {
                    dogToyProp.SetValue(task, dogToy);
                    dogToyPairs[dog.EntityId] = dogToy;
                }
            }
        }
    }
}
