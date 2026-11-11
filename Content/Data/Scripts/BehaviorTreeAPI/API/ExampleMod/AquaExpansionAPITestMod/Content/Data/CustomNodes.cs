using BehaviorTreeAPI;
using VRage.Game.ModAPI;
using static BehaviorTreeAPI.BTreeAPI.ClientBehaviorEngine;

namespace AquaExpansionAPITestMod
{
    public class MyNode : BTreeAPI.ClientBehaviorEngine.ClientBehaviorNode
    {
        public override void Start(IMyCharacter character)
        {
            
        }
        public override BTreeAPI.ClientBehaviorEngine.ClientNodeState Tick(IMyCharacter character, float dt)
        {
            if (character == null || character.MarkedForClose)
            {
                return SetState(ClientNodeState.Failure);
            }
            Log($"This node run if health less than HealthAbove");
            return SetState(BTreeAPI.ClientBehaviorEngine.ClientNodeState.Success);
        }
        public override void End(IMyCharacter character)
        {
            
        }
    }
}
