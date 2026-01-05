using System;
using System.Collections;
using System.Reflection;
using UnityExplorer.Config;
using UniverseLib.Input;

namespace UnityExplorer.UI
{
    public static class DisplayManager
    {
        public static int ActiveDisplayIndex { get; private set; }
        public static Display ActiveDisplay => Display.displays[ActiveDisplayIndex];

        public static int Width => ActiveDisplay.renderingWidth;
        public static int Height => ActiveDisplay.renderingHeight;

        public static Vector3 MousePosition => Application.isEditor
            ? IInputManager.MousePosition
            : Display.RelativeMouseAt(IInputManager.MousePosition);

        public static bool MouseInTargetDisplay => MousePosition.z == ActiveDisplayIndex;

        private static Camera canvasCamera;

        internal static void Init()
        {
            SetDisplay(ConfigManager.Target_Display.Value);
            ConfigManager.Target_Display.OnValueChanged += SetDisplay;
        }

        public static void SetDisplay(int display)
        {
            if (ActiveDisplayIndex == display)
                return;

            if (Display.displays.Length <= display)
            {
                ExplorerCore.LogWarning($"Cannot set display index to {display} as there are not enough monitors connected!");

                if (ConfigManager.Target_Display.Value == display)
                    ConfigManager.Target_Display.Value = 0;

                return;
            }

            ActiveDisplayIndex = display;
            ActiveDisplay.Activate();

            UIManager.UICanvas.targetDisplay = display;

            // ensure a camera is targeting the display
            if (!Camera.main || Camera.main.targetDisplay != display)
            {
                if (!canvasCamera)
                {
                    canvasCamera = new GameObject("UnityExplorer_CanvasCamera").AddComponent<Camera>();
                    GameObject.DontDestroyOnLoad(canvasCamera.gameObject);
                    canvasCamera.hideFlags = HideFlags.HideAndDontSave;

                    // Disable VR rendering on this camera to prevent interference with VR games
                    SetCameraToNonVR(canvasCamera);
                }
                canvasCamera.targetDisplay = display;
            }

            RuntimeHelper.StartCoroutine(FixPanels());
        }

        private static IEnumerator FixPanels()
        {
            yield return null;
            yield return null;

            foreach (Panels.UEPanel panel in UIManager.UIPanels.Values)
            {
                panel.EnsureValidSize();
                panel.EnsureValidPosition();
                panel.Dragger.OnEndResize();
            }
        }

        // Reflection helpers for stereoTargetEye (VR isolation)
        private static PropertyInfo _stereoTargetEyeProp;
        private static object _stereoTargetEyeNone;
        private static bool _stereoReflectionInitialized;

        /// <summary>
        /// Set a camera to not render to VR (stereoTargetEye = None).
        /// Uses reflection for compatibility with different Unity versions.
        /// </summary>
        private static void SetCameraToNonVR(Camera cam)
        {
            if (!_stereoReflectionInitialized)
            {
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
    }
}
