using AquaExpansionAPITestMod.TestBTLogic;
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
        //private bool _apiRequested;
        private bool _BTEapiRequested;
        /// <summary>
        /// Init BTreeAPI in Session LoadData
        /// </summary>
        public override void LoadData()
        {
            I = this;
            MyLog.Default.WriteLine($"[TestAPI] LoadData started.");
            //mod
            //AquaExpansionAPI.OnAvailable +=OnAquaExpansionAvailable;
            //AquaExpansionAPI.Initialize();
            //bt test
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
        /*private void RequestProvider()
        {
            if (_apiRequested)
                return;
            if (MyAPIGateway.Session == null)
                return;
            _apiRequested = true;
            AquaExpansionAPI.RequestProvider();
            
        }*/
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
            //MyAPIGateway.Utilities.ShowMessage(System, $"BTLink = {BTreeAPI.BTlinkHeartbeat}");
            //RequestProvider();
            //RequestBTreeProvider();
            PressToSpawnAnimal();
        }
        //Register new ammo profiles
        /*private void OnAquaExpansionAvailable()
        {
            MyAPIGateway.Utilities.ShowMessage(System,"AquaExpansion API AVAILABLE!");
            //register new ammo profile
            HydroAmmoProfileData profile = new HydroAmmoProfileData();
            profile.SubtypeId = "Missile200mm";
            profile.Mass = 10f;
            profile.DragCoefficient = 0.5f;
            profile.SplashType = AquaSplashType.Missile;
            profile.MinimumSpeed = 20f;
            profile.MaxRange = 3000f;
            profile.WaterStability = 1f;
            profile.ExitVelocityMultiplier = 1f;
            profile.ExitDamageMultiplier = 1f;
            bool result = AquaExpansionAPI.RegisterAmmoProfile(profile);
            MyAPIGateway.Utilities.ShowMessage(System,"Ammo registration: " + result);
            bool bresult = AquaExpansionAPI.RegisterMuzzleBurst("MyHandWeapon", "MyBurstEffect");
            MyAPIGateway.Utilities.ShowMessage(System, "Muzzle registration: " + bresult);
            if (AquaExpansionAPI.RegisterWeaponEffect("MySubtype","MyWeaponEffect"))
            {
                MyAPIGateway.Utilities.ShowMessage(System, "Weapon Effect registration: ");
            }
        }*/
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
            //AquaExpansionAPI.OnAvailable -=OnAquaExpansionAvailable;
            //AquaExpansionAPI.Close();
            BTreeAPI.OnAvailable -= OnBtreeAvailable;
            BTreeAPI.Close();
            I = null;
        }
    }
}
