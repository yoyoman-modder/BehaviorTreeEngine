using BehaviorTreeAPI;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using VRage.Game;
using VRage.Game.Components;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace AquaExpansionAPITestMod
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation | MyUpdateOrder.BeforeSimulation)]
    public class APITesSession : MySessionComponentBase
    {
        private string System = "TestAPI";
        public static APITesSession I;
        private bool _BTEapiRequested;
        /// <summary>
        /// Init BTreeAPI in Session LoadData
        /// </summary>
        public override void LoadData()
        {
            I = this;
            MyLog.Default.WriteLine($"[TestAPI] LoadData started.");
            BTreeAPI.OnAvailable += OnBtreeAvailable;
            BTreeAPI.Initialize();
        }
        //Register custom Gamelogic with BehaviorTree to add into providerdatabase and run on your character
        private void OnBtreeAvailable()
        {
            MyAPIGateway.Utilities.ShowMessage(System, "Behavior Tree API AVAILABLE!");
            if (BTreeAPI.RegisterExternalComponent("Space_Wolf", () => new WolfTestBehaviorTree()))
            {
                MyAPIGateway.Utilities.ShowMessage(System, "External component registered successfully.");
            }
        }
        /// <summary>
        /// Request BtreeAPI provider here
        /// </summary>
        /// <param name="sessionComponent"></param>
        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            RequestBTreeProvider();
            base.Init(sessionComponent);
        }
        private void RequestBTreeProvider()
        {
            if (_BTEapiRequested)
                return;
            _BTEapiRequested = true;
            BTreeAPI.RequestProvider();
        }
        /// <summary>
        /// Update BLLink heartbeat 
        /// </summary>
        public override void UpdateBeforeSimulation()
        {
            BTreeAPI.UpdateHeartbeat();
            PressToSpawnAnimal();
        }
        private void PressToSpawnAnimal()
        {
            if (MyAPIGateway.Input.IsNewKeyPressed(MyKeys.Control))
            {
                SpawnTestAnimal("Wolf", "Wolf");
            }
        }
        private void SpawnTestAnimal(String BotSubtype, string Creaturename)
        {
            var player = MyAPIGateway.Session?.Player;
            var character = player?.Character;
            if (character == null || character.Closed || character.IsDead)
                return;
            if (string.IsNullOrEmpty(BotSubtype))
            {
                MyAPIGateway.Utilities.ShowMessage(System, $"Invalid Botname");
                return;
            }
            Vector3D playerpos = character.GetPosition();
            var objforwardv = new Vector3(0, 0, 0);
            var objupv = new Vector3(0, 2, 0);
            double forwarddistance;
            double rightdistance;
            forwarddistance = 5;
            rightdistance = 0;
            Vector3D objectpos = new Vector3D(0, 0, 0);
            var posforward = playerpos + character.PositionComp.WorldMatrixRef.Forward * forwarddistance;
            var posright = playerpos + character.PositionComp.WorldMatrixRef.Right * rightdistance;
            long creatureid = MyVisualScriptLogicProvider.SpawnBot(BotSubtype, posforward, objforwardv, objupv, Creaturename);
        }
        //Unload
        protected override void UnloadData()
        {
            BTreeAPI.OnAvailable -= OnBtreeAvailable;
            BTreeAPI.Close();
            I = null;
        }
    }
}
