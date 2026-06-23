using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace PetAI
{
    public class AiTaskPetFlee : AiTaskBase
    {
        readonly float moveSpeed = 0.05f;
        readonly float fleeRange = 25f;
        readonly float fleeSeconds = 4f;
        Entity damager;

        public AiTaskPetFlee(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.05f);
            }
            if (taskConfig["fleerange"] != null)
            {
                fleeRange = taskConfig["fleerange"].AsFloat(25f);
            }
            if (taskConfig["fleeseconds"] != null)
            {
                fleeSeconds = taskConfig["fleeseconds"].AsFloat(4f);
            }
        }

        public override bool ShouldExecute()
        {
            var receiveBehavior = entity?.GetBehavior<EntityBehaviorReceiveCommand>();
            if (receiveBehavior == null) return false;
            if (receiveBehavior.AggressionLevel != EnumAggressionLevel.PASSIVE) return false;

            int lastDamageMs = entity.WatchedAttributes.GetInt("petai:lastDamageMs");
            if (lastDamageMs == 0) return false;
            long now = entity.World.ElapsedMilliseconds % int.MaxValue;
            long delta = now >= lastDamageMs ? now - lastDamageMs : (int.MaxValue - lastDamageMs) + now;
            if (delta > fleeSeconds * 1000) return false;

            int damagerId = entity.WatchedAttributes.GetInt("petai:lastDamagerId");
            if (damagerId == 0) return false;
            damager = entity.World.GetEntityById(damagerId);
            if (damager == null || !damager.Alive) return false;

            float dist = (float)damager.Pos.DistanceTo(entity.Pos);
            return dist < fleeRange;
        }

        public override void StartExecute()
        {
            base.StartExecute();
            if (damager == null) return;

            Vec3d away = (entity.Pos.XYZ - damager.Pos.XYZ);
            if (away.Length() < 0.001) away = new Vec3d(1, 0, 0);
            else away = away.Normalize();

            Vec3d fleeTarget = entity.Pos.XYZ + away * fleeRange;
            pathTraverser.WalkTowards(fleeTarget, moveSpeed, fleeRange / 2, OnGoalReached, OnStuck);
        }

        public override bool ContinueExecute(float dt)
        {
            var receiveBehavior = entity?.GetBehavior<EntityBehaviorReceiveCommand>();
            if (receiveBehavior == null || receiveBehavior.AggressionLevel != EnumAggressionLevel.PASSIVE) return false;
            if (damager == null || !damager.Alive) return false;
            return pathTraverser.Active;
        }

        private void OnStuck()
        {
        }

        private void OnGoalReached()
        {
        }
    }
}
