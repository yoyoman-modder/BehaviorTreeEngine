using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace BehaviorTreeAPI.Core
{
    public static class CustomNodeRegistry
    {
        private static readonly Dictionary<string, Registration> Registrations = new Dictionary<string, Registration>(StringComparer.Ordinal);
        private sealed class Registration
        {
            public Func<object> CreateState;
            public Action<IMyCharacter, object> Start;
            public Func<IMyCharacter, object, float, int> Tick;
            public Action<IMyCharacter, object> End;
        }
        private sealed class CallbackNode : BehaviorNode
        {
            private readonly Registration registration;
            private object state;
            private bool started;
            public CallbackNode(Registration registration)
            {
                this.registration = registration;
            }
            public override void Start(IMyCharacter character)
            {
                state = registration.CreateState();
                registration.Start(character, state);
                started = true;
                State = NodeState.Running;
            }
            public override NodeState Tick(IMyCharacter character, float dt)
            {
                SetCurrentNode();
                if (!started)
                    return SetState(NodeState.Failure);
                int result = registration.Tick(character, state, dt);
                if (result == 0)
                    return SetState(NodeState.Running);
                if (result == 1)
                    return SetState(NodeState.Success);
                return SetState(NodeState.Failure);
            }
            public override void End(IMyCharacter character)
            {
                if (!started)
                    return;
                started = false;
                registration.End(character, state);
                state = null;
            }
        }
        public static bool Register(string id,Func<object> createState,Action<IMyCharacter, object> start,Func<IMyCharacter, object, float, int> tick,Action<IMyCharacter, object> end)
        {
            if (string.IsNullOrEmpty(id) ||
                createState == null || start == null || tick == null || end == null ||
                Registrations.ContainsKey(id))
                return false;
            Registrations.Add(id, new Registration
            {
                CreateState = createState,
                Start = start,
                Tick = tick,
                End = end
            });
            return true;
        }
        public static bool Unregister(string id)
        {
            return !string.IsNullOrEmpty(id) && Registrations.Remove(id);
        }
        public static bool TryCreate(string id, out BehaviorNode node)
        {
            Registration registration;
            if (!string.IsNullOrEmpty(id) &&
                Registrations.TryGetValue(id, out registration))
            {
                node = new CallbackNode(registration);
                return true;
            }
            node = null;
            return false;
        }
        public static void Clear()
        {
            Registrations.Clear();
        }
    }
}
