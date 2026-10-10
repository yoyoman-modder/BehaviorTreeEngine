using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace BehaviorTreeAPI.API
{
    public sealed class BTTreeNodeSpec
    {
        private readonly string kind;
        private readonly string nodeId;
        private readonly BTTreeNodeSpec[] children;
        internal BTTreeNodeSpec(string kind,string nodeId,BTTreeNodeSpec[] children)
        {
            this.kind = kind;
            this.nodeId = nodeId;
            this.children = children;
        }
        // Only arrays and strings cross the mod-message bridge.
        internal object[] ToPayload()
        {
            if (kind == "custom")
                return new object[] { "custom", nodeId };
            object[] childPayloads = new object[children.Length];
            for (int i = 0; i < children.Length; i++)
                childPayloads[i] = children[i].ToPayload();
            return new object[] { kind, childPayloads };
        }
    }
    public static class BTTree
    {
        public static BTTreeNodeSpec Custom(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                throw new ArgumentException("Node ID is required.", "nodeId");
            return new BTTreeNodeSpec("custom", nodeId, null);
        }
        public static BTTreeNodeSpec Sequence(params BTTreeNodeSpec[] children)
        {
            return CreateComposite("sequence", children);
        }
        public static BTTreeNodeSpec Selector(params BTTreeNodeSpec[] children)
        {
            return CreateComposite("selector", children);
        }
        public static BTTreeNodeSpec Loop(BTTreeNodeSpec child)
        {
            if (child == null)
                throw new ArgumentNullException("child");
            return new BTTreeNodeSpec("loop", null, new BTTreeNodeSpec[] { child });
        }
        private static BTTreeNodeSpec CreateComposite(string kind, BTTreeNodeSpec[] children)
        {
            if (children == null || children.Length == 0)
                throw new ArgumentException("At least one child is required.");
            foreach (BTTreeNodeSpec child in children)
            {
                if (child == null)
                    throw new ArgumentException("Tree children cannot be null.");
            }
            return new BTTreeNodeSpec(kind, null, children);
        }
    }
    public class BTAPI : IDisposable
    {
        private const long HeartbeatMessageId = 786431205;
        private const long RegistrationMessageId = 786431206;
        private const int HeartbeatTimeout = 30;
        public const int CustomNodeRunning = 0;
        public const int CustomNodeSuccess = 1;
        public const int CustomNodeFailure = 2;
        public bool Heartbeat { get; private set; }
        private int ticksSinceHeartbeat;
        private bool registered;
        private Action onRegisteredCallback;
        private readonly Dictionary<string, Action<bool>> nodeCallbacks = new Dictionary<string, Action<bool>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Action<bool>> treeCallbacks = new Dictionary<string, Action<bool>>(StringComparer.Ordinal);
        public void Init(Action callback)
        {
            if (registered || MyAPIGateway.Utilities == null)
                return;
            onRegisteredCallback = callback;
            ticksSinceHeartbeat = HeartbeatTimeout;
            Heartbeat = false;
            MyAPIGateway.Utilities.RegisterMessageHandler(HeartbeatMessageId,HeartbeatHandler);
            MyAPIGateway.Utilities.RegisterMessageHandler(RegistrationMessageId,RegistrationHandler);
            registered = true;
            MyLog.Default.WriteLine("[BTAPI] BTEngine handlers registered.");
        }
        public void Update()
        {
            if (!registered)
                return;
            if (ticksSinceHeartbeat < HeartbeatTimeout)
                ticksSinceHeartbeat++;
            if (ticksSinceHeartbeat >= HeartbeatTimeout)
                Heartbeat = false;
        }
        private void HeartbeatHandler(object message)
        {
            if ((message as string) != "BTEngine:Ready")
                return;
            bool wasReady = Heartbeat;
            ticksSinceHeartbeat = 0;
            Heartbeat = true;
            if (!wasReady && onRegisteredCallback != null)
                onRegisteredCallback();
        }
        public bool RegisterCustomNode(string id,Func<object> createState,Action<IMyCharacter, object> start,Func<IMyCharacter, object, float, int> tick,Action<IMyCharacter, object> end,Action<bool> onResult = null)
        {
            if (!Heartbeat ||
                string.IsNullOrEmpty(id) ||
                createState == null ||
                start == null ||
                tick == null ||
                end == null)
                return false;
            if (onResult != null)
                nodeCallbacks[id] = onResult;
            MyAPIGateway.Utilities.SendModMessage(
                RegistrationMessageId,
                new object[]
                {"register",id,createState,start,tick,end});
            return true;
        }
        public bool RegisterTree(string treeId,BTTreeNodeSpec root,Action<bool> onResult = null)
        {
            if (!Heartbeat ||
                string.IsNullOrEmpty(treeId) ||
                root == null)
                return false;
            if (onResult != null)
                treeCallbacks[treeId] = onResult;
            MyAPIGateway.Utilities.SendModMessage(
                RegistrationMessageId,
                new object[]
                {"registerTree",treeId,root.ToPayload()});
            return true;
        }
        public bool StartTree(IMyCharacter character, string treeId)
        {
            if (!Heartbeat ||
                character == null ||
                character.MarkedForClose ||
                string.IsNullOrEmpty(treeId))
                return false;
            MyAPIGateway.Utilities.SendModMessage(
                RegistrationMessageId,
                new object[]
                {"startTree",character.EntityId,treeId});
            return true;
        }
        public bool StopTree(IMyCharacter character)
        {
            if (character == null)
                return false;
            MyAPIGateway.Utilities.SendModMessage(
                RegistrationMessageId,
                new object[]
                {"stopTree",character.EntityId});
            return true;
        }
        public bool UnregisterCustomNode(string id)
        {
            if (!Heartbeat || string.IsNullOrEmpty(id))
                return false;
            MyAPIGateway.Utilities.SendModMessage(
                RegistrationMessageId,
                new object[] { "unregister", id });
            return true;
        }
        public bool UnregisterTree(string treeId)
        {
            if (!Heartbeat || string.IsNullOrEmpty(treeId))
                return false;
            MyAPIGateway.Utilities.SendModMessage(
                RegistrationMessageId,
                new object[] { "unregisterTree", treeId });
            return true;
        }
        private void RegistrationHandler(object message)
        {
            string response = message as string;
            if (string.IsNullOrEmpty(response))
                return;
            if (HandleResult(
                response,
                "BTEngine:CustomNodeRegistered:",
                "BTEngine:CustomNodeRejected:",
                nodeCallbacks))
                return;
            HandleResult(
                response,
                "BTEngine:TreeRegistered:",
                "BTEngine:TreeRejected:",
                treeCallbacks);
        }
        private bool HandleResult(string response,string acceptedPrefix,string rejectedPrefix,Dictionary<string, Action<bool>> callbacks)
        {
            bool success;
            string id;
            if (response.StartsWith(acceptedPrefix, StringComparison.Ordinal))
            {
                success = true;
                id = response.Substring(acceptedPrefix.Length);
            }
            else if (response.StartsWith(rejectedPrefix, StringComparison.Ordinal))
            {
                success = false;
                id = response.Substring(rejectedPrefix.Length);
            }
            else
            {
                return false;
            }
            Action<bool> callback;
            if (callbacks.TryGetValue(id, out callback))
            {
                callbacks.Remove(id);
                callback(success);
            }
            return true;
        }
        public void Dispose()
        {
            if (registered && MyAPIGateway.Utilities != null)
            {
                MyAPIGateway.Utilities.UnregisterMessageHandler(HeartbeatMessageId,HeartbeatHandler);
                MyAPIGateway.Utilities.UnregisterMessageHandler(RegistrationMessageId,RegistrationHandler);
            }
            nodeCallbacks.Clear();
            treeCallbacks.Clear();
            registered = false;
            Heartbeat = false;
            ticksSinceHeartbeat = HeartbeatTimeout;
            onRegisteredCallback = null;
        }
    }
}
