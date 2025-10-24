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
    public class Fader : ACLBase
    {
        [SerializeField] private Material[] targetMaterials;
        [SerializeField] private string materialPropertyId;

        [Tooltip(("Value for reset")),
         SerializeField]
        private float defaultValue = 0; // Value for reset
        private float _normalizedDefault;

        [SerializeField] private Vector2 valueRange = new Vector2(0, 1);
        private float _minValue, _maxValue;

        // [SerializeField]
        // private Vector2 range = new Vector2(0, 1);

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
        private float smoothingRate = 0.5f;

        [Header("Curve")] // header
        [Tooltip("If enabled, the curve below will be used to evaluate the slider value")]
        [SerializeField]
        private bool useCurve;

        [Tooltip("Requires \"Use Curve\" to be enabled. E.g. can be changed to be logarithmic for volume sliders.")]
        [SerializeField]
        private AnimationCurve sliderCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Display & Labels")] // header
        [SerializeField]
        private bool alwaysShowValue = true;

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

        [SerializeField] private GameObject leftHandCollider;
        [SerializeField] private GameObject rightHandCollider;

        [SerializeField] private MeshRenderer handleRenderer;
        [SerializeField] private Transform leftLimiter;
        [SerializeField] private Transform rightLimiter;
        [SerializeField] private Axis axis = Axis.Z;

        [Header("UI")] // header
        [SerializeField]
        private TextMeshPro tmpLabelMain;

        [SerializeField] private TextMeshPro tmpLabelSub;

        [FormerlySerializedAs("tmpLabelValue"),
         SerializeField]
        private TextMeshPro tmpLabelValueActual;

        [SerializeField] private TextMeshPro tmpLabelValueTarget;

        [Header("External")] // header
        [SerializeField]
        private UdonBehaviour externalBehaviour;

        [SerializeField] private string externalFloat = "";
        [SerializeField] private string externalEvent = "";

        [Header("ACL")] // header
        
        [Tooltip("ACL used to check who can use the fader")]
        [SerializeField]
        private AccessControl accessControl;
        protected override AccessControl AccessControl
        {
            get => accessControl;
            set => accessControl = value;
        }
        protected override bool UseACL => true;
        
        [Header("Debug")] // header
        [SerializeField]
        private DebugLog debugLog;

        protected override DebugLog DebugLog
        {
            get => debugLog;
            set => debugLog = value;
        }

        protected override string LogPrefix => $"<color=#0C00FF>Wo1fieShaderFader</color> {materialPropertyId}";
        // [SerializeField] private DebugState debugState;

        [Header("State")] // header
        [UdonSynced]
        // IMPORTANT, DO NOT DELETE
        private float syncedValueNormalized;

        private float _lastSyncedValueNormalized = 0;

        // value after smoothing and applying curve
        private float localValue;
        private float targetValue;

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

        //deprecated fields

        [FormerlySerializedAs("minValue")]
        [FormerlySerializedAs("valueMin"),
         Tooltip(("minimum value")),
         SerializeField, HideInInspector]
        private float minValueOld = 0; // Default minimum value

        [FormerlySerializedAs("maxValue")]
        [FormerlySerializedAs("valueMax"),
         Tooltip(("maximum value")),
         SerializeField, HideInInspector]
        private float maxValueOld = 1; // Default maximum value

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
            _minValue = valueRange.x;
            _maxValue = valueRange.y;
            _normalizedDefault = Mathf.InverseLerp(_minValue, _maxValue, defaultValue);
            _faderHandle = gameObject;
            _uid = gameObject.GetInstanceID();
            _localPlayer = Networking.LocalPlayer;
            _isDesktop = !_localPlayer.IsUserInVR();
            _leftLimit = leftLimiter.gameObject.transform.localPosition[(int)axis];
            _rightLimit = rightLimiter.gameObject.transform.localPosition[(int)axis];

            Log($"limits: {_leftLimit} .. {_rightLimit}");
            if (handleRenderer == null)
            {
                handleRenderer = _faderHandle.GetComponent<MeshRenderer>();
            }

            // if (handleRenderer)
            // {
            //     _handleMat = handleRenderer.materials;
            //     _handleMat[0].DisableKeyword("_EMISSION");
            //     _handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            // }

            smoothedCurrentNormalized = syncedValueNormalized;

            DisableInteractive = true;
            InteractionText = labelMain + " - " + labelSub;

            if (tmpLabelMain)
            {
                if (tmpLabelSub == null)
                {
                    var text = (labelMain.Trim() + "\n" + labelSub.Trim()).Trim('\n', ' ');
                    tmpLabelMain.text = text;
                }
                else
                {
                    tmpLabelMain.text = labelMain;
                    tmpLabelSub.text = labelSub;
                }
            }

            // if (tmpLabelSub)
            // {
            //     tmpLabelSub.text = labelSub;
            // }

            if (tmpLabelValueActual)
            {
                tmpLabelValueActual.enabled = alwaysShowValue;
            }

            if (tmpLabelValueTarget)
            {
                tmpLabelValueTarget.enabled = alwaysShowValue;
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

        protected override void AccessChanged()
        {
            if (_isDesktop && isAuthorized)
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
            if (!isAuthorized) return;

            float normalizedNewValue = Mathf.InverseLerp(_minValue, _maxValue, newValue);
            // syncedValueNormalized = newValue;
            syncedValueNormalized = normalizedNewValue;
            RequestSerialization();
            OnDeserialization();
        }

        private void UpdateValueDisplay()
        {
            if (tmpLabelValueActual)
            {
                var wasEnabled = tmpLabelValueActual.enabled;
                if (alwaysShowValue)
                {
                    tmpLabelValueActual.enabled = true;
                }
                else if (_isDesktop)
                {
                    tmpLabelValueActual.enabled = _isHeld;
                }
                else
                {
                    tmpLabelValueActual.enabled =
                        (_leftGrabbed && _inLeftTrigger) || (_rightGrabbed && _inRightTrigger);
                }

                if (!wasEnabled && tmpLabelValueActual.enabled)
                {
                    tmpLabelValueActual.text = localValue.ToString(valueDisplayFormat);
                }
            }

            if (tmpLabelValueTarget)
            {
                var wasEnabled = tmpLabelValueTarget.enabled;
                if (alwaysShowValue)
                {
                    tmpLabelValueTarget.enabled = true;
                }
                else if (_isDesktop)
                {
                    tmpLabelValueTarget.enabled = _isHeld;
                }
                else
                {
                    tmpLabelValueTarget.enabled = (_leftGrabbed && _inLeftTrigger) || (_rightGrabbed && _inRightTrigger);
                }

                if (!wasEnabled && tmpLabelValueTarget.enabled)
                {
                    // var targetValue = Mathf.Lerp(minValue, maxValue, smoothingTargetNormalized);
                    tmpLabelValueTarget.text = targetValue.ToString(valueDisplayFormat);
                }
                
            }
        }

        public override void Interact()
        {
            if (!isAuthorized) return;
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
            // if (_handleMat != null)
            // {
            //     _handleMat[0].EnableKeyword("_EMISSION");
            //     _handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            // }

            if (!alwaysShowValue && tmpLabelValueActual)
            {
                tmpLabelValueActual.enabled = true;
            }

            if (!alwaysShowValue && tmpLabelValueTarget)
            {
                tmpLabelValueTarget.enabled = true;
            }
        }

        private void DesktopDrop()
        {
            _isHeld = false;

            // if (_handleMat != null)
            // {
            //     _handleMat[0].DisableKeyword("_EMISSION");
            //     _handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            // }

            if (!alwaysShowValue && tmpLabelValueActual)
            {
                tmpLabelValueActual.enabled = false;
            }

            if (!alwaysShowValue && tmpLabelValueTarget)
            {
                tmpLabelValueTarget.enabled = false;
            }
        }

        public override void InputLookVertical(float value, UdonInputEventArgs args)
        {
            if (!isAuthorized) return;
            if (!_isDesktop) return;
            if (!_isHeld) return;
            // Log($"InputLookVertical {value} {args.handType}");

            if (!_localPlayer.IsOwner(_faderHandle))
            {
                Networking.SetOwner(_localPlayer, _faderHandle);
            }

            if (!valueInitialized)
            {
                syncedValueNormalized = _normalizedDefault;
                smoothedCurrentNormalized = _normalizedDefault;
                smoothingTargetNormalized = _normalizedDefault;
                valueInitialized = true;
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
            if (!isAuthorized) return;

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
            if (!isAuthorized) return;
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

                var localHandPos = transform.parent.InverseTransformPoint(handData.position);

                float handPos = localHandPos[(int)axis];
                float clampedPos = Mathf.Clamp(handPos, _leftLimit, _rightLimit); // Clamp position within limits

                Vector3 newPos = _faderHandle.transform.localPosition;
                newPos[(int)axis] = clampedPos;
                _faderHandle.transform.localPosition = newPos;

                // probably too expensive ...
                // transform.worldToLocalMatrix.MultiplyVector(handData.position);

                // Normalize the slider position to a value between 0 and 1
                float normalizedValue = Mathf.InverseLerp(_leftLimit, _rightLimit, clampedPos);

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
            if (!isAuthorized) return;

            // Log($"OnTriggerEnter() Other: {other.name} ({other.GetInstanceID()}), Script on: {gameObject.name} ({gameObject.GetInstanceID()}), UID: {uid}");
            if (other.gameObject == leftHandCollider)
            {
                // if (_handleMat != null)
                // {
                //     _handleMat[0].EnableKeyword("_EMISSION");
                //     _handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                // }

                if (!_inLeftTrigger)
                {
                    Log($"Left Trigger Enter UID: {this._uid}");
                }

                _inLeftTrigger = true;
                _localPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Left, 1f, 1f, 0.2f);
            }

            if (other.gameObject == rightHandCollider)
            {
                // if (_handleMat != null)
                // {
                //     _handleMat[0].EnableKeyword("_EMISSION");
                //     _handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                // }

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
            if (!isAuthorized)
            {
                return;
            }

            if (other.gameObject.name == "leftHandCollider")
            {
                // if (_handleMat != null)
                // {
                //     // handleMat[0].EnableKeyword("_EMISSION");
                //     // handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                //     _handleMat[0].DisableKeyword("_EMISSION");
                //     _handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                // }

                if (_inLeftTrigger)
                {
                    Log($"Left Trigger Exit UID: {this._uid}");
                }

                _inLeftTrigger = false;
                _localPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Left, 1f, 1f, 0.2f);
            }

            if (other.gameObject.name == "rightHandCollider")
            {
                // if (_handleMat != null)
                // {
                //     // handleMat[0].EnableKeyword("_EMISSION");
                //     // handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                //     _handleMat[0].DisableKeyword("_EMISSION");
                //     _handleMat[0].globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                // }

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

            if (tmpLabelValueTarget)
            {
                var remappedTarget = normalizedValue;
                if (useCurve)
                {
                    remappedTarget = sliderCurve.Evaluate(normalizedValue);
                    Log($"Target value eval curve {normalizedValue} => {remappedTarget}");
                }

                targetValue = Mathf.Lerp(
                    _minValue,
                    _maxValue,
                    remappedTarget
                );

                if (tmpLabelValueTarget && tmpLabelValueTarget.enabled)
                {
                    tmpLabelValueTarget.text = targetValue.ToString(valueDisplayFormat);
                }
            }

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
                _AssignValue(normalizedValue, false);

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
                this.SendCustomEventDelayedFrames(
                    nameof(SmoothValueUpdate),
                    smoothUpdateInterval,
                    EventTiming.LateUpdate
                );
            }

            _AssignValue(smoothedCurrentNormalized, true);
        }

        private void _AssignValue(float normalizedValue, bool withTarget)
        {
            var remappedValue = normalizedValue;
            if (useCurve)
            {
                remappedValue = sliderCurve.Evaluate(normalizedValue);
                Log($"_AssignValue Evaluate curve {normalizedValue} => {remappedValue}");
            }

            localValue = Mathf.Lerp(
                _minValue,
                _maxValue,
                remappedValue
            );

            Log($"_AssignValue Lerp into range {remappedValue} => {localValue}");

            if (tmpLabelValueActual && tmpLabelValueActual.enabled)
            {
                tmpLabelValueActual.text = localValue.ToString(valueDisplayFormat);
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
            _lastSyncedValueNormalized = _normalizedDefault;
            _UpdateTargetFloat(_normalizedDefault);
        }

        private void UpdatePositionToCurrentValue()
        {
            // TODO: no need to normalize here anymore, use syncedValue directly
            // Calculate t based on currentValue's position between minValue and maxValue
            // float t = (syncedValueNormalized - minValue) / (maxValue - minValue);

            // Use t to find the corresponding xPos between LeftLimit and RightLimit
            // float xPos = _leftLimit + (_rightLimit - _leftLimit) * t;
            float zPos = Mathf.Lerp(_leftLimit, _rightLimit, syncedValueNormalized);
            // Create the new position vector for the slider object
            Vector3 newPos = _faderHandle.transform.localPosition;
            newPos[(int)axis] = zPos;

            // Set the slider object's position to newPos
            // _sliderPosition = newPos;

            _faderHandle.transform.localPosition = newPos;
        }

        public void Reset()
        {
            if (!isAuthorized) return;

            if (!_localPlayer.IsOwner(_faderHandle))
            {
                Networking.SetOwner(_localPlayer, _faderHandle);
            }

            valueInitialized = false; // forces it to skip smoothing
            syncedValueNormalized = _normalizedDefault;
            smoothedCurrentNormalized = _normalizedDefault;
            smoothingTargetNormalized = _normalizedDefault;
            Log($"Reset value to {defaultValue} ({_normalizedDefault})");

            UpdatePositionToCurrentValue();
            RequestSerialization();
        }

        // /// <summary>
        // /// Normalizes the given value and evaluates it on a curve, then expands it back to the actual range.
        // /// </summary>
        // /// <param name="newValue">Float that should be evaluated.</param>
        // /// <returns>The provided float evaluated along a curve.</returns>
        // protected float EvaluateCurve(float newValue)
        // {
        //     newValue = (newValue - _minValue) / (_maxValue - _minValue);
        //     newValue = sliderCurve.Evaluate(newValue);
        //     return _minValue + (newValue * (_maxValue - _minValue));
        // }

        [NonSerialized] private string prevMain;
        [NonSerialized] private string prevSub;
        [NonSerialized] private float prevDefaultValue;
        [NonSerialized] private float prevMinValue;
        [NonSerialized] private float prevMaxValue;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        // [Header("Editor Only")] // header
        // [SerializeField] private TMP_FontAsset fontAsset;
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

            if (minValueOld != 0 && maxValueOld != 1)
            {
                valueRange = new Vector2(minValueOld, maxValueOld);
                minValueOld = 0;
                maxValueOld = 1;
                this.MarkDirty();
            }
            
            //TODO: check on localTransforms too
            if (labelMain == prevMain && labelSub == prevSub && prevDefaultValue == defaultValue && prevMinValue == _minValue && prevMaxValue == _maxValue)
                return; // To prevent trying to apply the theme to often, as without it every single change in the scene causes it to be applied

            _minValue = valueRange.x;
            _maxValue = valueRange.y;
            _normalizedDefault = Mathf.InverseLerp(_minValue, _maxValue, defaultValue);

            ApplyValues();

            prevMain = labelMain;
            prevSub = labelSub;
            prevDefaultValue = defaultValue;
            prevMinValue = minValueOld;
            prevMaxValue = maxValueOld;
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
                // if (fontAsset)
                // {
                //     tmpLabelMain.font = fontAsset;
                // }
                if (tmpLabelSub == null)
                {
                    var text = (labelMain.Trim() + "\n" + labelSub.Trim()).Trim('\n', ' ');
                    tmpLabelMain.text = text;
                }
                else
                {
                    tmpLabelMain.text = labelMain;
                }
                
                if (axis == Axis.X)
                {
                    tmpLabelMain.transform.localPosition = new Vector3(
                        tmpLabelMain.transform.localPosition.x,
                        transform.localPosition.y,
                        transform.localPosition.z
                    );
                }
                else if (axis == Axis.Y)
                {
                    tmpLabelMain.transform.localPosition = new Vector3(
                        transform.localPosition.x,
                        tmpLabelMain.transform.localPosition.y,
                        transform.localPosition.z
                    );
                }
                else if (axis == Axis.Z)
                {
                    tmpLabelMain.transform.localPosition = new Vector3(
                        transform.localPosition.x,
                        transform.localPosition.y,
                        tmpLabelMain.transform.localPosition.z
                    );
                }
                tmpLabelMain.MarkDirty();
            }

            if (tmpLabelSub)
            {
                // if (fontAsset)
                // {
                //     tmpLabelSub.font = fontAsset;
                // }
                tmpLabelSub.text = labelSub;
                
                if (axis == Axis.X)
                {
                    tmpLabelSub.transform.localPosition = new Vector3(
                        tmpLabelSub.transform.localPosition.x,
                        transform.localPosition.y,
                        transform.localPosition.z
                    );
                }
                else if (axis == Axis.Y)
                {
                    tmpLabelSub.transform.localPosition = new Vector3(
                        transform.localPosition.x,
                        tmpLabelSub.transform.localPosition.y,
                        transform.localPosition.z
                    );
                }
                else if (axis == Axis.Z)
                {
                    tmpLabelSub.transform.localPosition = new Vector3(
                        transform.localPosition.x,
                        transform.localPosition.y,
                        tmpLabelSub.transform.localPosition.z
                    );
                }
                tmpLabelSub.MarkDirty();
            }

            if (tmpLabelValueActual)
            {
                // if (fontAsset)
                // {
                //     tmpLabelValueActual.font = fontAsset;
                // }
                tmpLabelValueActual.text = defaultValue.ToString(valueDisplayFormat);
                if (axis == Axis.X)
                {
                    tmpLabelValueActual.transform.localPosition = new Vector3(
                        tmpLabelValueActual.transform.localPosition.x,
                        transform.localPosition.y,
                        transform.localPosition.z
                    );
                }
                else if (axis == Axis.Y)
                {
                    tmpLabelValueActual.transform.localPosition = new Vector3(
                        transform.localPosition.x,
                        tmpLabelValueActual.transform.localPosition.y,
                        transform.localPosition.z
                    );
                }
                else if (axis == Axis.Z)
                {
                    tmpLabelValueActual.transform.localPosition = new Vector3(
                        transform.localPosition.x,
                        transform.localPosition.y,
                        tmpLabelValueActual.transform.localPosition.z
                    );
                }
            }

            if (tmpLabelValueTarget)
            {
                // if (fontAsset)
                // {
                //     tmpLabelValueTarget.font = fontAsset;
                // }
                tmpLabelValueTarget.text = defaultValue.ToString(valueDisplayFormat);
                if (axis == Axis.X)
                {
                    tmpLabelValueTarget.transform.localPosition = new Vector3(
                        tmpLabelValueTarget.transform.localPosition.x,
                        transform.localPosition.y,
                        transform.localPosition.z
                    );
                }
                else if (axis == Axis.Y)
                {
                    tmpLabelValueTarget.transform.localPosition = new Vector3(
                        transform.localPosition.x,
                        tmpLabelValueTarget.transform.localPosition.y,
                        transform.localPosition.z
                    );
                }
                else if (axis == Axis.Z)
                {
                    tmpLabelValueTarget.transform.localPosition = new Vector3(
                        transform.localPosition.x,
                        transform.localPosition.y,
                        tmpLabelValueTarget.transform.localPosition.z
                    );
                }
            }

            _faderHandle = gameObject;

            if (leftLimiter && rightLimiter)
            {
                _leftLimit = leftLimiter.gameObject.transform.localPosition[(int)axis];
                _rightLimit = rightLimiter.gameObject.transform.localPosition[(int)axis];
                syncedValueNormalized = _normalizedDefault;
                UpdatePositionToCurrentValue();
                syncedValueNormalized = 0f;
            }
        }
#endif
    }
}