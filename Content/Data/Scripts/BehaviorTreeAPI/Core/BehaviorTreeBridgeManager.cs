using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace BehaviorTreeAPI.Core
{
    public sealed class BehaviorTreeBridgeManager
    {
        private readonly Dictionary<long, ActiveTree> activeTrees = new Dictionary<long, ActiveTree>();
        private readonly Func<IMyCharacter, string, BehaviorTree> buildTree;
        private sealed class ActiveTree
        {
            public IMyCharacter Character;
            public BehaviorTree Tree;
        }
        public BehaviorTreeBridgeManager(Func<IMyCharacter, string, BehaviorTree> buildTree)
        {
            this.buildTree = buildTree;
        }
        public bool Start(IMyCharacter character, string treeId)
        {
            if (character == null || character.MarkedForClose ||
                string.IsNullOrEmpty(treeId))
                return false;
            BehaviorTree tree = buildTree(character, treeId);
            if (tree == null)
                return false;
            Stop(character);
            activeTrees[character.EntityId] = new ActiveTree
            {
                Character = character,
                Tree = tree
            };
            return true;
        }
        public void Stop(IMyCharacter character)
        {
            if (character != null)
                Stop(character.EntityId);
        }
        public void Stop(long characterId)
        {
            ActiveTree active;
            if (!activeTrees.TryGetValue(characterId, out active))
                return;
            active.Tree.Stop(active.Character);
            activeTrees.Remove(characterId);
        }
        public void Update(float dt)
        {
            var characterIds = new List<long>(activeTrees.Keys);
            foreach (long characterId in characterIds)
            {
                ActiveTree active;
                if (!activeTrees.TryGetValue(characterId, out active))
                    continue;
                if (active.Character == null || active.Character.MarkedForClose)
                {
                    active.Tree.Stop(active.Character);
                    activeTrees.Remove(characterId);
                    continue;
                }
                active.Tree.Update(active.Character, dt);
                // A completed tree has stopped itself.
                if (!active.Tree.IsRunning)
                    activeTrees.Remove(characterId);
            }
        }
        public void StopAll()
        {
            var characterIds = new List<long>(activeTrees.Keys);
            foreach (long characterId in characterIds)
                Stop(characterId);
        }
    }
}
