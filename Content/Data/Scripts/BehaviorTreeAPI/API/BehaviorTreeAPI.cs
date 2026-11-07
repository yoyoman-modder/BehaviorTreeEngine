using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Utils;

namespace BehaviorTreeAPI
{
    /// <summary>
    /// Client-side  BehaviorTreeAPI.
    ///
    /// Copy this file into another mod to communicate with
    /// BehaviorEngine.
    ///
    /// The AquaExpansion provider uses:
    ///     Request ID  = 7787
    ///     Response ID = 7778
    /// </summary>
    public static class BTreeAPI
    {
        public const ushort API_REQUEST_ID = 7787;
        public const ushort API_RESPONSE_ID = 7778;
        public const int API_VERSION = 1;
        private const string REQUEST_NAME = "BTEngineAPI";
        private static readonly string logname = $"[{REQUEST_NAME}] ";
        private static bool _initialized;
        private static bool _requestSent;
        private static bool _available;
        private static bool _unloading;
        private static string _ownerName;
        private static Dictionary<string, Delegate> _apiMethods;
        public static event Action OnAvailable;
        public const uint BTLINK_ID = 7789;
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
        public static void RequestProvider()
        {
            if (!_initialized)
                return;
            if (_unloading)
                return;
            if (_requestSent)
                return;
            if (_available)
                return;
            if (MyAPIGateway.Utilities == null)
                return;
            if (MyAPIGateway.Session == null)
                return;
            try
            {
                MyAPIGateway.Utilities.SendModMessage(API_REQUEST_ID, REQUEST_NAME);
                _requestSent = true;

                MyLog.Default.WriteLine(logname +"Provider request sent.");
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(logname +"Request error:");
                MyLog.Default.WriteLine(e.ToString());
            }
        }
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
        public static bool RegisterExternalComponent(string subtype, Func<MyGameLogicComponent> factory)
        {
            if (!_available)
                return false;
            if (string.IsNullOrWhiteSpace(subtype) ||
                factory == null)
                return false;
            Func<Dictionary<string, object>, bool> method = GetMethod<Func<Dictionary<string, object>, bool>>("RegisterExternalComponent");
            //Func<string, Func<MyGameLogicComponent>, bool> method = GetMethod<Func<string, Func<MyGameLogicComponent>, bool>>("RegisterExternalComponent");
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
        public static void UpdateHeartbeat()
        {
            if (_unloading || MyAPIGateway.Session == null)
                return;
            _heartbeatReceived = false;
            MyAPIGateway.Utilities.SendModMessage(BTLINK_ID,BTLINK_REQUEST);
            _available = _heartbeatReceived;
        }
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
            _heartbeatReceived = false;
            _available = false;
            _initialized = false;
            _ownerName = null;
            OnAvailable = null;
            MyLog.Default.WriteLine(logname +"Client unloaded.");
        }
    }
}
