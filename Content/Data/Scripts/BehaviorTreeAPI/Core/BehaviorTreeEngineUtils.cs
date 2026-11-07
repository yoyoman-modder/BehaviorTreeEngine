using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Utils;

namespace BehaviorTreeAPI.Core
{
    public static class BehaviorTreeCharacterComponentDatabase
    {
        private static readonly Dictionary<string, Func<MyGameLogicComponent>> BTCharactersLogicData = new Dictionary<string, Func<MyGameLogicComponent>>(StringComparer.Ordinal);
        public static void Init()
        {
            BTCharactersLogicData.Clear();
        }
        public static bool RegisterExternalComponent(string subtype,Func<MyGameLogicComponent> factory)
        {
            if (string.IsNullOrWhiteSpace(subtype) || factory == null)
                return false;
            string subtypeId = subtype.Trim();
            if (BTCharactersLogicData.ContainsKey(subtypeId))
            {
                MyLog.Default.WriteLine(
                    "BTEngineAPI Duplicate CharacterComponent rejected: " +
                    subtypeId);
                return false;
            }
            BTCharactersLogicData.Add(subtypeId, factory);
            MyLog.Default.WriteLine(
                "BTEngineAPI External CharacterComponent registered: " +
                subtypeId + " -> " + factory.Invoke().ToString() + " | " + BTCharactersLogicData.Count + " components registered.");
            return true;
        }
        public static Func<MyGameLogicComponent> Get(string subtype)
        {
            if (string.IsNullOrWhiteSpace(subtype))
                return null;
            Func<MyGameLogicComponent> factory;
            if (BTCharactersLogicData.TryGetValue(subtype.Trim(), out factory))
                return factory;
            return null;
        }
        public static bool Unregister(string subtype)
        {
            if (string.IsNullOrWhiteSpace(subtype))
                return false;
            return BTCharactersLogicData.Remove(subtype.Trim());
        }
        public static void Clear()
        {
            BTCharactersLogicData.Clear();
        }
        public static Dictionary<string, Func<MyGameLogicComponent>> FillCharacterComponents()
        {
            return new Dictionary<string, Func<MyGameLogicComponent>>(BTCharactersLogicData);
        }
    }
}
