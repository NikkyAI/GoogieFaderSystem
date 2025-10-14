using System;
using Texel;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;
using VRC;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Common;
using VRC.Udon.Common.Enums;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InvertIf
// ReSharper disable CompareOfFloatsByEqualityOperator
namespace GoogieFaderSystem
{
    public enum ShaderValueType
    {
        Float,
        Vector4
    };

    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class ShaderFader : EventBase
    {
        public ShaderValueType valueType = ShaderValueType.Float;
        public int vectorIndex = 0;
        [SerializeField] public Material[] targetMaterials;
        [SerializeField] public string materialPropertyId;

        [Tooltip(("Value for reset"))]
        [SerializeField]
        public float defaultValue = 0; // Value for reset

        [Tooltip(("minimum value"))]
        [SerializeField]
        public float valueMin = 0; // Default minimum value

        [Tooltip(("maximum value"))]
        [SerializeField]
        public float valueMax = 1; // Default maximum value
        
        [Header("Value Smoothing")] // header
        [Tooltip(("smoothes out value updating, but can lower frames"))]
        public bool enableValueSmoothing = false;
        [Tooltip("amount of frames to skip when smoothing the value, higher number == less load, but more choppy smoothing")]
        public int smoothUpdateInterval = 5;
        [Tooltip("rate at with the value moves towards new target a fixed duration")]
        public float smoothingRate = 0.15f;
        
        [Header("Display & Labels")] // header
        [SerializeField] public bool alwaysShowValue = false;
        [Tooltip("What the slider value will be formated as.\n- 0.0 means it will always at least show one digit with one decimal point\n- 00 means it will fill always be formated as two digits with no decimal point")]
        [SerializeField] private string valueDisplayFormat = "0.0";
        [SerializeField] public string labelMain = "MAIN";
        [SerializeField] public string labelSub = "SUB";

        // [SerializeField]
        // [Tooltip("divides the vertical look distance by this number")]
        // [SerializeField] 
        private float _desktopDampening = 20;

        [Header("Internals")] // header
        [Tooltip("ACL used to check who can use the fader")]
        [SerializeField]
        public AccessControl accessControl;

        [SerializeField] public GameObject leftHandCollider;
        [SerializeField] public GameObject rightHandCollider;

        [SerializeField] MeshRenderer handleRenderer;

        [SerializeField] GameObject leftLimiter;
        [SerializeField] GameObject rightLimiter;

        [Header("UI")] // header
        [SerializeField] private TextMeshPro tmpLabelMain;

        [SerializeField] private TextMeshPro tmpLabelSub;
        [SerializeField] private TextMeshPro tmpLabelValue;

        [Header("External")] // header
        [SerializeField] private UdonBehaviour externalBehaviour;
        [SerializeField] private string externalFloat="";
        [SerializeField] private string externalEvent="";
        
        [Header("Debug")] // header
        [SerializeField] public DebugLog debugLog;
        // [SerializeField] private DebugState debugState;

        [Header("State")] // header
        [UdonSynced] // IMPORTANT, DO NOT DELETE
        public float currentValue;
        private Vector3 _sliderPosition;

        private bool _isInitialized = false;
        private int _uid = 0;
        private bool _rightGrabbed;
        private bool _leftGrabbed;
        private bool _inLeftTrigger;
        private bool _inRightTrigger;

        private float _leftLimit;
        private float _rightLimit;
        private GameObject _faderHandle;
        private Material[] _handleMat;
        private VRCPlayerApi _localPlayer;
        private float _lastValue = 0;

        //[UdonSynced(UdonSyncMode.NotSynced)]
        private bool _isAuthorized = false; // should be set by whitelist and false by default

        private bool _isDesktop = false;

        private bool _isHeld = false;

        public string key => $"{materialPropertyId}_{_uid}";
        public const int EVENT_UPdATE = 0;
        public const int EVENT_COUNT = 1;
        
        protected override int EventCount => EVENT_COUNT;

        // public void _InternalUpdateDebugState()
        // {
        //     VRCPlayerApi owner = Networking.GetOwner(gameObject);
        //     debugState._SetValue("DisableInteractive", DisableInteractive.ToString());
        //     debugState._SetValue("InteractionText", InteractionText.ToString());
        //     debugState._SetValue("isAuthorized", _isAuthorized.ToString());
        //     debugState._SetValue("isDesktop", _isDesktop.ToString());
        //     debugState._SetValue("isHeld", _isHeld.ToString());
        //     debugState._SetValue("_leftGrabbed", _leftGrabbed.ToString());
        //     debugState._SetValue("_rightGrabbed", _rightGrabbed.ToString());
        //     debugState._SetValue("_inLeftTrigger", _inLeftTrigger.ToString());
        //     debugState._SetValue("_inRightTrigger", _inRightTrigger.ToString());
        //     debugState._SetValue("currentValue", currentValue.ToString(valueDisplayFormat));
        //     debugState._SetValue("labelMain", labelMain.ToString());
        //     debugState._SetValue("labelSub", labelSub.ToString());
        //     debugState._SetValue("owner", owner.displayName);
        // }

        void Start()
        {
            _EnsureInit();
        }
        protected override void _Init()
        {
            _faderHandle = gameObject;
            this._uid = gameObject.GetInstanceID();
            _localPlayer = Networking.LocalPlayer;
            _isDesktop = !_localPlayer.IsUserInVR();
            _leftLimit = leftLimiter.gameObject.transform.localPosition.x;
            _rightLimit = rightLimiter.gameObject.transform.localPosition.x;
            if (handleRenderer == null)
            {
                handleRenderer = _faderHandle.GetComponent<MeshRenderer>();
            }

            _handleMat = handleRenderer.materials;
            _handleMat[0].DisableKeyword("_EMISSION");
            _handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            smoothedCurrent = currentValue;
            
            DisableInteractive = true;
            InteractionText = labelMain + " - " + labelSub;

            if (tmpLabelMain)
            {
                tmpLabelMain.text = labelMain;
            }

            if (tmpLabelSub)
            {
                tmpLabelSub.text = labelSub;
            }

            if (tmpLabelValue)
            {
                tmpLabelValue.enabled = alwaysShowValue;
            }

            if (accessControl)
            {
                Log($"registered _OnValidate on {accessControl}");
                accessControl._Register(AccessControl.EVENT_VALIDATE, this, nameof(_OnValidate));
                accessControl._Register(AccessControl.EVENT_ENFORCE_UPDATE, this, nameof(_OnValidate));

                _OnValidate();
                Log($"setting isInteractable to {_isAuthorized} for {Networking.LocalPlayer.displayName}");
            }
            else
            {
                LogError("No access control set on SimpleGlobalToggle");
            }

            // if (debugState)
            // {
            //     debugState._Register(DebugState.EVENT_UPDATE, this, nameof(_InternalUpdateDebugState));
            //     debugState._SetContext(this, nameof(_InternalUpdateDebugState), "ShaderFader." + materialPropertyId);
            // }

            Reset();
            OnDeserialization();
            UpdateValueDisplay();
            _isInitialized = true;
        }

        public void _OnValidate()
        {
            // Log("_OnValidate");
            var oldAuthorized = _isAuthorized;
            _isAuthorized = accessControl._LocalHasAccess();
            if (_isAuthorized != oldAuthorized)
            {
                Log($"setting isAuthorized to {_isAuthorized} for {_localPlayer.displayName}");
            }

            if (_isDesktop && _isAuthorized)
            {
                DisableInteractive = false;
            }
            else
            {
                DisableInteractive = true;
            }
        }

        public void SetValue(float newValue)
        {
            if (!_isAuthorized) return;

            currentValue = newValue;
            RequestSerialization();
            OnDeserialization();
        }

        public void UpdateValueDisplay()
        {
            if (tmpLabelValue)
            {
                if (_isDesktop)
                {
                    tmpLabelValue.enabled = alwaysShowValue || _isHeld;
                }
                else
                {
                     tmpLabelValue.enabled = alwaysShowValue || (_leftGrabbed && _inLeftTrigger) || (_rightGrabbed && _inRightTrigger);
                }
            }
        }

        public override void Interact()
        {
            if (!_isAuthorized) return;
            if (!_isDesktop) return;

            if (!_localPlayer.IsOwner(_faderHandle))
            {
                Networking.SetOwner(_localPlayer, _faderHandle);
            }

            Log("Interact");
            DesktopPickup();
        }

        private void DesktopPickup()
        {
            _isHeld = true;
            _handleMat[0].EnableKeyword("_EMISSION");
            _handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            if (!alwaysShowValue && tmpLabelValue)
            {
                tmpLabelValue.enabled = true;
            }
        }

        private void DesktopDrop()
        {
            _isHeld = false;

            _handleMat[0].DisableKeyword("_EMISSION");
            _handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            if (!alwaysShowValue && tmpLabelValue)
            {
                tmpLabelValue.enabled = false;
            }
        }

        public override void InputLookVertical(float value, UdonInputEventArgs args)
        {
            if (!_isAuthorized) return;
            if (!_isDesktop) return;
            if (!_isHeld) return;
            Log($"InputLookVertical {value} {args.handType}");

            if (!_localPlayer.IsOwner(_faderHandle))
            {
                Networking.SetOwner(_localPlayer, _faderHandle);
            }

            var offset = (valueMax - valueMin) * value / _desktopDampening;

            currentValue = Mathf.Clamp(currentValue + offset, valueMin, valueMax);

            if (currentValue != _lastValue)
            {
                RequestSerialization();
                OnDeserialization();
                _lastValue = currentValue;
            }
        }

        public override void InputGrab(bool value, UdonInputEventArgs args)
        {
            if (!_isAuthorized) return;

            // Log($"InputGrab({value}, {args.handType})");

            if (_isDesktop)
            {
                if (!value)
                {
                    DesktopDrop();
                }
            }

            if (!_isDesktop)
            {
                if (args.handType == HandType.LEFT && value)
                {
                    if (!_leftGrabbed) Log($"LeftGrabbed() UID={this._uid}");

                    _leftGrabbed = true;
                }

                if (args.handType == HandType.RIGHT && value)
                {
                    if (!_rightGrabbed) Log($"RightGrabbed() UID={this._uid}");

                    _rightGrabbed = true;
                }

                if (args.handType == HandType.LEFT && !value)
                {
                    if (_leftGrabbed) Log($"LeftReleased() UID={this._uid}");

                    _leftGrabbed = false;
                }

                if (args.handType == HandType.RIGHT && !value)
                {
                    if (_rightGrabbed) Log($"RightReleased() UID={this._uid}");

                    _rightGrabbed = false;
                }

                UpdateValueDisplay();
            }
        }

        public override void OnDeserialization()
        {
            if (currentValue != _lastValue)
            {
                UpdatePositionToCurrentValue();
                _UpdateTargetFloat(currentValue);
                _lastValue = currentValue;
                // UpdateFaderPosition();
            }
        }

        private void UpdateFaderPosition()
        {
            _faderHandle.transform.localPosition = _sliderPosition;
        }

        public void Update()
        {
            if (!_isAuthorized) return;
            if (_isDesktop) return;
            // if (!isDesktop && (_rightGrabbed || _leftGrabbed) && _inTrigger)
            // if (((_rightGrabbed && _inRightTrigger) || (_leftGrabbed && _inLeftTrigger)))
            if ((_rightGrabbed && _inRightTrigger) || (_leftGrabbed && _inLeftTrigger))
            {
                if (!_localPlayer.IsOwner(_faderHandle))
                {
                    Networking.SetOwner(_localPlayer, _faderHandle);
                }

                Transform handData = _inRightTrigger ? rightHandCollider.transform : leftHandCollider.transform;

                var localHandPos = transform.parent.InverseTransformPoint(handData.position);//.worldToLocalMatrix.MultiplyVector();
                // Log($"transform.position {localHandPos} {handData.localPosition}");
                // var localHandPos = handData.localPosition;
                
                float handPosX = localHandPos.x;
                float clampedPosX = Mathf.Clamp(handPosX, _leftLimit, _rightLimit); // Clamp position within limits

                // TODO: trying out this simpler code for copying over y and z components
                Vector3 newPos = _faderHandle.transform.localPosition;
                newPos.x = clampedPosX;
                // Vector3 newPos = new Vector3(
                    // clampedPosX,
                    // _faderHandle.transform.localPosition.y,
                    // _faderHandle.transform.localPosition.z
                // );
                _faderHandle.transform.localPosition = newPos;
                
                // probably too expensive ...
                // transform.worldToLocalMatrix.MultiplyVector(handData.position);

                // Normalize the slider position to a value between 0 and 1
                float normalizedValue = Mathf.InverseLerp(_leftLimit, _rightLimit, clampedPosX);

                // Map the normalized value to the arbitrary range
                currentValue = Mathf.Lerp(valueMin, valueMax, normalizedValue);

                if (currentValue != _lastValue)
                {
                    // string grabbedText = inRightTrigger ? "Right" : "Left";
                    // this.Log($"Changing Value: {currentValue} Current Hand: {grabbedText}");
                    // _sliderPosition = _faderHandle.transform.localPosition;
                    RequestSerialization();
                    OnDeserialization();
                    _lastValue = currentValue;
                }
            }
        }

        public void OnTriggerEnter(Collider other)
        {
            if (!_isAuthorized) return;

            // Log($"OnTriggerEnter() Other: {other.name} ({other.GetInstanceID()}), Script on: {gameObject.name} ({gameObject.GetInstanceID()}), UID: {uid}");
            if (other.gameObject == leftHandCollider)
            {
                _handleMat[0].EnableKeyword("_EMISSION");
                _handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                if (!_inLeftTrigger)
                {
                    Log($"Left Trigger Enter UID: {this._uid}");
                }

                _inLeftTrigger = true;
                _localPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Left, 1f, 1f, 0.2f);
            }

            if (other.gameObject == rightHandCollider)
            {
                _handleMat[0].EnableKeyword("_EMISSION");
                _handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                if (!_inRightTrigger)
                {
                    Log($"Right Trigger Enter UID: {this._uid}");
                }

                _inRightTrigger = true;
                _localPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Right, 1f, 1f, 0.2f);
            }

