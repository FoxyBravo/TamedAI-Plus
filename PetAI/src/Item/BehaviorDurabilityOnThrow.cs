using Vintagestory.API.Common;

namespace PetAI
{
    class BehaviorDurabilityOnThrow : CollectibleBehavior
    {
        public BehaviorDurabilityOnThrow(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (slot?.Itemstack != null && byEntity?.World != null)
            {
                slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot, 1);
            }
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
        }
    }
}

