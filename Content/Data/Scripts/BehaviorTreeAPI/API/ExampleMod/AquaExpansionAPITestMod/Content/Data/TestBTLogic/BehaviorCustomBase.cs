using BehaviorTreeAPI;
using Sandbox.Game.Components;
using Sandbox.Game.Entities.Character;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace AquaExpansionAPITestMod.TestBTLogic
{
    public abstract class BehaviorCustomBase : MyGameLogicComponent
    {
        protected IMyCharacter Character;
        protected BTreeAPI.ClientBehaviorEngine.ClientBehaviorTree BT;
        protected BTreeAPI.ClientBehaviorEngine.ClientBehaviorManager BTManager;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            Character = Entity as IMyCharacter;
            if (Character == null)
                return;
            BTManager = new BTreeAPI.ClientBehaviorEngine.ClientBehaviorManager();
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }
        public override void UpdateOnceBeforeFrame()
        {
            if (!IsValid())
                return;
            // custom logic
            base.UpdateOnceBeforeFrame();
        }
        public override void UpdateBeforeSimulation()
        {
            BTManager.BehaviorTreeRun(Entity, Character, BuildTree, 2, true);
            if (!IsValid())
                return;
            BTManager.Update();
            UpdateCustomCharacterLogic();
            // other custom logic
            base.UpdateBeforeSimulation();
        }
        public override void UpdateAfterSimulation()
        {
            if (!IsValid() || Character.IsDead)
                return;
            //other custom logic
            base.UpdateAfterSimulation();
        }
        protected virtual void UpdateCustomCharacterLogic() { }
        protected abstract BTreeAPI.ClientBehaviorEngine.ClientBehaviorTree BuildTree();
        protected bool IsValid()
        {
            return Character != null
                && Character.Physics != null
                && !Character.MarkedForClose
                && !Character.Closed;
        }
        private void Clear()
        {
            BTManager.CloseBehaviorTree();
            BT = null;
            BTManager = null;
            Character = null;
        }
        public override void Close()
        {
            Clear();
            base.Close();
        }
    }
}
