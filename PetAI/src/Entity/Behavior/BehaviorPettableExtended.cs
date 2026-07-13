using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TamedAIPlus
{
    public class EntityBehaviorPettableExtended : EntityBehaviorPettable
    {
        private long lastObedienceIncrease = 0;
        private bool wasMortallyWounded = false;

        public EntityBehaviorPettableExtended(Entity entity) : base(entity)
        {
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            // Only play wounded sound when entering the mortally wounded state
            var mortallyWoundable = entity.GetBehavior<EntityBehaviorMortallyWoundable>();
            if (mortallyWoundable == null) return;

            bool isMortallyWounded = mortallyWoundable.HealthState == EnumEntityHealthState.MortallyWounded;

            // Play sound immediately when entering mortally wounded state
            if (isMortallyWounded && !wasMortallyWounded)
            {
                wasMortallyWounded = true;
                entity.PlayEntitySound("wounded", null);
            }

            // Reset when no longer mortally wounded (e.g. healed)
            if (!isMortallyWounded)
            {
                wasMortallyWounded = false;
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
            if (lastObedienceIncrease + 1000 < byEntity.World.ElapsedMilliseconds && entity.HasBehavior<EntityBehaviorTameable>())
            {
                lastObedienceIncrease = byEntity.World.ElapsedMilliseconds;
                entity.GetBehavior<EntityBehaviorTameable>().Obedience += 0.004f;
            }
        }

        public override string PropertyName()
        {
            return "pettableextended";
        }
    }
}