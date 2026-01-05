using UnityEngine;
using UnityEngine.UI;
using UnityExplorer.Cinematic;
using UnityExplorer.Config;
using UniverseLib.UI;
using UniverseLib.UI.Models;

namespace UnityExplorer.UI.Panels
{
    /// <summary>
    /// Panel for controlling the VR Spectator smooth camera feature.
    /// Provides a smoothed, larger FOV desktop view for VR games.
    /// </summary>
    public class VRSpectatorPanel : UEPanel
    {
        public VRSpectatorPanel(UIBase owner) : base(owner) { }

        public override string Name => "VR Spectator";
        public override UIManager.Panels PanelType => UIManager.Panels.VRSpectator;
        public override int MinWidth => 400;
        public override int MinHeight => 350;
        public override Vector2 DefaultAnchorMin => new(0.4f, 0.4f);
        public override Vector2 DefaultAnchorMax => new(0.6f, 0.6f);
        public override bool NavButtonWanted => true;
        public override bool ShouldSaveActiveState => true;

        // State
        public static bool IsSpectatorActive { get; private set; }
        public static Camera SpectatorCamera { get; private set; }
        private static VRSpectatorBehaviour _spectatorBehaviour;

        // UI elements
        private static ButtonRef _startStopButton;
        private static Text _vrStatusLabel;
        private static Slider _positionSmoothSlider;
        private static Slider _rotationSmoothSlider;
        private static Slider _fovSlider;
        private static InputFieldRef _positionSmoothInput;
        private static InputFieldRef _rotationSmoothInput;
        private static InputFieldRef _fovInput;

        protected override void ConstructPanelContent()
        {
            // Initialize VR detection
            VRDetectionUtility.Initialize();

            // VR Status indicator
            _vrStatusLabel = UIFactory.CreateLabel(ContentRoot, "VRStatus", $"VR Status: {VRDetectionUtility.GetStatusString()}");
            UIFactory.SetLayoutElement(_vrStatusLabel.gameObject, minWidth: 200, minHeight: 25);

            AddSpacer(5);

            // Start/Stop button
            _startStopButton = UIFactory.CreateButton(ContentRoot, "ToggleButton", "Start VR Spectator");
            UIFactory.SetLayoutElement(_startStopButton.GameObject, minWidth: 150, minHeight: 25, flexibleWidth: 9999);
            _startStopButton.OnClick += ToggleSpectator;
            UpdateToggleButtonState();

            AddSpacer(10);

            // Position Smoothing
            GameObject posSmoothRow = UIFactory.CreateHorizontalGroup(ContentRoot, "PosSmoothRow", false, false, true, true, 3, default, new(1, 1, 1, 0));

            Text posSmoothLabel = UIFactory.CreateLabel(posSmoothRow, "Label", "Position Smooth:");
            UIFactory.SetLayoutElement(posSmoothLabel.gameObject, minWidth: 110, minHeight: 25);

            _positionSmoothInput = UIFactory.CreateInputField(posSmoothRow, "Input", "0.1");
            UIFactory.SetLayoutElement(_positionSmoothInput.GameObject, minWidth: 60, minHeight: 25);
            _positionSmoothInput.Text = ConfigManager.VR_Spectator_Position_Smooth.Value.ToString("F2");
            _positionSmoothInput.Component.GetOnEndEdit().AddListener(OnPositionSmoothInputChanged);

            GameObject posSmoothSliderObj = UIFactory.CreateSlider(posSmoothRow, "Slider", out _positionSmoothSlider);
            UIFactory.SetLayoutElement(posSmoothSliderObj, minHeight: 25, minWidth: 150, flexibleWidth: 9999);
            _positionSmoothSlider.minValue = 0.01f;
            _positionSmoothSlider.maxValue = 0.5f;
            _positionSmoothSlider.value = ConfigManager.VR_Spectator_Position_Smooth.Value;
            _positionSmoothSlider.onValueChanged.AddListener(OnPositionSmoothSliderChanged);

            AddSpacer(5);

            // Rotation Smoothing
            GameObject rotSmoothRow = UIFactory.CreateHorizontalGroup(ContentRoot, "RotSmoothRow", false, false, true, true, 3, default, new(1, 1, 1, 0));

            Text rotSmoothLabel = UIFactory.CreateLabel(rotSmoothRow, "Label", "Rotation Smooth:");
            UIFactory.SetLayoutElement(rotSmoothLabel.gameObject, minWidth: 110, minHeight: 25);

            _rotationSmoothInput = UIFactory.CreateInputField(rotSmoothRow, "Input", "0.08");
            UIFactory.SetLayoutElement(_rotationSmoothInput.GameObject, minWidth: 60, minHeight: 25);
            _rotationSmoothInput.Text = ConfigManager.VR_Spectator_Rotation_Smooth.Value.ToString("F2");
            _rotationSmoothInput.Component.GetOnEndEdit().AddListener(OnRotationSmoothInputChanged);

            GameObject rotSmoothSliderObj = UIFactory.CreateSlider(rotSmoothRow, "Slider", out _rotationSmoothSlider);
            UIFactory.SetLayoutElement(rotSmoothSliderObj, minHeight: 25, minWidth: 150, flexibleWidth: 9999);
            _rotationSmoothSlider.minValue = 0.01f;
            _rotationSmoothSlider.maxValue = 0.5f;
            _rotationSmoothSlider.value = ConfigManager.VR_Spectator_Rotation_Smooth.Value;
            _rotationSmoothSlider.onValueChanged.AddListener(OnRotationSmoothSliderChanged);

            AddSpacer(5);

            // FOV
            GameObject fovRow = UIFactory.CreateHorizontalGroup(ContentRoot, "FOVRow", false, false, true, true, 3, default, new(1, 1, 1, 0));

            Text fovLabel = UIFactory.CreateLabel(fovRow, "Label", "Spectator FOV:");
            UIFactory.SetLayoutElement(fovLabel.gameObject, minWidth: 110, minHeight: 25);

            _fovInput = UIFactory.CreateInputField(fovRow, "Input", "90");
            UIFactory.SetLayoutElement(_fovInput.GameObject, minWidth: 60, minHeight: 25);
            _fovInput.Text = ConfigManager.VR_Spectator_FOV.Value.ToString("F0");
            _fovInput.Component.GetOnEndEdit().AddListener(OnFOVInputChanged);

            GameObject fovSliderObj = UIFactory.CreateSlider(fovRow, "Slider", out _fovSlider);
            UIFactory.SetLayoutElement(fovSliderObj, minHeight: 25, minWidth: 150, flexibleWidth: 9999);
            _fovSlider.minValue = 60f;
            _fovSlider.maxValue = 120f;
            _fovSlider.value = ConfigManager.VR_Spectator_FOV.Value;
            _fovSlider.onValueChanged.AddListener(OnFOVSliderChanged);

            AddSpacer(10);

            // Refresh VR Status button
            ButtonRef refreshButton = UIFactory.CreateButton(ContentRoot, "RefreshButton", "Refresh VR Status");
            UIFactory.SetLayoutElement(refreshButton.GameObject, minWidth: 150, minHeight: 25, flexibleWidth: 9999);
            refreshButton.OnClick += RefreshVRStatus;

            AddSpacer(10);

            // Instructions
            string instructions =
                "VR Spectator Camera\n\n" +
                "Creates a smoothed desktop view that follows the VR headset.\n\n" +
                "- Lower smoothing = more responsive but jerkier\n" +
                "- Higher smoothing = smoother but more delayed\n" +
                "- Wider FOV gives better context for spectators\n\n" +
                $"Toggle Hotkey: {ConfigManager.VR_Spectator_Toggle.Value}";

            Text instructionsText = UIFactory.CreateLabel(ContentRoot, "Instructions", instructions, TextAnchor.UpperLeft);
            UIFactory.SetLayoutElement(instructionsText.gameObject, minWidth: 350, minHeight: 150, flexibleWidth: 9999);
        }

