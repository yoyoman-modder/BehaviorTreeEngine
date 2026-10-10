using AquaExpansion.Core;
using Sandbox.Game;
using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace BehaviorTreeAPI.Core
{
    [MyStatLogicDescriptor("BTConnector")]
    public class BTConnector : MyStatLogic
    {
        private static MyStringHash BTConnectorId = MyStringHash.GetOrCompute("BTBridge");
        private bool ready = false;
        private BioLatentScheduler Biobuffer;
        private Dictionary<string, Func<MyGameLogicComponent>> BTLogicData = new Dictionary<string, Func<MyGameLogicComponent>>();
        public MyEntityStat BTBridge
        {
            get
            {
                MyEntityStat btbridge;
                if (this.m_stats.TryGetValue(BTConnectorId, out btbridge))
                {
                    return btbridge;
                }
                return btbridge;
            }
        }
        public override void Init(IMyCharacter character, Dictionary<MyStringHash, MyEntityStat> stats, string scriptName)
        {
            base.Init(character, stats, scriptName);
            MyEntityStat btbridge = this.BTBridge;
            if (btbridge != null)
            {
                if (btbridge.MaxValue == 1)
                {
                    ConstructBTData();
                }
            }
            Biobuffer = new BioLatentScheduler();
        }
        private void ConstructBTData()
        {
            BTLogicData = BehaviorTreeCharacterComponentDatabase.FillCharacterComponents();
            BehaviorTreeSession.Instance.Log($"Database {BTLogicData.Count}");
        }
        private void ConnectToCharacter()
        {
            if (ready)
                return;
            ready = true;
            Biobuffer.Schedule(GetCharacter, 2, false, 0);
        }
        private void GetCharacter()
        {
            if (base.Character != null && !base.Character.Closed && !base.Character.IsDead)
            {
                var bot = base.Character;
                if (bot != null)
                {
                    AddCharacterComponent(bot, bot.Definition.Id.SubtypeId.ToString());
                    BehaviorTreeSession.Instance.Log($"BTConnector initialized for {bot.DisplayName}");
                }
            }
        }
        private void AddCharacterComponent(IMyCharacter bot, string v)
        {
            if (bot == null || bot.MarkedForClose || bot.IsDead)
                return;
            if (string.IsNullOrEmpty(v))
                return;
            Func<MyGameLogicComponent> factory;
            if (!BTLogicData.TryGetValue(v, out factory))
            {
                BehaviorTreeSession.Instance.Log($"No component factory for key '{v}'");
                return;
            } 
            var newComp = factory();
            if (newComp == null)
                return;
            Type componentType = newComp.GetType();
            if (bot.GameLogic != null)
            {
                if (bot.GameLogic.GetType() == componentType)
                {
                    BehaviorTreeSession.Instance.Log($"Already has {bot.GameLogic.GetType().Name}");
                    return;
                }
            }
            bot.Components.Add(newComp);
            var builder = new MyObjectBuilder_EntityBase();
            newComp.Init(builder);
            BehaviorTreeSession.Instance.Log($"Attached {newComp.GetType().Name} to {bot.Definition.Id.SubtypeName}");
        }
        public override void Update()
        {
            ConnectToCharacter();
            Biobuffer.Update();
            base.Update();
        }
        public override void Close()
        {
            MyEntityStat btbridge = this.BTBridge;
            if (btbridge != null)
            {

            }
            Biobuffer.Clear();
            Biobuffer = null;
            base.Close();
        }
    }
}
