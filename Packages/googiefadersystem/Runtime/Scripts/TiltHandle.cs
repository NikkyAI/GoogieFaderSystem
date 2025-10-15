using System.Numerics;
using Texel;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace GoogieFaderSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class TiltHandle : UdonSharpBehaviour
    {
        [Tooltip(("Value for reset")),
         SerializeField]
        private float defaultAngle = 20; // Value for reset

        [Tooltip(("minimum value")),
         SerializeField]
        private float minAngle = 0; // Default minimum value

        [Tooltip(("maximum value")),
         SerializeField]
        private float maxAngle = 45; // Default maximum value

        // [SerializeField]
        // [Tooltip("divides the vertical look distance by this number")]
        // [SerializeField]
        private const float desktopDampening = 25;

        [Header("State")] // header
        [UdonSynced] private float syncedValue = 0;

        [Header("Debug")] // header
        [SerializeField] private DebugLog debugLog;
        // [SerializeField] DebugState debugState;

        [Header("Internals")]  // header
        [Tooltip("ACL used to check who can use the fader"), 
         SerializeField]
        private AccessControl accessControl;

        [SerializeField] private Transform hingeTransform;
        private Transform baseTransform;

        [SerializeField] private PickupTrigger pickupTrigger;
        private VRC_Pickup pickup;
        
        // [UdonSynced(UdonSyncMode.NotSynced)]
        private bool isAuthorized = false; // should be set by whitelist and false by default

        private VRCPlayerApi _localPlayer;
        private float _lastValue;
        private bool isHeld;

        void Start()
        {
            DisableInteractive = true;
            _localPlayer = Networking.LocalPlayer;

            if (pickupTrigger)
            {
                pickup = pickupTrigger.GetComponent<VRC_Pickup>();
                pickupTrigger.accessControl = accessControl;
                pickupTrigger._Register(PickupTrigger.EVENT_PICKUP, this, nameof(_OnPickup));
                pickupTrigger._Register(PickupTrigger.EVENT_DROP, this, nameof(_OnDrop));
            }
            else
            {
                LogError("missing pickup");
            }

            if (hingeTransform)
            {
                baseTransform = hingeTransform.parent;
            }
            else
            {
                LogError("missing hingeTransform");
            }

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

            // if (isAuthorized)
            // {
            //     pickup.pickupable = false;
            //     DisableInteractive = false;
            // }
            // else
            // {
            //     pickup.pickupable = false;
            //     DisableInteractive = true;
            // }
        }

        private bool _AccessCheck()
        {
            if (!Utilities.IsValid(accessControl))
                return false;
            return accessControl._LocalHasAccess();
        }

        public override void OnDeserialization()
        {
            if (syncedValue != _lastValue)
            {
                _lastValue = syncedValue;
                UpdateHingeTilt();
            }
        }

        public void _OnPickup()
        {
            if (isHeld)
            {
                Log("already being adjusted");
                return;
            }
            
            if (!_localPlayer.IsOwner(gameObject))
            {
                Networking.SetOwner(_localPlayer, gameObject);
            }
            
            isHeld = true;
            this.SendCustomEventDelayedFrames(nameof(FollowPickup), 5);
        }

        public void _OnDrop()
        {
            isHeld = false;

            Log("handle released, resetting position");
            pickup.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        public void FollowPickup()
        {
            if (!isHeld) return;
            var relativePos = baseTransform.transform.InverseTransformPoint(pickup.transform.position);
            relativePos.x = 0;
            
            // var angle = Vector3.Angle(Vector3.forward, relativePos);
            var angle = Vector3.Angle(relativePos, Vector3.forward);
            syncedValue = Mathf.Clamp(angle, minAngle, maxAngle);
            Log($"angle: {angle} -> {syncedValue}");
            
            RequestSerialization();
            OnDeserialization();

            if (isHeld)
            {
                this.SendCustomEventDelayedFrames(nameof(FollowPickup), 5);
            }
        }

        // public override void Interact()
        // {
        //     if (!isAuthorized)
        //     {
        //         return;
        //     }
        //
        //
        //     if (!_localPlayer.IsOwner(gameObject))
        //     {
        //         Networking.SetOwner(_localPlayer, gameObject);
        //     }
        //
        //     Log("Interact");
        //     isHeld = true;
        // }

        // public override void InputGrab(bool value, UdonInputEventArgs args)
        // {
        //     if (!isAuthorized)
        //     {
        //         return;
        //     }
        //
        //     if (isHeld && !value)
        //     {
        //         Log($"InputGrab {value} {args.handType}");
        //         isHeld = false;
        //     }
        // }

        // public override void InputLookVertical(float value, UdonInputEventArgs args)
        // {
        //     if (!isAuthorized)
        //     {
        //         return;
        //     }
        //
        //     if (!isHeld)
        //     {
        //         return;
        //     }
        //
        //     Log($"InputLookVertical {value} {args.handType}");
        //
        //     var offset = (maxAngle - minAngle) * value / desktopDampening;
        //
        //     syncedValue = Mathf.Clamp(syncedValue + offset, minAngle, maxAngle);
        //     RequestSerialization();
        //     OnDeserialization();
        // }

        private void UpdateHingeTilt()
        {
            // Create the new position vector for the slider object
            Quaternion newRot = Quaternion.Euler(
                -syncedValue,
                hingeTransform.transform.localRotation.y,
                hingeTransform.transform.localRotation.z
            );

            hingeTransform.transform.localRotation = newRot;

            // // Set the slider object's position to newPos
            // syncedSliderPosition = newPos;
        }

        public void Reset()
        {
            syncedValue = defaultAngle;
            RequestSerialization();

            UpdateHingeTilt();
            Log($"Reset tilt to {defaultAngle}");
        }

        private const string logPrefix = "[<color=#0C00FF>TiltHandle</color>]";

        private void Log(string text)
        {
            Debug.Log($"{logPrefix} {text}");
            if (Utilities.IsValid(debugLog))
            {
                debugLog._Write(
                    logPrefix,
                    text
                );
            }
        }
        
        
        private void LogError(string message)
        {
            Debug.LogError($"{logPrefix} {message}");
            if (Utilities.IsValid(debugLog))
            {
                debugLog._WriteError(
                    logPrefix,
                    message
                );
            }
        }

        private float prevDefault;
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
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);
            //TODO: check on localTransforms too
            if (prevDefault == defaultAngle)
                return; // To prevent trying to apply the theme to often, as without it every single change in the scene causes it to be applied
            prevDefault = defaultAngle;

            ApplyValues();
        }

        [ContextMenu("Apply Values")]
        public void ApplyValues()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);

            pickupTrigger.accessControl = accessControl;
            pickupTrigger.MarkDirty();

            syncedValue = defaultAngle;
            UpdateHingeTilt();
            this.MarkDirty();
            hingeTransform.transform.MarkDirty();
            
        }
#endif
    }
}