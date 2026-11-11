using BehaviorTreeAPI.Core;
using Sandbox.Game.Components;
using System;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace BehaviorTreeAPI.Examples
{
    // === Example Leaf Nodes ===
    /// <summary>
    /// This node makes the character idle. (Action)
    /// </summary>
    public sealed class Idle : BehaviorNode
    {
        private float timer;
        private float duration;
        public override void Start(IMyCharacter character)
        {
            timer = 0f;
            duration = 2f + (float)MyUtils.GetRandomDouble(1, 3);
            State = NodeState.Running;
        }
        public override NodeState Tick(IMyCharacter character, float dt)
        {
            SetCurrentNode();
            if (character == null ||
                character.MarkedForClose)
            {
                return SetState(NodeState.Failure);
            }
            timer += dt;
            if (timer >= duration)
            {
                //Log($"idle action compete at {duration}s"); 
                return SetState(NodeState.Success);
            }
            return SetState(NodeState.Running);
        }
        public override void End(IMyCharacter character)
        {
        }
    }
    /// <summary>
    /// This node checks if the characters health is above a certain threshold (True = >=) .(Conditional)
    /// </summary>
    public sealed class HealthAbove : BehaviorNode
    {
        private readonly double minimumHealth;
        public HealthAbove(double health)
        {
            minimumHealth = health;
        }
        public override void Start(IMyCharacter character)
        {
            State = NodeState.Running;
        }
        public override NodeState Tick(IMyCharacter character, float dt)
        {
            SetCurrentNode();
            if (character == null ||
                character.MarkedForClose)
            {
                return SetState(NodeState.Failure);
            }
            if (character.Components == null)
                return SetState(NodeState.Failure);
            MyCharacterStatComponent stats = character.Components.Get<MyCharacterStatComponent>();
            if (stats == null)
                return SetState(NodeState.Failure);
            float health = stats.Health.Value;
            if (health >= minimumHealth)
            {
                //Log($"My Health is {health}");
                return SetState(NodeState.Success);
            }
            //Fail("HealthAbove",$"Health {health}");
            //Log($"Fail Health is {health}");
            return SetState(NodeState.Failure);
        }
        public override void End(IMyCharacter character)
        {
        }
    }
    /// <summary>
    /// This node waits for a specified duration before succeeding. Can be one-shot or resetable. (Logic)
    /// </summary>
    public sealed class Timer : BehaviorNode
    {
        private readonly float duration;
        private readonly bool oneShot;
        private float elapsed;
        private bool finished;
        public Timer(float seconds, bool oneShot = false)
        {
            duration = Math.Max(0f, seconds);
            this.oneShot = oneShot;
            elapsed = 0f;
            finished = false;
        }
        public override void Start(IMyCharacter character)
        {
            if (oneShot && finished)
            {
                State = NodeState.Success;
                return;
            }
            elapsed = 0f;
            State = NodeState.Running;
        }
        public override NodeState Tick(IMyCharacter character, float dt)
        {
            SetCurrentNode();
            if (oneShot && finished)
                return SetState(NodeState.Success);
            elapsed += dt;
            if (elapsed < duration)
                return SetState(NodeState.Running);
            finished = true;
            //Log($"Waited {duration}s");
            return SetState(NodeState.Success);
        }
        public override void End(IMyCharacter character)
        {
        }
    }
}
