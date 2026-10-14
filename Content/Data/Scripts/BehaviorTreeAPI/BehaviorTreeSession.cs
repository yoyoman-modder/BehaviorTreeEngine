using BehaviorTreeAPI.Core;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace BehaviorTreeAPI
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class BehaviorTreeSession : MySessionComponentBase
    {
        private string System = "BTEngine";
        private const int HeartbeatInterval = 10;
        private const int MaximumTreeDepth = 32;
        private const int MaximumChildren = 64;
        public static BehaviorTreeSession Instance { get; private set; }
        public const long HeartbeatMessageId = 786431205;
        public const long RegistrationMessageId = 786431206;
        private readonly Dictionary<string, object[]> treeDefinitions = new Dictionary<string, object[]>(StringComparer.Ordinal);
        private int tick;
        private bool handlerRegistered;
        private TimeSpan lastUpdateTime;
        private BehaviorTreeBridgeManager treeManager;
        public bool IsInitialized { get; private set; }
        public override void LoadData()
        {
            Instance = this;
            InitDatabases();
            tick = 0;
            treeManager = new BehaviorTreeBridgeManager(BuildTree);
            MyAPIGateway.Utilities.RegisterMessageHandler(RegistrationMessageId,HandleRegistrationMessage);
            handlerRegistered = true;
            lastUpdateTime = MyAPIGateway.Session.ElapsedPlayTime;
            IsInitialized = true;
        }
        public override void UpdateBeforeSimulation()
        {
            if (!IsInitialized)
            {
                base.UpdateBeforeSimulation();
                return;
            }
            tick++;
            if (tick % HeartbeatInterval == 0)
            {
                MyAPIGateway.Utilities.SendModMessage(HeartbeatMessageId,"BTEngine:Ready");
            }
            TimeSpan now = MyAPIGateway.Session.ElapsedPlayTime;
            float dt = (float)(now - lastUpdateTime).TotalSeconds;
            lastUpdateTime = now;
            if (dt < 0f)
                dt = 0f;
            if (treeManager != null)
                treeManager.Update(dt);
            base.UpdateBeforeSimulation();
        }
        private void HandleRegistrationMessage(object message)
        {
            if (!IsInitialized)
                return;
            object[] data = message as object[];
            if (data != null)
            {
                HandleBridgeMessage(data);
                return;
            }
            // Preserve the existing string-based provider registration behavior.
            string request = message as string;
            const string prefix = "BTEngine:Register:";
            if (string.IsNullOrEmpty(request) ||
                !request.StartsWith(prefix, StringComparison.Ordinal))
                return;
            string clientId = request.Substring(prefix.Length);
            if (string.IsNullOrEmpty(clientId))
                return;
            MyAPIGateway.Utilities.SendModMessage(RegistrationMessageId,"BTEngine:Registered:" + clientId);
        }
        private void HandleBridgeMessage(object[] data)
        {
            if (data.Length == 0)
                return;
            string operation = data[0] as string;
            if (string.IsNullOrEmpty(operation))
                return;
            switch (operation)
            {
                case "register":
                    HandleNodeRegistration(data);
                    break;
                case "unregister":
                    HandleNodeUnregistration(data);
                    break;
                case "registerTree":
                    HandleTreeRegistration(data);
                    break;
                case "unregisterTree":
                    HandleTreeUnregistration(data);
                    break;
                case "startTree":
                    HandleStartTree(data);
                    break;
                case "stopTree":
                    HandleStopTree(data);
                    break;
            }
        }
        private void HandleNodeRegistration(object[] data)
        {
            if (data.Length != 6)
                return;
            string id = data[1] as string;
            bool added = CustomNodeRegistry.Register(
                id,
                data[2] as Func<object>,
                data[3] as Action<IMyCharacter, object>,
                data[4] as Func<IMyCharacter, object, float, int>,
                data[5] as Action<IMyCharacter, object>);
            MyAPIGateway.Utilities.SendModMessage(
                RegistrationMessageId,
                (added
                    ? "BTEngine:CustomNodeRegistered:"
                    : "BTEngine:CustomNodeRejected:") + id);
        }
        private void HandleNodeUnregistration(object[] data)
        {
            if (data.Length != 2)
                return;
            string id = data[1] as string;
            CustomNodeRegistry.Unregister(id);
        }
        private void HandleTreeRegistration(object[] data)
        {
            if (data.Length != 3)
                return;
            string treeId = data[1] as string;
            object[] rootSpec = data[2] as object[];
            bool accepted =
                !string.IsNullOrEmpty(treeId) &&
                rootSpec != null &&
                BuildNode(rootSpec, 0) != null;
            if (accepted)
                treeDefinitions[treeId] = rootSpec;
            MyAPIGateway.Utilities.SendModMessage(
                RegistrationMessageId,
                (accepted
                    ? "BTEngine:TreeRegistered:"
                    : "BTEngine:TreeRejected:") + treeId);
        }
        private void HandleTreeUnregistration(object[] data)
        {
            if (data.Length != 2)
                return;
            string treeId = data[1] as string;
            if (!string.IsNullOrEmpty(treeId))
                treeDefinitions.Remove(treeId);
        }
        private void HandleStartTree(object[] data)
        {
            if (data.Length != 3 || treeManager == null)
                return;
            if (!(data[1] is long))
                return;
            long characterId = (long)data[1];
            string treeId = data[2] as string;
            if (string.IsNullOrEmpty(treeId))
                return;
            IMyCharacter character = FindCharacter(characterId);
            if (character == null)
                return;
            treeManager.Start(character, treeId);
        }
        private void HandleStopTree(object[] data)
        {
            if (data.Length != 2 ||
                treeManager == null ||
                !(data[1] is long))
                return;
            treeManager.Stop((long)data[1]);
        }
        private IMyCharacter FindCharacter(long entityId)
        {
            var entity = MyAPIGateway.Entities.GetEntityById(entityId);
            return entity as IMyCharacter;
        }
        private BehaviorTree BuildTree(IMyCharacter character,string treeId)
        {
            object[] rootSpec;
            if (!treeDefinitions.TryGetValue(treeId, out rootSpec))
                return null;
            BehaviorNode root = BuildNode(rootSpec, 0);
            if (root == null)
                return null;
            return new BehaviorTree(root);
        }
        private BehaviorNode BuildNode(object[] spec, int depth)
        {
            if (spec == null ||
                spec.Length != 2 ||
                depth > MaximumTreeDepth)
                return null;
            string kind = spec[0] as string;
            if (string.IsNullOrEmpty(kind))
                return null;
            if (kind == "custom")
            {
                string nodeId = spec[1] as string;
                if (string.IsNullOrEmpty(nodeId))
                    return null;
                BehaviorNode customNode;
                if (CustomNodeRegistry.TryCreate(nodeId, out customNode))
                    return customNode;
                return null;
            }
            object[] childSpecs = spec[1] as object[];
            if (childSpecs == null ||
                childSpecs.Length == 0 ||
                childSpecs.Length > MaximumChildren)
                return null;
            var children = new List<BehaviorNode>();
            foreach (object childSpecObject in childSpecs)
            {
                object[] childSpec = childSpecObject as object[];
                BehaviorNode child = BuildNode(childSpec, depth + 1);
                if (child == null)
                    return null;
                children.Add(child);
            }
            if (kind == "sequence")
                return new SequenceNode(children.ToArray());
            if (kind == "selector")
                return new SelectorNode(children.ToArray());
            if (kind == "loop" && children.Count == 1)
                return new LoopNode(children[0]);
            return null;
        }
        private void InitDatabases()
        {
            BehaviorTreeCharacterComponentDatabase.Init();
        }
        public void Log(string message, bool log = false)
        {
            if (log && !string.IsNullOrEmpty(message))
            {
                MyAPIGateway.Utilities.ShowMessage(System, message);
            }
        }
        protected override void UnloadData()
        {
            IsInitialized = false;
            if (treeManager != null)
            {
                treeManager.StopAll();
                treeManager = null;
            }
            if (handlerRegistered && MyAPIGateway.Utilities != null)
            {
                MyAPIGateway.Utilities.UnregisterMessageHandler(RegistrationMessageId,HandleRegistrationMessage);
            }
            handlerRegistered = false;
            treeDefinitions.Clear();
            CustomNodeRegistry.Clear();
            tick = 0;
            Instance = null;
        }
    }
}
