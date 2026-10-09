using Sandbox.Game;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace BehaviorTreeAPI
{
    /// <summary>
    /// Node state
    /// </summary>
    public enum NodeState
    { Running,Success,Failure }
    // ======== Base Behavior Nodes / Blackboard  ========
    /// <summary>
    /// This is base class for all behavior tree nodes. (Node)
    /// </summary>
    public abstract class BehaviorNode
    {
        protected Blackboard BB;
        public NodeState State { get; protected set; }
        protected BehaviorNode()
        {
            State = NodeState.Running;
        }
        public void Bind(Blackboard bb)
        {
            BB = bb;
        }
        public abstract void Start(IMyCharacter character);
        public abstract NodeState Tick(IMyCharacter character,float dt);
        public abstract void End(IMyCharacter character);
        protected void SetCurrentNode()
        {
            if (BB == null)
                return;
            BB.SetCurrentNode(this);
        }
        protected void Log(string message)
        {
            MyVisualScriptLogicProvider.SendChatMessage($"{message}", "BT");
        }
        protected NodeState Fail(string node,string reason)
        {
            Log($"{node} Failure: {reason}");
            return SetState(NodeState.Failure);
        }
        protected NodeState SetState(NodeState state)
        {
            State = state;
            return state;
        }
    }
    /// <summary>
    /// This is a simple blackboard implementation for sharing data between nodes. (Memory)
    /// </summary>
    public class Blackboard
    {
        private readonly Dictionary<string, object> data = new Dictionary<string, object>();
        private BehaviorNode currentNode;
        public BehaviorNode CurrentNode
        {
            get { return currentNode; }
        }
        public string CurrentNodeName
        {
            get
            {
                if (currentNode == null)
                    return "None";

                return currentNode.GetType().Name;
            }
        }
        public void SetCurrentNode(BehaviorNode node)
        {
            currentNode = node;
        }
        public void Set<T>(string key, T value)
        {
            data[key] = value;
        }
        public T Get<T>(string key)
        {
            object value;
            if (data.TryGetValue(key, out value))
            {
                if (value is T)
                    return (T)value;
            }
            return default(T);
        }
        public bool Has(string key)
        {
            return data.ContainsKey(key);
        }
        public void Clear(string key)
        {
            data.Remove(key);
        }
        public void ClearAll()
        {
            data.Clear();
            currentNode = null;
        }
    }
    // === Composite Nodes ===
    /// <summary>
    /// Selects one child randomly each time the node starts and runs only that child.
    /// The selected child remains active until it returns Success or Failure.
    /// A new random child is selected on the next execution.
    /// </summary>
    public class RandomSelectorNode : BehaviorNode
    {
        public readonly List<BehaviorNode> children;
        private int current;
        private bool childStarted;
        public RandomSelectorNode(params BehaviorNode[] inChildren)
        {
            if (inChildren == null ||
                inChildren.Length == 0)
            {
                children = new List<BehaviorNode>();
            }
            else
            {
                children = new List<BehaviorNode>(inChildren);
            }
            current = -1;
            childStarted = false;
        }
        public override void Start(IMyCharacter character)
        {
            childStarted = false;
            State = NodeState.Running;
            int i;
            for (i = 0; i < children.Count; i++)
            {
                children[i].Bind(BB);
            }
            if (children.Count == 0)
            {
                State = NodeState.Success;
                return;
            }
            current = MyUtils.GetRandomInt(0,children.Count);
            children[current].Start(character);
            childStarted = true;
        }
        public override NodeState Tick(IMyCharacter character,float dt)
        {
            if (children.Count == 0)
                return SetState(NodeState.Success);
            if (current < 0 ||
                current >= children.Count)
                return SetState(NodeState.Failure);
            BehaviorNode child = children[current];
            NodeState result = child.Tick(character,dt);
            if (result == NodeState.Running)
                return SetState(NodeState.Running);
            if (childStarted)
            {
                child.End(character);
                childStarted = false;
            }
            return SetState(result);
        }
        public override void End(IMyCharacter character)
        {
            if (!childStarted)
                return;
            if (current >= 0 &&
                current < children.Count)
            {
                children[current].End(character);
            }
            childStarted = false;
        }
    }
    /// <summary>
    /// This node ticks its children is sequentally until one fails. AND logic, stops on first Failure. (Composite)
    /// </summary>
    public class SequenceNode : BehaviorNode
    {
        public readonly List<BehaviorNode> children;
        private int current;
        private bool childStarted;
        public SequenceNode(params BehaviorNode[] inChildren)
        {
            if (inChildren == null ||
                inChildren.Length == 0)
            {
                children = new List<BehaviorNode>();
            }
            else
            {
                children = new List<BehaviorNode>(inChildren);
            }
            current = 0;
            childStarted = false;
        }
        public override void Start(IMyCharacter character)
        {
            current = 0;
            childStarted = false;
            State = NodeState.Running;
            int i;
            for (i = 0; i < children.Count; i++)
            {
                children[i].Bind(BB);
            }
            if (children.Count > 0)
            {
                children[0].Start(character);
                childStarted = true;
            }
        }
        public override NodeState Tick(IMyCharacter character,float dt)
        {
            if (children.Count == 0)
                return SetState(NodeState.Success);
            if (current >= children.Count)
                return SetState(NodeState.Success);
            BehaviorNode child = children[current];
            NodeState result = child.Tick(character,dt);
            if (result == NodeState.Running)
                return SetState(NodeState.Running);
            if (childStarted)
            {
                child.End(character);
                childStarted = false;
            }
            if (result == NodeState.Failure)
                return SetState(NodeState.Failure);
            current++;
            if (current >= children.Count)
                return SetState(NodeState.Success);
            children[current].Start(character);
            childStarted = true;
            return SetState(NodeState.Running);
        }
        public override void End(IMyCharacter character)
        {
            if (!childStarted)
                return;
            if (current >= 0 &&
                current < children.Count)
            {
                children[current].End(character);
            }
            childStarted = false;
        }
    }
    /// <summary>
    /// This node ticks its children sequentually until one succeeds. OR logic, stops on first Success. Support many Children Nodes (Composite)
    /// </summary>
    public class SelectorNode : BehaviorNode
    {
        public readonly List<BehaviorNode> children;
        private int current;
        private bool childStarted;
        public SelectorNode(params BehaviorNode[] inChildren)
        {
            if (inChildren == null ||
                inChildren.Length == 0)
            {
                children = new List<BehaviorNode>();
            }
            else
            {
                children = new List<BehaviorNode>(inChildren);
            }
            current = 0;
            childStarted = false;
        }
        public override void Start(IMyCharacter character)
        {
            current = 0;
            childStarted = false;
            State = NodeState.Running;
            int i;
            for (i = 0; i < children.Count; i++)
            {
                children[i].Bind(BB);
            }
            if (children.Count > 0)
            {
                children[0].Start(character);
                childStarted = true;
            }
        }
        public override NodeState Tick(IMyCharacter character,float dt)
        {
            if (children.Count == 0)
                return SetState(NodeState.Failure);
            if (current >= children.Count)
                return SetState(NodeState.Failure);
            BehaviorNode child = children[current];
            NodeState result = child.Tick(character,dt);
            if (result == NodeState.Running)
                return SetState(NodeState.Running);
            if (childStarted)
            {
                child.End(character);
                childStarted = false;
            }
            if (result == NodeState.Success)
                return SetState(NodeState.Success);
            current++;
            if (current >= children.Count)
                return SetState(NodeState.Failure);
            children[current].Start(character);
            childStarted = true;
            return SetState(NodeState.Running);
        }
        public override void End(IMyCharacter character)
        {
            if (!childStarted)
                return;
            if (current >= 0 &&
                current < children.Count)
            {
                children[current].End(character);
            }
            childStarted = false;
        }
    }
    /// <summary>
    /// This node repeats its child indefinitely Only ONE Child Node . (Composite)
    /// </summary>
    public class LoopNode : BehaviorNode
    {
        private readonly BehaviorNode child;
        private bool childStarted;
        public LoopNode(
            BehaviorNode inChild)
        {
            child = inChild;
            childStarted = false;
        }
        public override void Start(IMyCharacter character)
        {
            State = NodeState.Running;
            if (child == null)
            {
                childStarted = false;
                return;
            }
            child.Bind(BB);
            child.Start(character);
            childStarted = true;
        }
        public override NodeState Tick(IMyCharacter character,float dt)
        {
            if (child == null)
                return SetState(NodeState.Failure);
            if (!childStarted)
            {
                child.Start(character);
                childStarted = true;
            }
            NodeState result = child.Tick(character,dt);
            if (result == NodeState.Running)
                return SetState(NodeState.Running);
            if (childStarted)
            {
                child.End(character);
                childStarted = false;
            }
            child.Start(character);
            childStarted = true;
            return SetState(NodeState.Running);
        }
        public override void End(IMyCharacter character)
        {
            if (!childStarted)
                return;
            child.End(character);
            childStarted = false;
        }
    }
    // ================= Behavior Tree =================
    /// <summary>
    /// This is the main Behavior Tree class that manages the execution of the tree.
    /// </summary>
    public class BehaviorTree
    {
        public readonly BehaviorNode root;
        private readonly Blackboard bb;
        private bool started;
        private BehaviorNode currentNode;
        public Blackboard Blackboard
        {
            get { return bb; }
        }
        public bool IsRunning
        {
            get { return started; }
        }
        public BehaviorNode CurrentNode
        {
            get
            {
                return currentNode;
            }
        }
        public string CurrentNodeName
        {
            get
            {
                if (bb == null || bb.CurrentNode == null)
                    return "None";
                return bb.CurrentNode.GetType().Name;
            }
        }
        public BehaviorTree(BehaviorNode inRoot)
        {
            bb = new Blackboard();
            root = inRoot;
            started = false;
            currentNode = null;
            if (root != null)
                root.Bind(bb);
        }
        public void SetCurrentNode(BehaviorNode node)
        {
            currentNode = node;
        }
        public void Update(IMyCharacter character, float dt)
        {
            if (root == null)
                return;
            if (character == null ||
                character.MarkedForClose)
            {
                Stop(character);
                return;
            }
            if (dt < 0f)
                dt = 0f;
            if (!started)
            {
                root.Start(character);
                started = true;
            }
            NodeState state = root.Tick(character, dt);
            if (state != NodeState.Running)
            {
                root.End(character);
                started = false;
                currentNode = null;
            }
        }
        public void Stop(IMyCharacter character)
        {
            if (!started)
                return;
            if (root != null)
                root.End(character);
            started = false;
            currentNode = null;
        }
        public void Reset()
        {
            started = false;
            currentNode = null;
            bb.ClearAll();
        }
    }
}

