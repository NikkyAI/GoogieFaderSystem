
using Texel;
using UdonSharp;
using UnityEngine;
using VRC;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace GoogieFaderSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class TiltHandle : UdonSharpBehaviour
    {
        [Tooltip(("Value for reset"))] [SerializeField]
        public float defaultValue = -70; // Value for reset

        [Tooltip(("minimum value"))] [SerializeField]
        public float valueMin = -90; // Default minimum value

        [Tooltip(("maximum value"))] [SerializeField]
        public float valueMax = -45; // Default maximum value

        // [SerializeField]
        // [Tooltip("divides the vertical look distance by this number")]
        // [SerializeField]
        private float desktopDampening = 25;
        
        [Header("State")]
        [UdonSynced] public float currentValue = 0;
        
        [Header("Debug")]
        [SerializeField]
        internal DebugLog debugLog;
        // [SerializeField] DebugState debugState;
        
        [Header("Internals")] 
        [Tooltip("ACL used to check who can use the fader")] 
        [SerializeField]
        internal AccessControl accessControl;
        [SerializeField] GameObject hinge;

        [UdonSynced(UdonSyncMode.NotSynced)]
        public bool isAuthorized = false; // should be set by whitelist and false by default

        private VRCPlayerApi _localPlayer;
        private float _lastValue;
        private bool isHeld;
        
        void Start()
        {
            DisableInteractive = true;
            _localPlayer = Networking.LocalPlayer;

            if (accessControl)
            {
                Log($"registered _OnValidate on {accessControl}");
                accessControl._Register(AccessControl.EVENT_VALIDATE, this, nameof(_OnValidate));
                accessControl._Register(AccessControl.EVENT_ENFORCE_UPDATE, this, nameof(_OnValidate));

                _OnValidate();
                Log($"setting isInteractable to {isAuthorized} for {Networking.LocalPlayer.displayName}");
            }
            // if (debugState)
            // {
            //     debugState._Register(DebugState.EVENT_UPDATE, this, nameof(_InternalUpdateDebugState));
            //     debugState._SetContext(this, nameof(_InternalUpdateDebugState), "ShaderFader." + materialPropertyId);
            // }
            
            Reset();
            OnDeserialization();
        }
        
        
        public void _OnValidate()
        {
            // Log("_OnValidate");
            var oldAuthorized = isAuthorized;
            isAuthorized = !accessControl.enforce || _AccessCheck();
            if (isAuthorized != oldAuthorized)
            {
                Log($"setting isAuthorized to {isAuthorized} for {_localPlayer.displayName}");
            }

            if (isAuthorized)
            {
                DisableInteractive = false;
            }
            else
            {
                DisableInteractive = true;
            }
        }
        
        private bool _AccessCheck()
        {
            if (!Utilities.IsValid(accessControl))
                return false;
            return accessControl._LocalHasAccess();
        }

        public override void OnDeserialization()
        {
            if (currentValue != _lastValue)
            {
                _lastValue = currentValue;
                UpdateHingeTilt();
            }
        }

        public override void Interact()
        {
            if (!isAuthorized)
            {
                return;
            }


            if (!_localPlayer.IsOwner(gameObject))
            {
                Networking.SetOwner(_localPlayer, gameObject);
            }
            Log("Interact");
            isHeld = true;
        }

        public override void InputGrab(bool value, UdonInputEventArgs args)
        {
            if (!isAuthorized)
            {
                return;
            }

            if (isHeld && !value)
            {
                Log($"InputGrab {value} {args.handType}");
                isHeld = false;
            }
        }
        
        public override void InputLookVertical(float value, UdonInputEventArgs args)
        {
            if (!isAuthorized)
            {
                return;
            }
            if (!isHeld)
            {
                return;
            }
            Log($"InputLookVertical {value} {args.handType}");

            var offset = (valueMax - valueMin) * value / desktopDampening;
            
            currentValue = Mathf.Clamp(currentValue + offset, valueMin, valueMax);
            RequestSerialization();
            OnDeserialization();
        }

        private void UpdateHingeTilt()
        {
            // Create the new position vector for the slider object
            Quaternion newRot = Quaternion.Euler(
                currentValue,
                hinge.transform.localRotation.y,
                hinge.transform.localRotation.z
            );
            
            hinge.transform.localRotation = newRot;

            // // Set the slider object's position to newPos
            // syncedSliderPosition = newPos;
        }

        public void Reset()
        {
            currentValue = defaultValue;
            RequestSerialization();

            UpdateHingeTilt();
            Log($"Reset tilt to {defaultValue}");
        }
        
        private const string logPrefix = "[<color=#0C00FF>TiltHandle</color>]";

        private void Log(string text)
        {
            Debug.Log($"{logPrefix} {text}");
            if (Utilities.IsValid(debugLog))
            {
                debugLog._Write(
                    $"{logPrefix}",
                    text
                );
            }
        }
        
        private float prevDefault;
#if UNITY_EDITOR && !COMPILER_UDONSHARP

        private void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);
            //TODO: check on localTransforms too
            if (prevDefault == defaultValue)
                return; // To prevent trying to apply the theme to often, as without it every single change in the scene causes it to be applied
            prevDefault = defaultValue;

            ApplyValues();
        }

        [ContextMenu("Apply Values")]
        public void ApplyValues()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);

            currentValue = defaultValue;
            UpdateHingeTilt();
            this.MarkDirty();
            hinge.transform.MarkDirty();
        }
#endif
    }
}