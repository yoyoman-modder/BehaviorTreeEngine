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
        private static string SystemName = "BTEngine";
        public static BehaviorTreeSession Instance { get; private set; }
        public const ushort API_REQUEST_ID = 7787;
        public const ushort API_RESPONSE_ID = 7778;
        public const ushort BTLINK_ID = 7789;
        public const int API_VERSION = 1;
        private const string REQUEST_NAME = "BTEngineAPI";
        private const string BTLINK_REQUEST = "BTEngine:Check";
        private const string BTLINK_RESPONSE = "BTEngine:Ready";
        private static readonly string logname = $"[{SystemName}] ";
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
            ConsoleLog(logname +
                "Provider registered. " +
                "RequestID=" + API_REQUEST_ID +
                " ResponseID=" + API_RESPONSE_ID);
            MyAPIGateway.Utilities.RegisterMessageHandler(BTLINK_ID,BTLinkHandler);
            //Log("Provider registered.", true);
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
            //Log("BTLink request received.", true);
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
            ConsoleLog(logname + "Client request received.");
            //Log("Client request received.", true);
            if (_unloading)
                return;
            try
            {
                MyAPIGateway.Utilities.SendModMessage(API_RESPONSE_ID, _apiMethods);
                ConsoleLog(logname + "API response sent.");
                //Log("API response sent.", true);
            }
            catch (Exception e)
            {
                ConsoleLog(logname + "Failed to send API response:");
                ConsoleLog(e.ToString());
                //Log("Failed to send API response:", true);
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
                MyAPIGateway.Utilities.ShowMessage(SystemName, message);
            }
        }
        public void ConsoleLog(string message, bool log = true)
        {
            if (log && !string.IsNullOrEmpty(message))
            {
                MyLog.Default.WriteLine(message);
            }
        }
        // =====================================================
        // API METHODS
        // =====================================================
        private void BuildApiMethods()
        {
            _apiMethods.Clear();
            _apiMethods.Add("VerifyVersion", new Func<int, bool>(VerifyVersion));
            _apiMethods.Add("RegisterExternalComponent", new Func<Dictionary<string, object>, bool>(RegisterExternalComponent));
            ConsoleLog(logname + "API methods created: " + _apiMethods.Count);
        }
        private bool RegisterExternalComponent(Dictionary<string, object> data)
        {
            if (_unloading || data == null)
                return false;
            string subtype = GetString(data, "subtype");
            string owner = GetString(data, "Owner");
            object factoryValue;
            Func<MyGameLogicComponent> factory =
                data.TryGetValue("factory", out factoryValue)
                    ? factoryValue as Func<MyGameLogicComponent>
                    : null;
            if (string.IsNullOrWhiteSpace(subtype))
            {
                ConsoleLog(logname + "External registration rejected: empty subtype.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(owner))
            {
                ConsoleLog(logname + "External registration rejected: empty owner.");
                return false;
            }
            if (factory == null)
            {
                ConsoleLog(logname + "External registration rejected: missing or invalid factory.");
                return false;
            }
            subtype = subtype.Trim();
            owner = owner.Trim();
            string existingOwner;
            bool updating = _externalCharactersLogicOwners.TryGetValue(subtype, out existingOwner);
            if (updating)
            {
                if (!string.Equals(existingOwner,owner,StringComparison.OrdinalIgnoreCase))
                {
                    ConsoleLog(logname +
                        "CharacterComponent registration REJECTED. " +
                        "Subtype '" + subtype +
                        "' is already owned by '" + existingOwner +
                        "'. Requested by '" + owner + "'.");
                    return false;
                }
                ConsoleLog(logname +
                    "Existing owner updating CharacterComponent: " +
                    subtype + " / " + owner);
            }
            try
            {
                bool result = updating
                    ? BehaviorTreeCharacterComponentDatabase.UpdateExternalComponent(subtype, factory)
                    : BehaviorTreeCharacterComponentDatabase.RegisterExternalComponent(subtype, factory);
                if (!result)
                {
                    ConsoleLog(logname + "Database rejected Component: " + subtype);
                    return false;
                }
                _externalCharactersLogicOwners[subtype] = owner;
                ConsoleLog(logname +
                    "External CharacterComponent registered: " +
                    subtype + " Owner=" + owner);
                return true;
            }
            catch (Exception e)
            {
                ConsoleLog(logname +
                    "Error registering external CharacterComponent: " +
                    subtype);
                ConsoleLog(e.ToString());
                return false;
            }
        }
        private bool VerifyVersion(int version)
        {
            if (_unloading)
                return false;
            bool result = version == API_VERSION;
            ConsoleLog(logname +
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
            ConsoleLog(logname + "Provider unloading.");
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
                    ConsoleLog(logname + "Error unregistering handler:");
                    ConsoleLog(e.ToString());
                }
                _registered = false;
            }
            _apiMethods.Clear();
            _externalCharactersLogicOwners.Clear();
            Instance = null;
        }
    }
}
