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

// ReSharper disable RedundantDefaultMemberInitializer

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InvertIf
// ReSharper disable CompareOfFloatsByEqualityOperator
namespace GoogieFaderSystem
{
    // public enum ShaderValueType
    // {
    //     [InspectorName("Float")]
    //     Float,
    //     [InspectorName("Vector 4")]
    //     Vector4
    // };

    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class ShaderFader : EventBase
    {
        [SerializeField] private Material[] targetMaterials;
        [SerializeField] private string materialPropertyId;

        [Tooltip(("Value for reset")), 
         SerializeField]
        private float defaultValue = 0; // Value for reset

        [FormerlySerializedAs("valueMin"),
         Tooltip(("minimum value")),
         SerializeField]
        private float minValue = 0; // Default minimum value

        [FormerlySerializedAs("valueMax"),
         Tooltip(("maximum value")),
         SerializeField]
        private float maxValue = 1; // Default maximum value

        [Header("Value Smoothing")] // header
        [Tooltip(("smoothes out value updating, but can lower frames")),
         SerializeField]
        private bool enableValueSmoothing = false;

        [Tooltip(
            "amount of frames to skip when smoothing the value, higher number == less load, but more choppy smoothing"),
         SerializeField]
        private int smoothUpdateInterval = 5;

        [Tooltip("fraction of the distance covered within roughly 1s"),
         SerializeField]
        private float smoothingRate = 0.15f;

        [Header("Curve")] // header
        [Tooltip("If enabled, the curve below will be used to evaluate the slider value")]
        [SerializeField]
        private bool useCurve;
        
        [Tooltip("Requires \"Use Curve\" to be enabled. E.g. can be changed to be logarithmic for volume sliders.")]
        [SerializeField]
        private AnimationCurve sliderCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Display & Labels")] // header
        [SerializeField]
        private bool alwaysShowValue = false;

        [Tooltip(
            "What the slider value will be formated as.\n- 0.0 means it will always at least show one digit with one decimal point\n- 00 means it will fill always be formated as two digits with no decimal point")]
        [SerializeField]
        private string valueDisplayFormat = "0.0";

        [SerializeField] private string labelMain = "MAIN";
        [SerializeField] private string labelSub = "SUB";

        [Header("Vector 4")] // header
        [SerializeField]
        private bool assignVectorComponent = false;

        [SerializeField] private int vectorIndex = 0;

        // [SerializeField]
        // [Tooltip("divides the vertical look distance by this number")]
        // [SerializeField] 
        private const float _desktopDampening = 20;

        [Header("Internals")] // header
        [Tooltip("ACL used to check who can use the fader")]
        [SerializeField] private AccessControl accessControl;

        [SerializeField] private GameObject leftHandCollider;
        [SerializeField] private GameObject rightHandCollider;

        [SerializeField] private MeshRenderer handleRenderer;
        [SerializeField] private Transform leftLimiter;
        [SerializeField] private Transform rightLimiter;

        [Header("UI")] // header
        [SerializeField] private TextMeshPro tmpLabelMain;

        [SerializeField] private TextMeshPro tmpLabelSub;
        [SerializeField] private TextMeshPro tmpLabelValue;

        [Header("External")] // header
        [SerializeField] private UdonBehaviour externalBehaviour;

        [SerializeField] private string externalFloat = "";
        [SerializeField] private string externalEvent = "";

        [Header("Debug")] // header
        [SerializeField] private DebugLog debugLog;
        // [SerializeField] private DebugState debugState;

        [Header("State")] // header
        [UdonSynced]
        // IMPORTANT, DO NOT DELETE
        private float syncedValueNormalized;

        private float _lastSyncedValueNormalized = 0;

        // value after smoothing and applying curve
        private float localValue;

        public float Value => localValue;

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

        //[UdonSynced(UdonSyncMode.NotSynced)]
        private bool _isAuthorized = false; // should be set by whitelist and false by default

        private bool _isDesktop = false;

        private bool _isHeld = false;

        // Section: value smoothing
        private float smoothingTargetNormalized;
        private float smoothedCurrentNormalized;
        private const float epsilon = 0.01f;
        private bool valueInitialized = false;
        private bool isSmoothing = false;

        public string Key => $"{materialPropertyId}_{_uid}";
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
            _uid = gameObject.GetInstanceID();
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

            smoothedCurrentNormalized = syncedValueNormalized;

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
                LogError("No access control set");
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

            float normalizedNewValue = Mathf.InverseLerp(minValue, maxValue, newValue);
            // syncedValueNormalized = newValue;
            syncedValueNormalized = normalizedNewValue;
            RequestSerialization();
            OnDeserialization();
        }

