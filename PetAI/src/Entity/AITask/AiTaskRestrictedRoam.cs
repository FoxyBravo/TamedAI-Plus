using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TamedAIPlus
{
    public class AiTaskRestrictedRoam : AiTaskBase
    {

        double? x;
        double? y;
        double? z;
        readonly float moveSpeed = 0.01f;
        readonly float maxDistance = 10f;
        bool stuck = false;

        readonly string commandName = "stay";
        public AiTaskRestrictedRoam(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.01f);
            }

            if (taskConfig["maxdistance"] != null)
            {
                maxDistance = taskConfig["maxdistance"].AsFloat(10f);
            }

            if (taskConfig["command"] != null)
            {
                commandName = taskConfig["command"].AsString("stay");
            }

            animMeta.Animation = "Walk";
        }

        public override bool ShouldExecute()
        {
        if (entity?.GetBehavior<EntityBehaviorRideable>()?.AnyMounted() == true)
            {
                x = null;
                y = null;
                z = null;
                string attrKey = commandName + "location";
                ITreeAttribute loc = entity?.WatchedAttributes?.GetTreeAttribute(attrKey);
                if (loc != null)
                {
                    entity?.WatchedAttributes?.RemoveAttribute(attrKey);
                }
                return false;
            }
            
            if (entity?.GetBehavior<EntityBehaviorReceiveCommand>()?.ComplexCommand != commandName)
            {
                x = null;
                y = null;
                z = null;
                return false;
            }
            if (x == null || y == null || z == null)
            {
                ITreeAttribute home = entity?.WatchedAttributes?.GetTreeAttribute(commandName + "location");
                x = home?.TryGetDouble("x");
                y = home?.TryGetDouble("y");
                z = home?.TryGetDouble("z");
                return false;
            }
            return entity.GetBehavior<EntityBehaviorReceiveCommand>().ComplexCommand == commandName &&
                entity.Pos.SquareDistanceTo((float)x, (float)y, (float)z) > maxDistance * maxDistance;
        }
        public override void StartExecute()
        {
            base.StartExecute();

            //animMeta.Animation = "Walk";
            entity.AnimManager.StartAnimation("Walk");
            //entity.Controls.Forward = true;

            if (x != null && y != null && z != null)
            {
                pathTraverser.WalkTowards(new Vec3d((double)x, (double)y, (double)z), moveSpeed, maxDistance / 2, OnGoalReached, OnStuck);
            }
            stuck = false;
        }

        public override bool ContinueExecute(float dt)
        {
            if (x == null || y == null || z == null)
            {
                return false;
            }

            //entity.Controls.Forward = true;

            if (entity.Pos.SquareDistanceTo((double)x, (double)y, (double)z) < maxDistance * maxDistance / 4)
            {
                pathTraverser.Stop();
                return false;
            }

            return !stuck && pathTraverser.Active;
        }

        private void OnStuck()
        {
            stuck = true;
        }

        private void OnGoalReached()
        {
        }
    }
}