        private void AddSpacer(int height)
        {
            GameObject spacer = UIFactory.CreateUIObject("Spacer", ContentRoot);
            UIFactory.SetLayoutElement(spacer, minHeight: height, flexibleHeight: 0);
        }

        // ~~~~~~~~ Spectator Control ~~~~~~~~

        public static void ToggleSpectator()
        {
            if (IsSpectatorActive)
                EndSpectator();
            else
                BeginSpectator();
        }

        public static void BeginSpectator()
        {
            if (IsSpectatorActive)
                return;

            // Check if VR is available
            if (!VRDetectionUtility.IsVREnabled())
            {
                ExplorerCore.LogWarning("VR Spectator: VR is not enabled. Cannot start spectator camera.");
                return;
            }

            CreateSpectatorCamera();
            IsSpectatorActive = true;
            UpdateToggleButtonState();

            ExplorerCore.Log("VR Spectator: Started");
        }

        public static void EndSpectator()
        {
            if (!IsSpectatorActive)
                return;

            DestroySpectatorCamera();
            IsSpectatorActive = false;
            UpdateToggleButtonState();

            ExplorerCore.Log("VR Spectator: Stopped");
        }

        private static void CreateSpectatorCamera()
        {
            // Create a new camera for desktop output
            GameObject camObj = new GameObject("CUE VR Spectator Camera");
            GameObject.DontDestroyOnLoad(camObj);
            camObj.hideFlags = HideFlags.HideAndDontSave;

            SpectatorCamera = camObj.AddComponent<Camera>();

            // Configure for desktop-only rendering (not VR)
            VRSpectatorBehaviour.SetCameraToMono(SpectatorCamera);
            SpectatorCamera.fieldOfView = ConfigManager.VR_Spectator_FOV.Value;
            SpectatorCamera.depth = 100; // Render on top

            // Copy rendering settings from main VR camera if available
            Camera mainVRCam = Camera.main;
            if (mainVRCam != null)
            {
                SpectatorCamera.clearFlags = mainVRCam.clearFlags;
                SpectatorCamera.backgroundColor = mainVRCam.backgroundColor;
                SpectatorCamera.nearClipPlane = mainVRCam.nearClipPlane;
                SpectatorCamera.farClipPlane = mainVRCam.farClipPlane;
                SpectatorCamera.cullingMask = mainVRCam.cullingMask;
            }

            // Add spectator behaviour
            _spectatorBehaviour = camObj.AddComponent<VRSpectatorBehaviour>();
            _spectatorBehaviour.Initialize(SpectatorCamera);
            _spectatorBehaviour.PositionSmoothTime = ConfigManager.VR_Spectator_Position_Smooth.Value;
            _spectatorBehaviour.RotationSmoothTime = ConfigManager.VR_Spectator_Rotation_Smooth.Value;
            _spectatorBehaviour.SpectatorFOV = ConfigManager.VR_Spectator_FOV.Value;
            _spectatorBehaviour.IsActive = true;
        }

