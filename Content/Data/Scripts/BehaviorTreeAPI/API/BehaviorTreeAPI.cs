using Sandbox.Game.Components;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace BehaviorTreeAPI
{
    /// <summary>
    /// Client-side  BehaviorTreeAPI.
    ///
    /// Copy this file into another mod to communicate with
    /// BehaviorEngine.
    ///
    ///  provider uses:
    ///     Request ID  = 7787
    ///     Response ID = 7778
    /// </summary>
    #region BTreeAPI
    public static class BTreeAPI
    {
        private const ushort API_REQUEST_ID = 7787;
        private const ushort API_RESPONSE_ID = 7778;
        private const int API_VERSION = 1;
        private const string REQUEST_NAME = "BTEngineAPI";
        private static readonly string logname = $"[{REQUEST_NAME}] ";
        private static bool _initialized;
        private static bool _requestSent;
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private static readonly TimeSpan RequestRetryInterval = TimeSpan.FromSeconds(5);
        private static bool _available;
        private static bool _unloading;
        private static string _ownerName;
        private static Dictionary<string, Delegate> _apiMethods;
        public static event Action OnAvailable;
        private const uint BTLINK_ID = 7789;
        private const string BTLINK_REQUEST = "BTEngine:Check";
        private const string BTLINK_RESPONSE = "BTEngine:Ready";
        private static bool _heartbeatReceived;
        public static bool Available
        {
            get { return _available; }
        }
        public static bool BTlinkHeartbeat
        {
            get { return _heartbeatReceived; }
        }
        public static string OwnerName
        {
            get { return _ownerName; }
        }
        // =====================================================
        // INITIALIZE
        // =====================================================
        /// <summary>
        /// Initalize BTreeAPI on LoadData in SessionComponent
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;
            if (MyAPIGateway.Utilities == null)
            {
                MyLog.Default.WriteLine(logname+"Initialize failed: Utilities NULL.");
                return;
            }
            _initialized = true;
            _unloading = false;
            _requestSent = false;
            _lastRequestTime = DateTime.MinValue;
            _available = false;
            _apiMethods = null;
            // Automatically identify the consuming mod.
            try
            {
                _ownerName = MyAPIGateway.Utilities.GamePaths.ModScopeName;
            }
            catch
            {
                _ownerName = "UnknownMod";
            }
            if (string.IsNullOrWhiteSpace(_ownerName))
            {
                _ownerName = "UnknownMod";
            }
            _ownerName = _ownerName.Trim();
            MyAPIGateway.Utilities.RegisterMessageHandler(API_RESPONSE_ID, MessageHandler);
            MyAPIGateway.Utilities.RegisterMessageHandler(BTLINK_ID,BTLinkHandler);
            MyLog.Default.WriteLine(logname+"Client initialized. Owner=" +_ownerName);
        }
        /// <summary>
        /// BTLink Mod Message Handler
        /// </summary>
        /// <param name="obj"></param>
        private static void BTLinkHandler(object obj)
        {
            if (_unloading || obj == null)
                return;
            string response = obj as string;
            if (response == BTLINK_RESPONSE)
                _heartbeatReceived = true;
        }
        // =====================================================
        // REQUEST PROVIDER
        // =====================================================
        /// <summary>
        /// Request BtreeAPI Provider
        /// </summary>
        public static void RequestProvider()
        {
            if (!_initialized)
                return;
            if (_unloading)
                return;
            if (_available)
                return;
            if (_requestSent && DateTime.UtcNow - _lastRequestTime < RequestRetryInterval)
                return;
            if (MyAPIGateway.Utilities == null)
                return;
            if (MyAPIGateway.Session == null)
                return;
            try
            {
                MyAPIGateway.Utilities.SendModMessage(API_REQUEST_ID, REQUEST_NAME);
                _requestSent = true;
                _lastRequestTime = DateTime.UtcNow;

                MyLog.Default.WriteLine(logname +"Provider request sent.");
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(logname +"Request error:");
                MyLog.Default.WriteLine(e.ToString());
            }
        }
        /// <summary>
        /// BTreeAPI Mod Message Handler 
        /// </summary>
        /// <param name="obj"></param>
        private static void MessageHandler(object obj)
        {
            if (_unloading)
                return;
            if (obj == null)
                return;
            Dictionary<string, Delegate> methods = obj as Dictionary<string, Delegate>;
            if (methods == null)
            {
                MyLog.Default.WriteLine(logname +"Invalid provider response.");
                return;
            }
            _apiMethods = methods;
            Func<int, bool> verifyVersion = GetMethod<Func<int, bool>>("VerifyVersion");
            if (verifyVersion == null)
                return;
            bool compatible;
            try
            {
                compatible = verifyVersion(API_VERSION);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(logname +"Version check failed:");
                MyLog.Default.WriteLine(e.ToString());
                return;
            }
            if (!compatible)
            {
                MyLog.Default.WriteLine(logname +"Incompatible API version.");
                return;
            }
            _available = true;
            MyLog.Default.WriteLine(logname +"Provider available.");
            try
            {
                Action callback = OnAvailable;
                if (callback != null)
                    callback();
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(logname +"OnAvailable error:");
                MyLog.Default.WriteLine(e.ToString());
            }
        }
        /// <summary>
        /// Get BTreeAPI method
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        private static T GetMethod<T>(string name) where T : class
        {
            if (_apiMethods == null)
                return null;
            Delegate method;
            if (!_apiMethods.TryGetValue(name, out method))
            {
                MyLog.Default.WriteLine(logname +"Method not found: " +name);
                return null;
            }
            return method as T;
        }
        /// <summary>
        /// Register Custom GameLogicComponent To Build and Run CustomLogic with BehaviorTree()
        /// </summary>
        /// <param name="subtype"></param>
        /// <param name="factory"></param>
        /// <returns></returns>
        public static bool RegisterExternalComponent(string subtype, Func<MyGameLogicComponent> factory)
        {
            if (!_available)
                return false;
            if (string.IsNullOrWhiteSpace(subtype) ||
                factory == null)
                return false;
            Func<Dictionary<string, object>, bool> method = GetMethod<Func<Dictionary<string, object>, bool>>("RegisterExternalComponent");
            if (method == null)
                return false;
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["subtype"] = subtype;
            data["factory"] = factory;
            // Owner is added automatically.
            data["Owner"] = _ownerName;
            try
            {
                return method(data);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(
                    logname +
                    "Register CharacterComponent error:");
                MyLog.Default.WriteLine(e.ToString());
                return false;
            }
        }
        /// <summary>
        /// Update BtreeAPI BT Link in Update()
        /// </summary>
        public static void UpdateHeartbeat()
        {
            if (_unloading || MyAPIGateway.Session == null || MyAPIGateway.Utilities == null)
            {
                _heartbeatReceived = false;
                return;
            }
            if (!_available)
            {
                _heartbeatReceived = false;
                RequestProvider();
                return;
            }
            MyAPIGateway.Utilities.SendModMessage(BTLINK_ID, BTLINK_REQUEST);
        }
        /// <summary>
        /// Close BtreeAPI on UnloadData in SessionComponent
        /// </summary>
        public static void Close()
        {
            if (!_initialized)
                return;
            _unloading = true;
            MyLog.Default.WriteLine(logname +"Client unloading. Owner=" +_ownerName);
            try
            {
                if (MyAPIGateway.Utilities != null)
                {
                    MyAPIGateway.Utilities.UnregisterMessageHandler(API_RESPONSE_ID, MessageHandler);
                    MyAPIGateway.Utilities.UnregisterMessageHandler(BTLINK_ID,BTLinkHandler);
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(
                    logname +
                    "Error unregistering:");
                MyLog.Default.WriteLine(e.ToString());
            }
            _apiMethods = null;
            _requestSent = false;
            _lastRequestTime = DateTime.MinValue;
            _heartbeatReceived = false;
            _available = false;
            _initialized = false;
            _ownerName = null;
            OnAvailable = null;
            MyLog.Default.WriteLine(logname +"Client unloaded.");
        }
        /// <summary>
        /// BehaviorTreeEngine Client side implementation.
        /// </summary>
        public static class ClientBehaviorEngine
        {
            /// <summary>
            /// Client Node state
            /// </summary>
            public enum ClientNodeState
            { Running, Success, Failure }
            // ================= Behavior Tree =================
            /// <summary>
            /// This is the main Client Behavior Tree class that manages the execution of the tree.
            /// </summary>
            public sealed class ClientBehaviorTree
            {
                public readonly ClientBehaviorNode root;
                private readonly ClientBlackboard bb;
                private bool started;
                private ClientBehaviorNode currentNode;
                public ClientBlackboard Blackboard
                {
                    get { return bb; }
                }
                public bool IsRunning
                {
                    get { return started; }
                }
                public ClientBehaviorNode CurrentNode
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
                public ClientBehaviorTree(ClientBehaviorNode inRoot)
                {
                    bb = new ClientBlackboard();
                    root = inRoot;
                    started = false;
                    currentNode = null;
                    if (root != null)
                        root.Bind(bb);
                }
                public void SetCurrentNode(ClientBehaviorNode node)
                {
                    currentNode = node;
                }
                public void Update(IMyCharacter character, float dt)
                {
                    if (!BTlinkHeartbeat)
                    {
                        Stop(character);
                        return;
                    }
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
                    ClientNodeState state = root.Tick(character, dt);
                    if (state != ClientNodeState.Running)
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
            // ======== Blackboard ========
            /// <summary>
            /// This is a simple blackboard implementation for sharing data between nodes. (Memory)
            /// </summary>
            public sealed class ClientBlackboard
            {
                private readonly Dictionary<string, object> data = new Dictionary<string, object>();
                private ClientBehaviorNode currentNode;
                public ClientBehaviorNode CurrentNode
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
                public void SetCurrentNode(ClientBehaviorNode node)
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
            // ======== Base Behavior Nodes ========
            /// <summary>
            /// This is base Client class for all behavior tree nodes. (Node)
            /// </summary>
            public abstract class ClientBehaviorNode
            {
                protected ClientBlackboard BB;
                public ClientNodeState State { get; protected set; }
                protected ClientBehaviorNode()
                {
                    State = ClientNodeState.Running;
                }
                public void Bind(ClientBlackboard bb)
                {
                    BB = bb;
                }
                public abstract void Start(IMyCharacter character);
                public abstract ClientNodeState Tick(IMyCharacter character, float dt);
                public abstract void End(IMyCharacter character);
                protected void SetCurrentNode()
                {
                    if (BB == null)
                        return;
                    BB.SetCurrentNode(this);
                }
                protected void Log(string message)
                {
                    MyAPIGateway.Utilities.ShowMessage("[BTEngineClient]", message);
                }
                protected ClientNodeState Fail(string node, string reason)
                {
                    Log($"{node} Failure: {reason}");
                    return SetState(ClientNodeState.Failure);
                }
                protected ClientNodeState SetState(ClientNodeState state)
                {
                    State = state;
                    return state;
                }
            }
            /// <summary>
            /// Composite nodes are nodes that can have one or more child nodes.They controll the flow of execution and can implement logic such as wequence,selection and looping. 
            /// Composite nodes are used to create complex behaviors by combining multiple child nodes into a single node.
            /// </summary>
            public static class CompositeNodes
            {
                // === Composite Nodes ===
                /// <summary>
                /// This node repeats its child indefinitely Only ONE Child Node . (Composite)
                /// </summary>
                public sealed class ClientLoopNode : ClientBehaviorNode
                {
                    private readonly ClientBehaviorNode child;
                    private bool childStarted;
                    public ClientLoopNode(ClientBehaviorNode inChild)
                    {
                        child = inChild;
                        childStarted = false;
                    }
                    public override void Start(IMyCharacter character)
                    {
                        State = ClientNodeState.Running;
                        if (child == null)
                        {
                            childStarted = false;
                            return;
                        }
                        child.Bind(BB);
                        child.Start(character);
                        childStarted = true;
                    }
                    public override ClientNodeState Tick(IMyCharacter character, float dt)
                    {
                        if (child == null)
                            return SetState(ClientNodeState.Failure);
                        if (!childStarted)
                        {
                            child.Start(character);
                            childStarted = true;
                        }
                        ClientNodeState result = child.Tick(character, dt);
                        if (result == ClientNodeState.Running)
                            return SetState(ClientNodeState.Running);
                        if (childStarted)
                        {
                            child.End(character);
                            childStarted = false;
                        }
                        child.Start(character);
                        childStarted = true;
                        return SetState(ClientNodeState.Running);
                    }
                    public override void End(IMyCharacter character)
                    {
                        if (!childStarted)
                            return;
                        child.End(character);
                        childStarted = false;
                    }
                }
                /// <summary>
                /// This node ticks its children is sequentally until one fails. AND logic, stops on first Failure. (Composite)
                /// </summary>
                public sealed class ClientSequenceNode : ClientBehaviorNode
                {
                    public readonly List<ClientBehaviorNode> children;
                    private int current;
                    private bool childStarted;
                    public ClientSequenceNode(params ClientBehaviorNode[] inChildren)
                    {
                        if (inChildren == null ||
                            inChildren.Length == 0)
                        {
                            children = new List<ClientBehaviorNode>();
                        }
                        else
                        {
                            children = new List<ClientBehaviorNode>(inChildren);
                        }
                        current = 0;
                        childStarted = false;
                    }
                    public override void Start(IMyCharacter character)
                    {
                        current = 0;
                        childStarted = false;
                        State = ClientNodeState.Running;
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
                    public override ClientNodeState Tick(IMyCharacter character, float dt)
                    {
                        if (children.Count == 0)
                            return SetState(ClientNodeState.Success);
                        if (current >= children.Count)
                            return SetState(ClientNodeState.Success);
                        ClientBehaviorNode child = children[current];
                        ClientNodeState result = child.Tick(character, dt);
                        if (result == ClientNodeState.Running)
                            return SetState(ClientNodeState.Running);
                        if (childStarted)
                        {
                            child.End(character);
                            childStarted = false;
                        }
                        if (result == ClientNodeState.Failure)
                            return SetState(ClientNodeState.Failure);
                        current++;
                        if (current >= children.Count)
                            return SetState(ClientNodeState.Success);
                        children[current].Start(character);
                        childStarted = true;
                        return SetState(ClientNodeState.Running);
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
                public sealed class ClientSelectorNode : ClientBehaviorNode
                {
                    public readonly List<ClientBehaviorNode> children;
                    private int current;
                    private bool childStarted;
                    public ClientSelectorNode(params ClientBehaviorNode[] inChildren)
                    {
                        if (inChildren == null ||
                            inChildren.Length == 0)
                        {
                            children = new List<ClientBehaviorNode>();
                        }
                        else
                        {
                            children = new List<ClientBehaviorNode>(inChildren);
                        }
                        current = 0;
                        childStarted = false;
                    }
                    public override void Start(IMyCharacter character)
                    {
                        current = 0;
                        childStarted = false;
                        State = ClientNodeState.Running;
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
                    public override ClientNodeState Tick(IMyCharacter character, float dt)
                    {
                        if (children.Count == 0)
                            return SetState(ClientNodeState.Failure);
                        if (current >= children.Count)
                            return SetState(ClientNodeState.Failure);
                        ClientBehaviorNode child = children[current];
                        ClientNodeState result = child.Tick(character, dt);
                        if (result == ClientNodeState.Running)
                            return SetState(ClientNodeState.Running);
                        if (childStarted)
                        {
                            child.End(character);
                            childStarted = false;
                        }
                        if (result == ClientNodeState.Success)
                            return SetState(ClientNodeState.Success);
                        current++;
                        if (current >= children.Count)
                            return SetState(ClientNodeState.Failure);
                        children[current].Start(character);
                        childStarted = true;
                        return SetState(ClientNodeState.Running);
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
                /// Selects one child randomly each time the node starts and runs only that child.
                /// The selected child remains active until it returns Success or Failure.
                /// A new random child is selected on the next execution.
                /// </summary>
                public sealed class ClientRandomSelectorNode : ClientBehaviorNode
                {
                    public readonly List<ClientBehaviorNode> children;
                    private int current;
                    private bool childStarted;
                    public ClientRandomSelectorNode(params ClientBehaviorNode[] inChildren)
                    {
                        if (inChildren == null ||
                            inChildren.Length == 0)
                        {
                            children = new List<ClientBehaviorNode>();
                        }
                        else
                        {
                            children = new List<ClientBehaviorNode>(inChildren);
                        }
                        current = -1;
                        childStarted = false;
                    }
                    public override void Start(IMyCharacter character)
                    {
                        childStarted = false;
                        State = ClientNodeState.Running;
                        int i;
                        for (i = 0; i < children.Count; i++)
                        {
                            children[i].Bind(BB);
                        }
                        if (children.Count == 0)
                        {
                            State = ClientNodeState.Success;
                            return;
                        }
                        current = MyUtils.GetRandomInt(0, children.Count);
                        children[current].Start(character);
                        childStarted = true;
                    }
                    public override ClientNodeState Tick(IMyCharacter character, float dt)
                    {
                        if (children.Count == 0)
                            return SetState(ClientNodeState.Success);
                        if (current < 0 ||
                            current >= children.Count)
                            return SetState(ClientNodeState.Failure);
                        ClientBehaviorNode child = children[current];
                        ClientNodeState result = child.Tick(character, dt);
                        if (result == ClientNodeState.Running)
                            return SetState(ClientNodeState.Running);
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
            }
            /// <summary>
            /// Default example nodes for the BehaviorTreeEngine.
            /// </summary>
            public static class DefaultNodes
            {
                // === Example Leaf Nodes ===
                /// <summary>
                /// This node makes the character idle. (Action)
                /// </summary>
                public sealed class ClientIdle : ClientBehaviorNode
                {
                    private float timer;
                    private float duration;
                    public override void Start(IMyCharacter character)
                    {
                        timer = 0f;
                        duration = 2f + (float)MyUtils.GetRandomDouble(1, 3);
                        State = ClientNodeState.Running;
                    }
                    public override ClientNodeState Tick(IMyCharacter character, float dt)
                    {
                        SetCurrentNode();
                        if (character == null ||
                            character.MarkedForClose)
                        {
                            return SetState(ClientNodeState.Failure);
                        }
                        timer += dt;
                        if (timer >= duration)
                        {
                            //Log($"idle action compete at {duration}s");
                            return SetState(ClientNodeState.Success);
                        }
                        return SetState(ClientNodeState.Running);
                    }
                    public override void End(IMyCharacter character)
                    {
                    }
                }
                /// <summary>
                /// This node waits for a specified duration before succeeding. Can be one-shot or resetable. (Logic)
                /// </summary>
                public sealed class ClientTimer : ClientBehaviorNode
                {
                    private readonly float duration;
                    private readonly bool oneShot;
                    private float elapsed;
                    private bool finished;
                    public ClientTimer(float seconds, bool oneShot = false)
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
                            State = ClientNodeState.Success;
                            return;
                        }
                        elapsed = 0f;
                        State = ClientNodeState.Running;
                    }
                    public override ClientNodeState Tick(IMyCharacter character, float dt)
                    {
                        SetCurrentNode();
                        if (oneShot && finished)
                            return SetState(ClientNodeState.Success);
                        elapsed += dt;
                        if (elapsed < duration)
                            return SetState(ClientNodeState.Running);
                        finished = true;
                        //Log($"Waited {duration}s");
                        return SetState(ClientNodeState.Success);
                    }
                    public override void End(IMyCharacter character)
                    {
                    }
                }
                /// <summary>
                /// This node checks if the characters health is above a certain threshold (True = >=) .(Conditional)
                /// </summary>
                public sealed class ClientHealthAbove : ClientBehaviorNode
                {
                    private readonly double minimumHealth;
                    public ClientHealthAbove(double health)
                    {
                        minimumHealth = health;
                    }
                    public override void Start(IMyCharacter character)
                    {
                        State = ClientNodeState.Running;
                    }
                    public override ClientNodeState Tick(IMyCharacter character, float dt)
                    {
                        SetCurrentNode();
                        if (character == null ||
                            character.MarkedForClose)
                        {
                            return SetState(ClientNodeState.Failure);
                        }
                        if (character.Components == null)
                            return SetState(ClientNodeState.Failure);
                        MyCharacterStatComponent stats = character.Components.Get<MyCharacterStatComponent>();
                        if (stats == null)
                            return SetState(ClientNodeState.Failure);
                        float health = stats.Health.Value;
                        if (health >= minimumHealth)
                        {
                            //Log($"My Health is {health}");
                            return SetState(ClientNodeState.Success);
                        }
                        //Fail("HealthAbove",$"Health {health}");
                        //Log($"Fail Health is {health}");
                        return SetState(ClientNodeState.Failure);
                    }
                    public override void End(IMyCharacter character)
                    {
                    }
                }
            }
            /// <summary>
            /// behaviorTreeEngine Lifecycle Manager
            /// </summary>
            public sealed class ClientBehaviorManager
            {
                private ClientBehaviorTree BT;
                private IMyCharacter Character;
                private IMyEntity Entity;
                private bool Logs;
                public bool BTready { get; private set; }
                public bool BTInitializationScheduled { get; private set; }
                public delegate ClientBehaviorTree BuildTreeDelegate();
                /// <summary>
                /// Schedule delayed BehaviorTree Initalization
                /// </summary>
                /// <param name="entity"></param>
                /// <param name="character"></param>
                /// <param name="buildTreeDelegate"></param>
                /// <param name="delay"></param>
                /// <param name="logs"></param>
                public void BehaviorTreeRun(IMyEntity entity,IMyCharacter character,BuildTreeDelegate buildTreeDelegate, int delay = 2,bool logs = false)
                {
                    if (BTready || BTInitializationScheduled)
                        return;
                    if (!BTlinkHeartbeat || buildTreeDelegate == null)
                        return;
                    if (entity == null || character == null ||
                        character.Closed || character.MarkedForClose || character.IsDead)
                        return;
                    Entity = entity;
                    Character = character;
                    Logs = logs;
                    BTInitializationScheduled = true;
                    MyAPIGateway.Utilities.InvokeOnGameThread(() => Initialize(buildTreeDelegate), "BTEngine", delay);
                }
                /// <summary>
                /// Init BehaviorTree
                /// </summary>
                /// <param name="buildTreeDelegate"></param>
                private void Initialize(BuildTreeDelegate buildTreeDelegate)
                {
                    BTInitializationScheduled = false;
                    if (!BTlinkHeartbeat || buildTreeDelegate == null)
                        return;
                    if (Entity == null)
                    {
                        BTready = false;
                        if (Logs)
                            MyAPIGateway.Utilities.ShowMessage(
                            "[BTEngine]",
                            $"Failed to init for {Entity?.DisplayName}");
                        return;
                    }
                    Character = Entity as IMyCharacter;
                    if (Character == null ||
                        Character.Closed || Character.MarkedForClose || Character.IsDead)
                    {
                        if (Logs)
                            MyAPIGateway.Utilities.ShowMessage(
                            "[BTEngine]",
                            $"Failed to init for {Character?.Definition.Id.SubtypeId}");
                        return; 
                    }
                    BT = buildTreeDelegate();
                    if (BT == null)
                    {
                        BTready = false;
                        if (Logs)
                            MyAPIGateway.Utilities.ShowMessage(
                            "[BTEngine]",
                            "BuildTree() returned null for " + Entity.DisplayName);
                        return;
                    }
                    BTready = true;
                    if (Logs)
                        MyAPIGateway.Utilities.ShowMessage(
                            "[BTEngine]",
                            "BuildTree() initialized " + Character.DisplayName
                        );
                }
                /// <summary>
                /// Update BehaviorTree
                /// </summary>
                public void Update()
                {
                    if (!BTlinkHeartbeat)
                    {
                        Stop();
                        return;
                    }
                    if (!BTready || BT == null)
                        return;
                    if (Character == null || Character.Closed ||
                        Character.MarkedForClose || Character.IsDead)
                    {
                        Stop();
                        return;
                    }
                    float deltaTime = (float)MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
                    BT.Update(Character, deltaTime);
                }
                /// <summary>
                /// Stop BehaviorTree
                /// </summary>
                private void Stop()
                {
                    if (BT != null)
                    {
                        BT.Stop(Character);
                    }
                }
                /// <summary>
                /// Clear BehaviorTree
                /// </summary>
                /// <param name="node"></param>
                private void ClearTree(ClientBehaviorNode node)
                {
                    if (node == null)
                        return;
                    CompositeNodes.ClientSequenceNode sel = node as CompositeNodes.ClientSequenceNode;
                    if (sel != null)
                    {
                        for (int i = 0; i < sel.children.Count; i++)
                        {
                            ClearTree(sel.children[i]);
                        }
                        sel.children.Clear();
                        return;
                    }
                    CompositeNodes.ClientSequenceNode seq = node as CompositeNodes.ClientSequenceNode;
                    if (seq != null)
                    {
                        for (int i = 0; i < seq.children.Count; i++)
                        {
                            ClearTree(seq.children[i]);
                        }
                        seq.children.Clear();
                        return;
                    }
                    
                }
                /// <summary>
                /// Closes BehaviorTree
                /// </summary>
                public void CloseBehaviorTree()
                {
                    Stop();
                    if (BT != null)
                    {
                        ClearTree(BT.root);
                        BT = null;
                    }
                    BT = null;
                    if (Logs)
                    {
                        MyAPIGateway.Utilities.ShowMessage(
                                "[BTEngine]",
                                "BuildTree() Closed " + Character.DisplayName);
                    }
                    Character = null;
                    Entity = null;
                    BTready = false;
                    BTInitializationScheduled = false;
                }
            }
        }
    }
    #endregion
}