        private void UpdateValueDisplay()
        {
            if (tmpLabelValue)
            {
                var wasEnabled = tmpLabelValue.enabled;
                if (alwaysShowValue)
                {
                    tmpLabelValue.enabled = true;
                }
                else if (_isDesktop)
                {
                    tmpLabelValue.enabled = _isHeld;
                }
                else
                {
                    tmpLabelValue.enabled = (_leftGrabbed && _inLeftTrigger) || (_rightGrabbed && _inRightTrigger);
                }

                if (!wasEnabled && tmpLabelValue.enabled)
                {
                    tmpLabelValue.text = localValue.ToString(valueDisplayFormat);
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
            // Log($"InputLookVertical {value} {args.handType}");

            if (!_localPlayer.IsOwner(_faderHandle))
            {
                Networking.SetOwner(_localPlayer, _faderHandle);
            }

            // var offset = (maxValue - minValue) * value / _desktopDampening;
            var offset = value / _desktopDampening;
            // syncedValueNormalized = Mathf.Clamp(syncedValueNormalized + offset, minValue, maxValue);
            syncedValueNormalized = Mathf.Clamp(syncedValueNormalized + offset, 0f, 1f);

            if (syncedValueNormalized != _lastSyncedValueNormalized)
            {
                RequestSerialization();
                OnDeserialization();
                _lastSyncedValueNormalized = syncedValueNormalized;
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
            if (syncedValueNormalized != _lastSyncedValueNormalized)
            {
                UpdatePositionToCurrentValue();
                // Log("OnDeserialization");
                _UpdateTargetFloat(syncedValueNormalized);
                _lastSyncedValueNormalized = syncedValueNormalized;
                // UpdateFaderPosition();
            }
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

                var localHandPos =
                    transform.parent.InverseTransformPoint(handData.position); //.worldToLocalMatrix.MultiplyVector();
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
                // syncedValueNormalized = Mathf.Lerp(minValue, maxValue, normalizedValue);

                syncedValueNormalized = normalizedValue;

                if (syncedValueNormalized != _lastSyncedValueNormalized)
                {
                    // string grabbedText = inRightTrigger ? "Right" : "Left";
                    // this.Log($"Changing Value: {currentValue} Current Hand: {grabbedText}");
                    // _sliderPosition = _faderHandle.transform.localPosition;
                    RequestSerialization();
                    OnDeserialization();
                    _lastSyncedValueNormalized = syncedValueNormalized;
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

        private void _UpdateTargetFloat(float normalizedValue)
        {
            Log($"UpdateTargetFloat {normalizedValue}");
            // if (tmpLabelValue)
            // {
            // tmpLabelValue.text = val.ToString(valueDisplayFormat);
            // }

            if (enableValueSmoothing && valueInitialized)
            {
                smoothingTargetNormalized = normalizedValue;
                if (!isSmoothing)
                {
                    // NOTE epsilon is now a const because value is always normalized
                    // epsilon = (maxValue - minValue) / 100;
                    isSmoothing = true;
                    SmoothValueUpdate();
                }
            }
            else
            {
                _AssignValue(normalizedValue);

                valueInitialized = true;
            }
        }


        public void SmoothValueUpdate()
        {
            Log($"UpdateLoop {smoothedCurrentNormalized} => {smoothingTargetNormalized}");
            // smooth lerp smoothedCurrent to targetFloat

            smoothedCurrentNormalized = Mathf.Lerp(
                smoothingTargetNormalized,
                smoothedCurrentNormalized,
                Mathf.Exp(-smoothingRate * Time.deltaTime * smoothUpdateInterval)
            );

            if (Mathf.Abs(smoothingTargetNormalized - smoothedCurrentNormalized) <= epsilon)
            {
                smoothedCurrentNormalized = smoothingTargetNormalized;
                Log(
                    $"value {materialPropertyId} reached target {smoothingTargetNormalized.ToString(valueDisplayFormat)}");
                isSmoothing = false;
            }
            else
            {
                this.SendCustomEventDelayedFrames(nameof(SmoothValueUpdate), smoothUpdateInterval,
                    EventTiming.LateUpdate);
            }

            _AssignValue(smoothedCurrentNormalized);
        }

        private void _AssignValue(float normalizedValue)
        {
            var remappedValue = normalizedValue;
            if (useCurve)
            {
                remappedValue = sliderCurve.Evaluate(normalizedValue);
                Log($"_AssignValue Evaluate curve {normalizedValue} => {remappedValue}");
            }
            
            localValue = Mathf.Lerp(
                minValue,
                maxValue,
                remappedValue
            );

            Log($"_AssignValue Lerp into range {remappedValue} => {localValue}");

            if (tmpLabelValue && tmpLabelValue.enabled)
            {
                tmpLabelValue.text = localValue.ToString(valueDisplayFormat);
            }

            foreach (Material targetMaterial in targetMaterials)
            {
                if (assignVectorComponent)
                {
                    Vector4 current = targetMaterial.GetVector(materialPropertyId);
                    current[this.vectorIndex] = localValue;
                    targetMaterial.SetVector(materialPropertyId, current);
                }
                else
                {
                    targetMaterial.SetFloat(materialPropertyId, localValue);
                }
            }

            _UpdateHandlers(EVENT_UPdATE);
            if (externalBehaviour)
            {
                if (externalFloat != "")
                {
                    externalBehaviour.SetProgramVariable(externalFloat, localValue);
                }

                if (externalEvent != "")
                {
                    externalBehaviour.SendCustomEvent(externalEvent);
                }
            }
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                Log("OnEnable");
                if (_lastSyncedValueNormalized != syncedValueNormalized)
                {
                    _UpdateTargetFloat(syncedValueNormalized);
                }
            }
        }

        private void OnDisable()
        {
            //TODO normalize defaultValue
            var normalizedDefault = Mathf.InverseLerp(minValue, maxValue, defaultValue);
            _lastSyncedValueNormalized = normalizedDefault;
            _UpdateTargetFloat(normalizedDefault);
        }

        private void UpdatePositionToCurrentValue()
        {
            // TODO: no need to normalize here anymore, use syncedValue directly
            // Calculate t based on currentValue's position between minValue and maxValue
            // float t = (syncedValueNormalized - minValue) / (maxValue - minValue);

            // Use t to find the corresponding xPos between LeftLimit and RightLimit
            // float xPos = _leftLimit + (_rightLimit - _leftLimit) * t;
            float xPos = Mathf.Lerp(_leftLimit, _rightLimit, syncedValueNormalized);
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
            var normalizedDefault = Mathf.InverseLerp(minValue, maxValue, defaultValue);
            syncedValueNormalized = normalizedDefault;
            smoothedCurrentNormalized = normalizedDefault;
            smoothingTargetNormalized = normalizedDefault;
            Log($"Reset value to {defaultValue} ({normalizedDefault})");

            UpdatePositionToCurrentValue();
            RequestSerialization();
        }
        
        /// <summary>
        /// Normalizes the given value and evaluates it on a curve, then expands it back to the actual range.
        /// </summary>
        /// <param name="newValue">Float that should be evaluated.</param>
        /// <returns>The provided float evaluated along a curve.</returns>
        protected float EvaluateCurve(float newValue)
        {
            newValue = (newValue - minValue) / (maxValue - minValue);
            newValue = sliderCurve.Evaluate(newValue);
            return minValue + (newValue * (maxValue - minValue));
        }

        private const string logPrefix = "<color=#0C00FF>Wo1fieShaderFader</color>";

        private void LogError(string message)
        {
            Debug.LogError($"[{logPrefix} {materialPropertyId}] {message}");
            if (Utilities.IsValid(debugLog))
            {
                debugLog._WriteError(
                    $"[{logPrefix} {materialPropertyId}]",
                    message
                );
            }
        }

        private void Log(string message)
        {
            Debug.Log($"[{logPrefix} {materialPropertyId}] {message}");
            if (Utilities.IsValid(debugLog))
            {
                debugLog._Write(
                    $"{logPrefix} {materialPropertyId}]",
                    message
                );
            }
        }

        [NonSerialized] private string prevMain;
        [NonSerialized] private string prevSub;
        [NonSerialized] private float prevDefaultValue;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public AccessControl ACL
        {
            get => accessControl;
            set => accessControl = value;
        }
        public DebugLog DebugLog
        {
            get => debugLog;
            set => debugLog = value;
        }
        public GameObject LeftHandCollider
        {
            get => leftHandCollider;
            set => leftHandCollider = value;
        }
        public GameObject RightHandCollider
        {
            get => rightHandCollider;
            set => rightHandCollider = value;
        }
        
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

            if (leftLimiter && rightLimiter)
            {
                _leftLimit = leftLimiter.gameObject.transform.localPosition.x;
                _rightLimit = rightLimiter.gameObject.transform.localPosition.x;
                var normalizedDefault = Mathf.InverseLerp(minValue, maxValue, defaultValue);
                syncedValueNormalized = normalizedDefault;
                UpdatePositionToCurrentValue();
                syncedValueNormalized = 0f;
            }
        }
#endif
    }
}