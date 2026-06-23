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

namespace PetAI
{
    /// <summary>
    /// Aim-throw behavior for the Chewing bone (wolftaming:dogtoy).
    ///
    /// Flow:
    ///   OnHeldInteractStart  -> enter aim state, suppress default use
    ///   OnHeldInteractStep   -> (client) raise the held bone, keep aim alive
    ///   OnHeldInteractCancel -> leave aim state (player cancelled, did not release)
    ///   OnHeldInteractStop   -> (server) decrement durability, spawn the bone
    ///                           as a normal item entity with velocity in the
    ///                           player's look direction
    ///
    /// Durability is preserved from the previous behavior: the held stack is
    /// damaged by 1 on a successful release, so the spawned entity carries
    /// the decremented stack. The wolftaming dogtoy AI task can then fetch
    /// it and return the same (decremented) stack to the player.
    ///
    /// The aim is simply the player's look direction at the moment of
    /// release (pitch + yaw). No separate crosshair or trajectory preview:
    /// the vanilla crosshair is the aim point, and the player aims by
    /// pointing the camera, then holds right-click to charge, then releases
    /// to throw.
    /// </summary>
    class BehaviorChewingBoneAimThrow : CollectibleBehavior
    {
        private const string AimingAttrKey = "petai:chewingbone-aiming";

        private const float SpawnForwardOffset = 0.5f;
        private const float PickupSqrDistance = 2.0f;
        private const float DropSqrDistance = 4.0f; // 2 blocks squared

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

            slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot, 1);

            ItemStack taken = slot.TakeOut(1);
            if (taken == null) return;
            slot.MarkDirty();

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

        /// <summary>
        /// Server tick: two phases.
        ///   Phase 1 (pickup): for every dog we tagged in NotifyDogs, check if
        ///     it has reached the bone. If so, despawn the bone and stash the
        ///     itemstack in one of the dog's free hand slots so it visually
        ///     "carries" the toy back. The wolftaming AiTaskPlayFetch then
        ///     sees the bone is gone and naturally transitions to its BringToy
        ///     state, walking the dog back to the player.
        ///   Phase 2 (drop): for every dog that is currently carrying a bone,
        ///     check if it is within 3 blocks of any player. If so, clear the
        ///     hand slot and spawn the bone on the ground — vanilla auto-pickup
        ///     then puts it in the player's inventory.
        ///
        /// We work around AiTaskPlayFetch.GetToy()'s built-in pickup (which
        /// silently fails for the dogtoy item) by doing the carry/drop
        /// ourselves in this tick handler. The dog doesn't truly "hold" the
        /// bone in its mouth shape (the wolftaming dog model has no hand
        /// element), but the item is in the slot during the return trip and
        /// ends up on the ground next to the player.
        /// </summary>
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
                    // Dog died while carrying — drop the bone where it fell.
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

        /// <summary>
        /// Put the bone into the first free hand slot on the dog. Direct
        /// Itemstack assignment (bypassing Accepts) so it works even when the
        /// slot's normal validation rejects the dogtoy item.
        /// </summary>
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

        /// <summary>
        /// Check whether the dog already has the given stack in either hand
        /// slot. Used to detect when the wolftaming AiTaskPlayFetch's own
        /// pickup won the race against our TryCarryInMouth, so we can still
        /// track the bone for the drop phase instead of giving it to the
        /// player as a fallback.
        /// </summary>
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

        /// <summary>
        /// Find nearby entities with an AiTaskPlayFetch task and point them at the
        /// freshly thrown bone. AiTaskPlayFetch lives in the WolfTaming assembly,
        /// which petai does not reference, so we look it up via reflection and
        /// set its public DogToy property. Silently no-ops if wolftaming is not
        /// loaded or the fetch task is not present on a given entity.
        /// </summary>
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

                // TaskManager.GetTask<T>() — locate the parameterless generic
                // method definition and bind it to AiTaskPlayFetch at runtime.
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
