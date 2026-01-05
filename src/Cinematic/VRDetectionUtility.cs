using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UniverseLib;

namespace UnityExplorer.Cinematic
{
    /// <summary>
    /// Utility class for detecting VR mode and accessing HMD pose data.
    /// Supports both legacy UnityEngine.VR (Unity 5.x-2017.1) and modern UnityEngine.XR (2017.2+).
    /// </summary>
    public static class VRDetectionUtility
    {
        public enum VRAPIType
        {
            None,
            LegacyVR,
            ModernXR
        }

        // Detection state
        private static bool _initialized = false;
        private static VRAPIType _detectedAPI = VRAPIType.None;

        // Cached reflection objects - Modern XR
        private static Type _xrSettingsType;
        private static PropertyInfo _xrEnabledProp;
        private static Type _inputTrackingType;
        private static MethodInfo _getNodeStatesMethod;
        private static Type _xrNodeStateType;
        private static MethodInfo _tryGetPositionMethod;
        private static MethodInfo _tryGetRotationMethod;
        private static PropertyInfo _nodeTypeProperty;

        // Cached reflection objects - Legacy VR
        private static Type _vrSettingsType;
        private static PropertyInfo _vrEnabledProp;
        private static Type _legacyInputTrackingType;
        private static MethodInfo _getLocalPositionMethod;
        private static MethodInfo _getLocalRotationMethod;

        // XRNode/VRNode enum values for Head
        private static object _xrNodeHead;
        private static object _vrNodeHead;

        public static VRAPIType DetectedAPI => _detectedAPI;

        /// <summary>
        /// Initialize VR detection. Must be called before using other methods.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            // Try modern XR first (Unity 2017.2+)
            if (TryInitializeModernXR())
            {
                _detectedAPI = VRAPIType.ModernXR;
                ExplorerCore.Log("VR Detection: Modern XR API detected");
                return;
            }

            // Fallback to legacy VR (Unity 5.x - 2017.1)
            if (TryInitializeLegacyVR())
            {
                _detectedAPI = VRAPIType.LegacyVR;
                ExplorerCore.Log("VR Detection: Legacy VR API detected");
                return;
            }

            _detectedAPI = VRAPIType.None;
            ExplorerCore.Log("VR Detection: No VR API found");
        }

        private static bool TryInitializeModernXR()
        {
            try
            {
                // Try to find XRSettings
                _xrSettingsType = ReflectionUtility.GetTypeByName("UnityEngine.XR.XRSettings");
                if (_xrSettingsType == null)
                    return false;

                _xrEnabledProp = _xrSettingsType.GetProperty("enabled", BindingFlags.Public | BindingFlags.Static);
                if (_xrEnabledProp == null)
                    return false;

                // Find InputTracking for pose data
                _inputTrackingType = ReflectionUtility.GetTypeByName("UnityEngine.XR.InputTracking");
                if (_inputTrackingType == null)
                    return false;

                // Find XRNodeState type and methods
                _xrNodeStateType = ReflectionUtility.GetTypeByName("UnityEngine.XR.XRNodeState");
                if (_xrNodeStateType == null)
                    return false;

                // Get the GetNodeStates method
                Type listType = typeof(List<>).MakeGenericType(_xrNodeStateType);
                _getNodeStatesMethod = _inputTrackingType.GetMethod("GetNodeStates", new Type[] { listType });

                // Get TryGetPosition and TryGetRotation from XRNodeState
                _tryGetPositionMethod = _xrNodeStateType.GetMethod("TryGetPosition");
                _tryGetRotationMethod = _xrNodeStateType.GetMethod("TryGetRotation");
                _nodeTypeProperty = _xrNodeStateType.GetProperty("nodeType");

                // Get XRNode.Head enum value
                Type xrNodeType = ReflectionUtility.GetTypeByName("UnityEngine.XR.XRNode");
                if (xrNodeType != null)
                {
                    _xrNodeHead = Enum.Parse(xrNodeType, "Head");
                }

                return true;
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Failed to initialize Modern XR: {ex.Message}");
                return false;
            }
        }

