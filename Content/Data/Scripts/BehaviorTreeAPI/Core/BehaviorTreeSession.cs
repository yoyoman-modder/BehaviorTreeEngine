using BehaviorTreeAPI.Core;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Utils;

namespace BehaviorTreeAPI
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class BehaviorTreeSession : MySessionComponentBase
    {
        private string System = "BTEngine";
        public static BehaviorTreeSession Instance { get; private set; }
        public const ushort API_REQUEST_ID = 7787;
        public const ushort API_RESPONSE_ID = 7778;
        public const ushort BTLINK_ID = 7789;
        public const int API_VERSION = 1;
        private const string REQUEST_NAME = "BTEngineAPI";
        private const string BTLINK_REQUEST = "BTEngine:Check";
        private const string BTLINK_RESPONSE = "BTEngine:Ready";
        private static readonly string logname = $"[{REQUEST_NAME}] ";
        private bool _registered;
        private bool _unloading;
        private readonly Dictionary<string, Delegate> _apiMethods = new Dictionary<string, Delegate>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _externalCharactersLogicOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public override void LoadData()
        {
            Instance = this;
            _unloading = false;
            _registered = false;
            _apiMethods.Clear();
            _externalCharactersLogicOwners.Clear();
            MyLog.Default.WriteLine(logname +"Provider LoadData.");
            BuildApiMethods();
            Log("Provider LoadData.",true);
            MyAPIGateway.Utilities.RegisterMessageHandler(API_REQUEST_ID, MessageHandler);
            _registered = true;
            MyLog.Default.WriteLine(
                logname +
                "Provider registered. " +
                "RequestID=" + API_REQUEST_ID +
                " ResponseID=" + API_RESPONSE_ID);
            MyAPIGateway.Utilities.RegisterMessageHandler(BTLINK_ID,BTLinkHandler);
            Log("Provider registered.", true);
            InitDatabases();
        }
        private void BTLinkHandler(object obj)
        {
            if (_unloading || obj == null)
                return;
            string request = obj as string;
            if (request != BTLINK_REQUEST)
                return;
            MyAPIGateway.Utilities.SendModMessage(BTLINK_ID,BTLINK_RESPONSE);
            MyLog.Default.WriteLine(logname + "BTLink sent.");
            Log("BTLink request received.", true);
        }

        private void MessageHandler(object obj)
        {
            if (_unloading)
                return;
            if (obj == null)
                return;
            string request = obj as string;
            if (request == null)
                return;
            if (!string.Equals(request, REQUEST_NAME, StringComparison.Ordinal))
            {
                return;
            }
            MyLog.Default.WriteLine(logname +"Client request received.");
            Log("Client request received.", true);
            if (_unloading)
                return;
            try
            {
                MyAPIGateway.Utilities.SendModMessage(API_RESPONSE_ID, _apiMethods);
                MyLog.Default.WriteLine(logname +"API response sent.");
                Log("API response sent.", true);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(logname +"Failed to send API response:");
                Log("Failed to send API response:", true);
                MyLog.Default.WriteLine(e.ToString());
            }
        }
        public override void UpdateBeforeSimulation()
        {
           
            base.UpdateBeforeSimulation();
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
        // =====================================================
        // API METHODS
        // =====================================================
        private void BuildApiMethods()
        {
            _apiMethods.Clear();
            _apiMethods.Add("VerifyVersion", new Func<int, bool>(VerifyVersion));
            //_apiMethods.Add("RegisterExternalComponent",new Func<string, Func<MyGameLogicComponent>, bool>(BehaviorTreeCharacterComponentDatabase.RegisterExternalComponent));
            _apiMethods.Add("RegisterExternalComponent", new Func<Dictionary<string, object>, bool>(RegisterExternalComponent));
            MyLog.Default.WriteLine(logname +"API methods created: " +_apiMethods.Count);
        }
        private bool RegisterExternalComponent(Dictionary<string, object> data)
        {
            if (_unloading)
                return false;
            if (data == null)
                return false;
            string subtype = GetString(data, "subtype");
            string owner = GetString(data, "Owner");
            Func<MyGameLogicComponent> factory = data.ContainsKey("factory")
                   ? data["factory"] as Func<MyGameLogicComponent>
                   : null;
            if (string.IsNullOrWhiteSpace(subtype))
            {
                MyLog.Default.WriteLine(
                   logname +
                   "External registration rejected: " +
                   "empty subtype.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(owner))
            {
                MyLog.Default.WriteLine(
                    logname +
                    "External registration rejected: " +
                    "empty owner.");
                return false;
            }
            subtype = subtype.Trim();
            owner = owner.Trim();
            // =================================================
            // DUPLICATE / OWNERSHIP CHECK
            // =================================================
            string existingOwner;
            bool updating = _externalCharactersLogicOwners.TryGetValue(subtype, out existingOwner);
            if (updating)
            {
                // Same mod can update its own logic.
                if (!string.Equals(existingOwner, owner, StringComparison.OrdinalIgnoreCase))
                {
                    MyLog.Default.WriteLine(
                        logname +
                        "CharacterComponent registration REJECTED. " +
                        "Subtype '" +
                        subtype +
                        "' is already owned by '" +
                        existingOwner +
                        "'. Requested by '" +
                        owner +
                        "'.");
                    return false;
                }
                MyLog.Default.WriteLine(
                   logname +
                   "Existing owner updating CharacterComponent: " +
                   subtype +
                   " / " +
                   owner);
            }
            try
            {
                if (factory == null)
                    return false;
                bool result = updating
                    ? BehaviorTreeCharacterComponentDatabase.UpdateExternalComponent(subtype, factory)
                    : BehaviorTreeCharacterComponentDatabase.RegisterExternalComponent(subtype, factory);
                if (!result)
                {
                    MyLog.Default.WriteLine(
                        logname +
                        "Database rejected ammo: " +
                        subtype);
                    return false; 
                }
                // Only claim ownership after successful registration.
                _externalCharactersLogicOwners[subtype] = owner;
                MyLog.Default.WriteLine(
                    logname +
                    "External CharacterComponent registered: " +
                    subtype +
                    " Owner=" +
                    owner);
                return true;
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(
                   logname +
                   "Error registering external CharacterComponent: " +
                   subtype);
                MyLog.Default.WriteLine(e.ToString());
                return false;
            }
        }
        private bool VerifyVersion(int version)
        {
            if (_unloading)
                return false;
            bool result = version == API_VERSION;
            MyLog.Default.WriteLine(
                logname +
                "Version check: " +
                version +
                " -> " +
                result);
            return result;
        }
        // =====================================================
        // DATA HELPERS
        // =====================================================
        private static string GetString(Dictionary<string, object> data, string key)
        {
            object value;
            if (!data.TryGetValue(key, out value))
            {
                return string.Empty;
            }
            return value as string ?? string.Empty;
        }
        protected override void UnloadData()
        {
            _unloading = true;
            MyLog.Default.WriteLine(logname +"Provider unloading.");
            if (_registered)
            {
                try
                {
                    if (MyAPIGateway.Utilities != null)
                    {
                        MyAPIGateway.Utilities.UnregisterMessageHandler(API_REQUEST_ID, MessageHandler);
                        MyAPIGateway.Utilities.UnregisterMessageHandler(BTLINK_ID,BTLinkHandler);
                    }
                }
                catch (Exception e)
                {
                    MyLog.Default.WriteLine(logname +"Error unregistering handler:");
                    MyLog.Default.WriteLine(e.ToString());
                }
                _registered = false;
            }
            _apiMethods.Clear();
            _externalCharactersLogicOwners.Clear();
            Instance = null;
        }
    }
}
