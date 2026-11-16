using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Utils;

namespace AquaExpansion.API
{
    /// <summary>
    /// Client-side AquaExpansion API.
    ///
    /// Copy this file into another mod to communicate with
    /// AquaExpansion.
    ///
    /// The AquaExpansion provider uses:
    ///     Request ID  = 8888
    ///     Response ID = 8889
    /// </summary>
    public static class AquaExpansionAPI
    {
        public const ushort API_REQUEST_ID = 8888;
        public const ushort API_RESPONSE_ID = 8889;
        public const int API_VERSION = 1;
        private const string REQUEST_NAME = "AquaExpansionAPI";
        private static bool _initialized;
        private static bool _requestSent;
        private static bool _available;
        private static bool _unloading;
        private static string _ownerName;
        private static Dictionary<string, Delegate> _apiMethods;
        public static event Action OnAvailable;
        public static bool Available
        {
            get { return _available; }
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
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "Initialize failed: Utilities NULL.");

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
            MyAPIGateway.Utilities.RegisterMessageHandler(API_RESPONSE_ID,MessageHandler);
            MyLog.Default.WriteLine(
                "[AquaExpansion API] " +
                "Client initialized. Owner=" +
                _ownerName);
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
                MyAPIGateway.Utilities.SendModMessage(API_REQUEST_ID,REQUEST_NAME);
                _requestSent = true;

                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "Provider request sent.");
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "Request error:");
                MyLog.Default.WriteLine(e.ToString());
            }
        }
        // =====================================================
        // MESSAGE HANDLER
        // =====================================================
        private static void MessageHandler(object message)
        {
            if (_unloading)
                return;
            if (message == null)
                return;
            Dictionary<string, Delegate> methods = message as Dictionary<string, Delegate>;
            if (methods == null)
            {
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "Invalid provider response.");
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
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "Version check failed:");
                MyLog.Default.WriteLine(e.ToString());
                return;
            }
            if (!compatible)
            {
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "Incompatible API version.");
                return;
            }
            _available = true;
            MyLog.Default.WriteLine(
                "[AquaExpansion API] " +
                "Provider available.");
            try
            {
                Action callback = OnAvailable;
                if (callback != null)
                    callback();
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "OnAvailable error:");
                MyLog.Default.WriteLine(e.ToString());
            }
        }
        // =====================================================
        // GET METHOD
        // =====================================================
        private static T GetMethod<T>(string name) where T : class
        {
            if (_apiMethods == null)
                return null;
            Delegate method;

            if (!_apiMethods.TryGetValue(name,out method))
            {
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "Method not found: " +
                    name);
                return null;
            }
            return method as T;
        }
        // =====================================================
        // REGISTER AMMO
        // =====================================================
        public static bool RegisterAmmoProfile(HydroAmmoProfileData profile)
        {
            if (!_available)
                return false;
            if (profile == null)
                return false;
            if (string.IsNullOrWhiteSpace(profile.SubtypeId))
            {
                return false;
            }
            Func<Dictionary<string, object>, bool> method = GetMethod<Func<Dictionary<string, object>,bool>>("RegisterAmmoProfile");

            if (method == null)
                return false;
            Dictionary<string, object> data = profile.ToDictionary();
            // Owner is added automatically.
            data["Owner"] = _ownerName;
            try
            {
                return method(data);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "RegisterAmmoProfile error:");
                MyLog.Default.WriteLine(e.ToString());
                return false;
            }
        }
        // =====================================================
        // UNREGISTER AMMO
        // =====================================================
        public static bool UnregisterAmmoProfile(string subtypeId)
        {
            if (!_available)
                return false;
            if (string.IsNullOrWhiteSpace(subtypeId))
            {
                return false;
            }
            Func<Dictionary<string, object>, bool> method = GetMethod<Func<Dictionary<string, object>,bool>>("UnregisterAmmoProfile");
            if (method == null)
                return false;
            Dictionary<string, object> data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            data["SubtypeId"] = subtypeId.Trim();
            data["Owner"] = _ownerName;
            try
            {
                return method(data);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "UnregisterAmmoProfile error:");
                MyLog.Default.WriteLine(e.ToString());
                return false;
            }
        }
        // =====================================================
        // CHECK AMMO
        // =====================================================
        public static bool HasAmmoProfile(string subtypeId)
        {
            if (!_available)
                return false;
            if (string.IsNullOrWhiteSpace(subtypeId))
            {
                return false;
            }
            Func<string, bool> method = GetMethod<Func<string, bool>>("HasAmmoProfile");
            if (method == null)
                return false;
            try
            {
                return method(subtypeId.Trim());
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "HasAmmoProfile error:");
                MyLog.Default.WriteLine(e.ToString());
                return false;
            }
        }
        // =====================================================
        // GET OWNER
        // =====================================================
        public static string GetAmmoOwner(string subtypeId)
        {
            if (!_available)
                return string.Empty;
            if (string.IsNullOrWhiteSpace(subtypeId))
            {
                return string.Empty;
            }
            Func<string, string> method = GetMethod<Func<string, string>>("GetAmmoOwner");
            if (method == null)
                return string.Empty;
            try
            {
                return method(subtypeId.Trim());
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "GetAmmoOwner error:");
                MyLog.Default.WriteLine(e.ToString());
                return string.Empty;
            }
        }
        public static HydroAmmoProfileData GetAmmoProfile(string subtypeId)
        {
            if (!_available)
                return null;
            if (string.IsNullOrWhiteSpace(subtypeId))
                return null;
            Func<string, HydroAmmoProfileData> method = GetMethod<Func<string, HydroAmmoProfileData>>("GetAmmoProfile");
            if (method == null)
                return null;
            return method(subtypeId.Trim());
        }
        public static bool HasMuzzleBurst(string subtypeId)
        {
            if (!_available)
                return false;
            if (string.IsNullOrWhiteSpace(subtypeId))
            {
                return false;
            }
            Func<string, bool> method = GetMethod<Func<string, bool>>("HasMuzzleBurst");
            if (method == null)
                return false;
            try
            {
                return method(subtypeId.Trim());
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "HasMuzzleBurst error:");
                MyLog.Default.WriteLine(e.ToString());
                return false;
            }
        }
        public static string GetMuzzleBurstOwner(string subtypeId)
        {
            if (!_available)
                return string.Empty;
            if (string.IsNullOrWhiteSpace(subtypeId))
            {
                return string.Empty;
            }
            Func<string, string> method = GetMethod<Func<string, string>>("GetMuzzleBurstOwner");
            if (method == null)
                return string.Empty;
            try
            {
                return method(subtypeId.Trim());
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "GetMuzzleBurstOwner error:");
                MyLog.Default.WriteLine(e.ToString());
                return string.Empty;
            }
        }
        public static bool RegisterMuzzleBurst(string subtypeId,string effect)
        {
            if (!_available)
                return false;
            if (string.IsNullOrWhiteSpace(subtypeId) ||
                string.IsNullOrWhiteSpace(effect))
                return false;
            Func<Dictionary<string, object>, bool> method = GetMethod<Func<Dictionary<string, object>, bool>>("RegisterMuzzleBurst");
            if (method == null)
                return false;
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["SubtypeId"] = subtypeId;
            data["Effect"] = effect;
            // Owner is added automatically.
            data["Owner"] = _ownerName;
            try
            {
                return method(data);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "RegisterMuzzleBurst error:");
                MyLog.Default.WriteLine(e.ToString());
                return false;
            }
        }
        public static string GetWeaponEffectOwner(string subtypeId)
        {
            if (!_available)
                return string.Empty;
            if (string.IsNullOrWhiteSpace(subtypeId))
            {
                return string.Empty;
            }
            Func<string, string> method = GetMethod<Func<string, string>>("GetWeaponEffectOwner");
            if (method == null)
                return string.Empty;
            try
            {
                return method(subtypeId.Trim());
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "GetWeaponEffectOwner error:");
                MyLog.Default.WriteLine(e.ToString());
                return string.Empty;
            }
        }
        public static bool RegisterWeaponEffect(string subtypeId, string effect)
        {
            if (!_available)
                return false;
            if (string.IsNullOrWhiteSpace(subtypeId) ||
                string.IsNullOrWhiteSpace(effect))
                return false;
            Func<Dictionary<string, object>, bool> method = GetMethod<Func<Dictionary<string, object>, bool>>("RegisterWeaponEffect");
            if (method == null)
                return false;
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["SubtypeId"] = subtypeId;
            data["Effect"] = effect;
            // Owner is added automatically.
            data["Owner"] = _ownerName;
            try
            {
                return method(data);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "RegisterWeaponEffect error:");
                MyLog.Default.WriteLine(e.ToString());
                return false;
            }
        }
        // =====================================================
        // CLOSE
        // =====================================================
        public static void Close()
        {
            if (!_initialized)
                return;
            _unloading = true;
            MyLog.Default.WriteLine(
                "[AquaExpansion API] " +
                "Client unloading. Owner=" +
                _ownerName);
            try
            {
                if (MyAPIGateway.Utilities != null)
                {
                    MyAPIGateway.Utilities.UnregisterMessageHandler(API_RESPONSE_ID,MessageHandler);
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(
                    "[AquaExpansion API] " +
                    "Error unregistering:");
                MyLog.Default.WriteLine(e.ToString());
            }
            _apiMethods = null;
            _requestSent = false;
            _available = false;
            _initialized = false;
            _ownerName = null;
            OnAvailable = null;
            MyLog.Default.WriteLine(
                "[AquaExpansion API] " +
                "Client unloaded.");
        }
    }
    // =========================================================
    // AMMO DATA
    // =========================================================
    public class HydroAmmoProfileData
    {
        public string SubtypeId;
        public float Mass;
        public float DragCoefficient;
        public AquaSplashType SplashType;
        public float MinimumSpeed;
        public float MaxRange;
        public float WaterStability;
        public float EnergyLossCoefficient;
        public float ProjectileMassDamage;
        public float ProjectileHealthDamage;
        public float ExitVelocityMultiplier;
        public float ExitDamageMultiplier;
        public string TrailEffect;
        public float UnderwaterTurnMultiplier;
        public float EngineAcceleration;
        public float UnderwaterEngineMultiplier;
        public int Torpedo;
        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "SubtypeId", SubtypeId },
                { "Mass", Mass },
                { "DragCoefficient",DragCoefficient },
                { "SplashType",(int)SplashType },
                { "MinimumSpeed",MinimumSpeed },
                { "MaxRange",MaxRange },
                { "WaterStability",WaterStability },
                { "EnergyLossCoefficient",EnergyLossCoefficient },
                { "ProjectileMassDamage",ProjectileMassDamage },
                { "ProjectileHealthDamage",ProjectileHealthDamage },
                { "ExitVelocityMultiplier",ExitVelocityMultiplier },
                { "ExitDamageMultiplier",ExitDamageMultiplier },
                { "TrailEffect",TrailEffect },
                { "UnderwaterTurnMultiplier",UnderwaterTurnMultiplier },
                { "EngineAcceleration",EngineAcceleration },
                { "UnderwaterEngineMultiplier",UnderwaterEngineMultiplier },
                { "Torpedo",Torpedo }
            };
        }
    }
    // =========================================================
    // SPLASH TYPE
    // =========================================================
    public enum AquaSplashType
    {
        Bullet = 0,
        Missile = 1,
        Exit = 2,
        Shell = 3,
        Railgun = 4
    }
}
