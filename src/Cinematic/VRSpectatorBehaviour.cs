using System;
using System.Reflection;
using UnityEngine;
using UnityExplorer.Config;
#if CPP
#if UNHOLLOWER
using UnhollowerRuntimeLib;
#endif
#if INTEROP
using Il2CppInterop.Runtime.Injection;
#endif
#endif

namespace UnityExplorer.Cinematic
{
    /// <summary>
    /// MonoBehaviour that provides smooth camera movement for VR spectator view.
    /// Follows the HMD position/rotation with configurable smoothing.
    /// </summary>
    internal class VRSpectatorBehaviour : MonoBehaviour
    {
#if CPP
        static VRSpectatorBehaviour()
        {
            ClassInjector.RegisterTypeInIl2Cpp<VRSpectatorBehaviour>();
        }

        public VRSpectatorBehaviour(IntPtr ptr) : base(ptr) { }
#endif

        // Smoothing state
        private Vector3 _smoothedPosition;
        private Quaternion _smoothedRotation;
        private Vector3 _positionVelocity;
        private bool _initialized = false;

        // Reference to the VR camera we're tracking
        private Camera _vrCamera;

        // Configuration
        public float PositionSmoothTime { get; set; } = 0.1f;
        public float RotationSmoothTime { get; set; } = 0.08f;
        public float SpectatorFOV { get; set; } = 90f;
        public bool IsActive { get; set; } = false;

        // The spectator camera we're controlling
        private Camera _spectatorCamera;

        /// <summary>
        /// Initialize the behaviour with the spectator camera.
        /// </summary>
        public void Initialize(Camera spectatorCamera)
        {
            _spectatorCamera = spectatorCamera;
            _initialized = false;
            FindVRCamera();
        }

        /// <summary>
        /// Try to find the active VR camera in the scene.
        /// </summary>
        public void FindVRCamera()
        {
            // Try Camera.main first
            Camera mainCam = Camera.main;
            if (mainCam != null && mainCam != _spectatorCamera)
            {
                _vrCamera = mainCam;
                return;
            }

            // Search for any camera that's rendering to VR
            foreach (Camera cam in Camera.allCameras)
            {
                if (cam != _spectatorCamera && IsVRCamera(cam))
                {
                    _vrCamera = cam;
                    return;
                }
            }

            // Fallback: use any camera that's not our spectator camera
            foreach (Camera cam in Camera.allCameras)
            {
                if (cam != _spectatorCamera)
                {
                    _vrCamera = cam;
                    return;
                }
            }
        }

        private void LateUpdate()
        {
            if (!IsActive || _spectatorCamera == null)
                return;

            UpdateSmoothing();
        }

        /// <summary>
        /// Update the smoothed camera position and rotation.
        /// </summary>
        private void UpdateSmoothing()
        {
            // Try to get pose from HMD tracking first
            bool gotPose = VRDetectionUtility.TryGetHMDPose(out Vector3 hmdLocalPos, out Quaternion hmdLocalRot);

            Vector3 targetPosition;
            Quaternion targetRotation;

            if (gotPose && _vrCamera != null)
            {
                // Convert HMD local pose to world space using VR camera as reference
                Transform vrCamParent = _vrCamera.transform.parent;
                if (vrCamParent != null)
                {
                    targetPosition = vrCamParent.TransformPoint(hmdLocalPos);
                    targetRotation = vrCamParent.rotation * hmdLocalRot;
                }
                else
                {
                    targetPosition = hmdLocalPos;
                    targetRotation = hmdLocalRot;
                }
            }
            else if (_vrCamera != null)
            {
                // Fallback: just track the VR camera's transform directly
                targetPosition = _vrCamera.transform.position;
                targetRotation = _vrCamera.transform.rotation;
            }
            else
            {
                // No VR camera found, try to find one
                FindVRCamera();
                return;
            }

            // Initialize smoothed values on first frame
            if (!_initialized)
            {
                _smoothedPosition = targetPosition;
                _smoothedRotation = targetRotation;
                _positionVelocity = Vector3.zero;
                _initialized = true;
            }

            // Use unscaled delta time so it works when game is paused
            float deltaTime = Time.unscaledDeltaTime;

            // Position smoothing with SmoothDamp
            _smoothedPosition = Vector3.SmoothDamp(
                _smoothedPosition,
                targetPosition,
                ref _positionVelocity,
                PositionSmoothTime,
                Mathf.Infinity,
                deltaTime
            );

            // Rotation smoothing with exponential Slerp
            float rotT = 1f - Mathf.Exp(-deltaTime / Mathf.Max(RotationSmoothTime, 0.001f));
            _smoothedRotation = Quaternion.Slerp(_smoothedRotation, targetRotation, rotT);

            // Apply to spectator camera
            _spectatorCamera.transform.position = _smoothedPosition;
            _spectatorCamera.transform.rotation = _smoothedRotation;
            _spectatorCamera.fieldOfView = SpectatorFOV;
        }

        /// <summary>
        /// Reset the smoothed state to immediately match the current HMD pose.
        /// </summary>
        public void ResetSmoothing()
        {
            _initialized = false;
        }

        /// <summary>
        /// Update settings from config.
        /// </summary>
        public void UpdateFromConfig()
        {
            PositionSmoothTime = ConfigManager.VR_Spectator_Position_Smooth.Value;
            RotationSmoothTime = ConfigManager.VR_Spectator_Rotation_Smooth.Value;
            SpectatorFOV = ConfigManager.VR_Spectator_FOV.Value;
        }

        // Reflection helpers for stereoTargetEye
        private static PropertyInfo _stereoTargetEyeProp;
        private static object _stereoTargetEyeNone;
        private static bool _stereoReflectionInitialized;

        /// <summary>
        /// Check if a camera is rendering to VR (stereoTargetEye != None).
        /// </summary>
        private static bool IsVRCamera(Camera cam)
        {
            InitStereoReflection();
            if (_stereoTargetEyeProp == null)
                return false;

            try
            {
                object stereoValue = _stereoTargetEyeProp.GetValue(cam, null);
                return !stereoValue.Equals(_stereoTargetEyeNone);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Set a camera to render mono (desktop only, not VR).
        /// </summary>
        public static void SetCameraToMono(Camera cam)
        {
            InitStereoReflection();
            if (_stereoTargetEyeProp == null || _stereoTargetEyeNone == null)
                return;

            try
            {
                _stereoTargetEyeProp.SetValue(cam, _stereoTargetEyeNone, null);
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Failed to set stereoTargetEye: {ex.Message}");
            }
        }

        private static void InitStereoReflection()
        {
            if (_stereoReflectionInitialized)
                return;

            _stereoReflectionInitialized = true;

            try
            {
                _stereoTargetEyeProp = typeof(Camera).GetProperty("stereoTargetEye");
                if (_stereoTargetEyeProp != null)
                {
                    Type stereoMaskType = _stereoTargetEyeProp.PropertyType;
                    _stereoTargetEyeNone = Enum.Parse(stereoMaskType, "None");
                }
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Failed to initialize stereo reflection: {ex.Message}");
            }
        }
    }
}
