using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

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

        private ICoreAPI Api;
        private ChewingBoneCrosshair Crosshair;

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
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            if (Crosshair != null)
            {
                (api as ICoreClientAPI)?.Event.UnregisterRenderer(Crosshair, EnumRenderStage.Ortho);
                Crosshair.Dispose();
                Crosshair = null;
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

            float charge = GameMath.Clamp(0.5f + secondsUsed * 0.5f, 0.5f, 1.2f);
            float speed = 1.5f * charge;
            Vec3d velocity = new Vec3d(
                (double)viewVec.X * speed,
                (double)viewVec.Y * speed,
                (double)viewVec.Z * speed
            );

            byEntity.World.SpawnItemEntity(taken, spawnPos, velocity);

            handling = EnumHandling.PreventDefault;
        }
    }
}
