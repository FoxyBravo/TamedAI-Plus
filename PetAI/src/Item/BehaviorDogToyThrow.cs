using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace PetAI
{
    class BehaviorDogToyThrow : CollectibleBehavior
    {
        ICoreAPI Api;

        public BehaviorDogToyThrow(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            Api = api;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            byEntity.World.Api.Logger.Notification("petai: BehaviorDogToyThrow.OnHeldInteractStart fired, firstEvent={0}, entitySel={1}, blockSel={2}, item={3}", firstEvent, entitySel?.Entity?.Code, blockSel?.Position, slot?.Itemstack?.Item?.Code);
            if (!firstEvent) return;
            if (slot?.Itemstack == null || byEntity?.World == null) return;

            slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot, 1);

            float yaw = byEntity.Pos.Yaw + GameMath.PI;
            double dirX = Math.Sin(yaw);
            double dirZ = Math.Cos(yaw);
            Vec3d spawnPos = byEntity.Pos.XYZ
                .Add(0, byEntity.LocalEyePos.Y, 0)
                .Add(dirX * 2, 0.2, dirZ * 2);

            ItemStack taken = slot.TakeOut(1);
            if (taken == null) return;

            byEntity.World.SpawnItemEntity(taken, spawnPos);

            handHandling = EnumHandHandling.PreventDefault;
            handling = EnumHandling.PreventDefault;
        }
    }
}