            UpdateValueDisplay();
        }

        public void OnTriggerExit(Collider other)
        {
            if (!_isAuthorized)
            {
                return;
            }

            if (other.gameObject.name == "leftHandCollider")
            {
                // handleMat[0].EnableKeyword("_EMISSION");
                // handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                _handleMat[0].DisableKeyword("_EMISSION");
                _handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                if (_inLeftTrigger)
                {
                    Log($"Left Trigger Exit UID: {this._uid}");
                }

                _inLeftTrigger = false;
                _localPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Left, 1f, 1f, 0.2f);
            }

            if (other.gameObject.name == "rightHandCollider")
            {
                // handleMat[0].EnableKeyword("_EMISSION");
                // handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                _handleMat[0].DisableKeyword("_EMISSION");
                _handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                if (_inRightTrigger)
                {
                    Log($"Right Trigger Exit UID: {this._uid}");
                }

                _inRightTrigger = false;
                _localPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Right, 1f, 1f, 0.2f);
            }

            UpdateValueDisplay();
        }

        private float targetFloat;
        private float smoothedCurrent;
        private float epsilon;
        private bool valueInitialized = false;
        
        private void _UpdateTargetFloat(float val)
        {
            Log($"UpdateTargetFloat {val}");
            if (tmpLabelValue)
            {
                tmpLabelValue.text = val.ToString(valueDisplayFormat);
            }

            _UpdateHandlers(EVENT_UPdATE);
            if (externalBehaviour)
            {
                if (externalFloat != "")
                {
                    externalBehaviour.SetProgramVariable(externalFloat, val);
                }
                if (externalEvent != "")
                {
                    externalBehaviour.SendCustomEvent(externalEvent);
                }
            }

            if (valueInitialized && enableValueSmoothing)
            {
                targetFloat = val;
                epsilon = (valueMax - valueMin) / 100;
                UpdateLoop();
            }
            else
            {
                foreach (Material targetMaterial in targetMaterials)
                {
                    switch (this.valueType)
                    {
                        case ShaderValueType.Vector4:
                            Vector4 current = targetMaterial.GetVector(materialPropertyId);
                            current[this.vectorIndex] = val;
                            targetMaterial.SetVector(materialPropertyId, current);
                            break;
                        case ShaderValueType.Float:
                            targetMaterial.SetFloat(materialPropertyId, val);
                            break;
                    }
                }

                valueInitialized = true;
            }
        }
        
        public void UpdateLoop()
        {
            Log($"UpdateLoop {smoothedCurrent} => {targetFloat}");
            // smooth lerp smoothedCurrent to targetFloat

            smoothedCurrent = Mathf.Lerp(targetFloat, smoothedCurrent,Mathf.Exp(-smoothingRate * Time.deltaTime * smoothUpdateInterval));
            
            if (Mathf.Abs(targetFloat - smoothedCurrent) <= epsilon)
            {
                smoothedCurrent = targetFloat;
                Log($"value {materialPropertyId} reached target {targetFloat.ToString(valueDisplayFormat)}");
            }
            else
            {
                this.SendCustomEventDelayedFrames(nameof(UpdateLoop), smoothUpdateInterval, EventTiming.LateUpdate);
            }
            foreach (Material targetMaterial in targetMaterials)
            {
                switch (this.valueType)
                {
                    case ShaderValueType.Vector4:
                        Vector4 current = targetMaterial.GetVector(materialPropertyId);
                        current[this.vectorIndex] = smoothedCurrent;
                        targetMaterial.SetVector(materialPropertyId, current);
                        break;
                    case ShaderValueType.Float:
                        targetMaterial.SetFloat(materialPropertyId, smoothedCurrent);
                        break;
                }
            }
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                Log("OnEnable");
                if (_lastValue != currentValue)
                {
                    _UpdateTargetFloat(currentValue);
                }
            }
        }

        private void OnDisable()
        {
            _lastValue = defaultValue;
            _UpdateTargetFloat(defaultValue);
        }

        private void UpdatePositionToCurrentValue()
        {
            // Calculate t based on currentValue's position between minValue and maxValue
            float t = (currentValue - valueMin) / (valueMax - valueMin);

            // Use t to find the corresponding xPos between LeftLimit and RightLimit
            // float xPos = _leftLimit + (_rightLimit - _leftLimit) * t;
            float xPos = Mathf.Lerp(_leftLimit, _rightLimit, t);
            // Create the new position vector for the slider object
            Vector3 newPos = new Vector3(
                xPos,
                _faderHandle.transform.localPosition.y,
                _faderHandle.transform.localPosition.z
            );

            // Set the slider object's position to newPos
            // _sliderPosition = newPos;

            _faderHandle.transform.localPosition = newPos;
        }

        public void Reset()
        {
            if (!_isAuthorized) return;

            valueInitialized = false; // forces it to skip smoothing
            this.currentValue = this.defaultValue;

            UpdatePositionToCurrentValue();
            Log($"Reset value to {this.defaultValue}");
            RequestSerialization();
        }

        private const string logPrefix = "[<color=#0C00FF>Wo1fieShaderFader</color>]";

        private void LogError(string message)
        {
            Debug.LogError($"{logPrefix} {message}");
            if (Utilities.IsValid(debugLog))
            {
                debugLog._WriteError(
                    $"{logPrefix} {materialPropertyId}",
                    message
                );
            }
        }

        private void Log(string message)
        {
            Debug.Log($"{logPrefix} {message}");
            if (Utilities.IsValid(debugLog))
            {
                debugLog._Write(
                    $"{logPrefix} {materialPropertyId}",
                    message
                );
            }
        }

        private string prevMain;
        private string prevSub;
        private float prevDefaultValue;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);

            //TODO: check on localTransforms too
            if (labelMain == prevMain && labelSub == prevSub && prevDefaultValue == defaultValue)
                return; // To prevent trying to apply the theme to often, as without it every single change in the scene causes it to be applied
            prevMain = labelMain;
            prevSub = labelSub;
            prevDefaultValue = defaultValue;
            ApplyValues();
        }

        [ContextMenu("Apply Values")]
        public void ApplyValues()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);

            InteractionText = labelMain + " - " + labelSub;
            this.MarkDirty();

            if (tmpLabelMain)
            {
                tmpLabelMain.text = labelMain;
                tmpLabelMain.MarkDirty();
            }

            if (tmpLabelSub)
            {
                tmpLabelSub.text = labelSub;
                tmpLabelSub.MarkDirty();
            }
            
            if (tmpLabelValue)
            {
                tmpLabelValue.text = defaultValue.ToString(valueDisplayFormat);
            }

            _faderHandle = gameObject;
            _leftLimit = leftLimiter.gameObject.transform.localPosition.x;
            _rightLimit = rightLimiter.gameObject.transform.localPosition.x;
            currentValue = defaultValue;
            UpdatePositionToCurrentValue();
        }
#endif
    }
}