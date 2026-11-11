using BehaviorTreeAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.ObjectBuilders;

namespace AquaExpansionAPITestMod.TestBTLogic
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Character), false, "Space_Wolf")]
    public class WolfTestBehaviorTree : BehaviorCustomBase
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            //custom logic if needed
        }
        protected override BTreeAPI.ClientBehaviorEngine.ClientBehaviorTree BuildTree()
        {
            return new BTreeAPI.ClientBehaviorEngine.ClientBehaviorTree(//root node
                new BTreeAPI.ClientBehaviorEngine.CompositeNodes.ClientLoopNode(// repeat forever child
                    new BTreeAPI.ClientBehaviorEngine.CompositeNodes.ClientSequenceNode(// run children in order until one fails
                        new BTreeAPI.ClientBehaviorEngine.DefaultNodes.ClientHealthAbove(50)
                    )
                )
             );
        }
        protected override void UpdateCustomCharacterLogic()
        {
            base.UpdateCustomCharacterLogic();
            // custom logic
        }
    }
}