        private static bool TryInitializeLegacyVR()
        {
            try
            {
                // Try to find VRSettings (legacy namespace)
                _vrSettingsType = ReflectionUtility.GetTypeByName("UnityEngine.VR.VRSettings");
                if (_vrSettingsType == null)
                    return false;

                _vrEnabledProp = _vrSettingsType.GetProperty("enabled", BindingFlags.Public | BindingFlags.Static);
                if (_vrEnabledProp == null)
                    return false;

                // Find InputTracking for pose data (legacy namespace)
                _legacyInputTrackingType = ReflectionUtility.GetTypeByName("UnityEngine.VR.InputTracking");
                if (_legacyInputTrackingType == null)
                    return false;

                // Get VRNode.Head enum value
                Type vrNodeType = ReflectionUtility.GetTypeByName("UnityEngine.VR.VRNode");
                if (vrNodeType != null)
                {
                    _vrNodeHead = Enum.Parse(vrNodeType, "Head");
                }

                // Get GetLocalPosition and GetLocalRotation methods
                _getLocalPositionMethod = _legacyInputTrackingType.GetMethod("GetLocalPosition", new Type[] { vrNodeType });
                _getLocalRotationMethod = _legacyInputTrackingType.GetMethod("GetLocalRotation", new Type[] { vrNodeType });

                return _getLocalPositionMethod != null && _getLocalRotationMethod != null;
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Failed to initialize Legacy VR: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if VR is currently enabled and active.
        /// </summary>
        public static bool IsVREnabled()
        {
            if (!_initialized)
                Initialize();

            if (_detectedAPI == VRAPIType.None)
                return false;

            try
            {
                if (_detectedAPI == VRAPIType.ModernXR && _xrEnabledProp != null)
                {
                    return (bool)_xrEnabledProp.GetValue(null, null);
                }
                else if (_detectedAPI == VRAPIType.LegacyVR && _vrEnabledProp != null)
                {
                    return (bool)_vrEnabledProp.GetValue(null, null);
                }
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Failed to check VR enabled state: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Try to get the current HMD (head-mounted display) pose.
        /// </summary>
        /// <param name="position">The HMD position in tracking space</param>
        /// <param name="rotation">The HMD rotation</param>
        /// <returns>True if pose was successfully retrieved</returns>
        public static bool TryGetHMDPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (!_initialized)
                Initialize();

            if (_detectedAPI == VRAPIType.None || !IsVREnabled())
                return false;

            try
            {
                if (_detectedAPI == VRAPIType.ModernXR)
                {
                    return TryGetHMDPoseModernXR(out position, out rotation);
                }
                else if (_detectedAPI == VRAPIType.LegacyVR)
                {
                    return TryGetHMDPoseLegacyVR(out position, out rotation);
                }
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Failed to get HMD pose: {ex.Message}");
            }

            return false;
        }

        private static bool TryGetHMDPoseModernXR(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            try
            {
                // Create a list to hold node states
                Type listType = typeof(List<>).MakeGenericType(_xrNodeStateType);
                IList nodeStates = (IList)Activator.CreateInstance(listType);

                // Call InputTracking.GetNodeStates(nodeStates)
                _getNodeStatesMethod.Invoke(null, new object[] { nodeStates });

                // Find the Head node
                foreach (object state in nodeStates)
                {
                    object nodeType = _nodeTypeProperty.GetValue(state, null);
                    if (nodeType.Equals(_xrNodeHead))
                    {
                        // TryGetPosition(out Vector3 position)
                        object[] posArgs = new object[] { Vector3.zero };
                        bool posSuccess = (bool)_tryGetPositionMethod.Invoke(state, posArgs);
                        if (posSuccess)
                            position = (Vector3)posArgs[0];

                        // TryGetRotation(out Quaternion rotation)
                        object[] rotArgs = new object[] { Quaternion.identity };
                        bool rotSuccess = (bool)_tryGetRotationMethod.Invoke(state, rotArgs);
                        if (rotSuccess)
                            rotation = (Quaternion)rotArgs[0];

                        return posSuccess && rotSuccess;
                    }
                }
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Failed to get HMD pose (Modern XR): {ex.Message}");
            }

            return false;
        }

        private static bool TryGetHMDPoseLegacyVR(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            try
            {
                if (_getLocalPositionMethod != null && _vrNodeHead != null)
                {
                    position = (Vector3)_getLocalPositionMethod.Invoke(null, new object[] { _vrNodeHead });
                }

                if (_getLocalRotationMethod != null && _vrNodeHead != null)
                {
                    rotation = (Quaternion)_getLocalRotationMethod.Invoke(null, new object[] { _vrNodeHead });
                }

                return true;
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Failed to get HMD pose (Legacy VR): {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Get a user-friendly status string describing the current VR state.
        /// </summary>
        public static string GetStatusString()
        {
            if (!_initialized)
                Initialize();

            if (_detectedAPI == VRAPIType.None)
                return "No VR API detected";

            bool enabled = IsVREnabled();
            string apiName = _detectedAPI == VRAPIType.ModernXR ? "XR" : "VR (Legacy)";

            return enabled ? $"{apiName}: Active" : $"{apiName}: Inactive";
        }
    }
}