        private static void DestroySpectatorCamera()
        {
            if (_spectatorBehaviour != null)
            {
                _spectatorBehaviour.IsActive = false;
                _spectatorBehaviour = null;
            }

            if (SpectatorCamera != null)
            {
                GameObject.Destroy(SpectatorCamera.gameObject);
                SpectatorCamera = null;
            }
        }

        private static void UpdateToggleButtonState()
        {
            if (_startStopButton == null)
                return;

            if (IsSpectatorActive)
            {
                _startStopButton.ButtonText.text = "Stop VR Spectator";
                RuntimeHelper.SetColorBlock(_startStopButton.Component, new Color(0.4f, 0.2f, 0.2f));
            }
            else
            {
                _startStopButton.ButtonText.text = "Start VR Spectator";
                RuntimeHelper.SetColorBlock(_startStopButton.Component, new Color(0.2f, 0.4f, 0.2f));
            }
        }

        private void RefreshVRStatus()
        {
            // Re-initialize VR detection
            VRDetectionUtility.Initialize();
            _vrStatusLabel.text = $"VR Status: {VRDetectionUtility.GetStatusString()}";

            // Also refresh the spectator camera's VR camera reference if active
            if (_spectatorBehaviour != null)
            {
                _spectatorBehaviour.FindVRCamera();
            }
        }

        // ~~~~~~~~ UI Callbacks ~~~~~~~~

        private void OnPositionSmoothSliderChanged(float value)
        {
            ConfigManager.VR_Spectator_Position_Smooth.Value = value;
            _positionSmoothInput.Text = value.ToString("F2");

            if (_spectatorBehaviour != null)
                _spectatorBehaviour.PositionSmoothTime = value;
        }

        private void OnPositionSmoothInputChanged(string value)
        {
            if (float.TryParse(value, out float result))
            {
                result = Mathf.Clamp(result, 0.01f, 0.5f);
                ConfigManager.VR_Spectator_Position_Smooth.Value = result;
                _positionSmoothSlider.value = result;

                if (_spectatorBehaviour != null)
                    _spectatorBehaviour.PositionSmoothTime = result;
            }
        }

        private void OnRotationSmoothSliderChanged(float value)
        {
            ConfigManager.VR_Spectator_Rotation_Smooth.Value = value;
            _rotationSmoothInput.Text = value.ToString("F2");

            if (_spectatorBehaviour != null)
                _spectatorBehaviour.RotationSmoothTime = value;
        }

        private void OnRotationSmoothInputChanged(string value)
        {
            if (float.TryParse(value, out float result))
            {
                result = Mathf.Clamp(result, 0.01f, 0.5f);
                ConfigManager.VR_Spectator_Rotation_Smooth.Value = result;
                _rotationSmoothSlider.value = result;

                if (_spectatorBehaviour != null)
                    _spectatorBehaviour.RotationSmoothTime = result;
            }
        }

        private void OnFOVSliderChanged(float value)
        {
            ConfigManager.VR_Spectator_FOV.Value = value;
            _fovInput.Text = value.ToString("F0");

            if (_spectatorBehaviour != null)
                _spectatorBehaviour.SpectatorFOV = value;

            if (SpectatorCamera != null)
                SpectatorCamera.fieldOfView = value;
        }

        private void OnFOVInputChanged(string value)
        {
            if (float.TryParse(value, out float result))
            {
                result = Mathf.Clamp(result, 60f, 120f);
                ConfigManager.VR_Spectator_FOV.Value = result;
                _fovSlider.value = result;

                if (_spectatorBehaviour != null)
                    _spectatorBehaviour.SpectatorFOV = result;

                if (SpectatorCamera != null)
                    SpectatorCamera.fieldOfView = result;
            }
        }
    }
}
