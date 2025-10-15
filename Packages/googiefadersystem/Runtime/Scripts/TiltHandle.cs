using System;
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
    public class TiltHandle : ACLBase
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
        public float SyncedValue => syncedValue;

        [Header("Debug")] // header
        [SerializeField] private DebugLog debugLog;
        protected override DebugLog DebugLog
        {
            get => debugLog;
            set => debugLog = value;
        }
        protected override string LogPrefix => nameof(TiltHandle);
        // [SerializeField] DebugState debugState;

        [Header("Internals")]  // header
        [Tooltip("ACL used to check who can use the fader"), 
         SerializeField]
        private AccessControl accessControl;
        protected override AccessControl AccessControl
        {
            get => accessControl;
            set => accessControl = value;
        }
        protected override bool UseACL => true;

        [SerializeField] private Transform hingeTransform;
        private Transform baseTransform;

        [SerializeField] private PickupTrigger pickupTrigger;
        private VRC_Pickup pickup;

        private VRCPlayerApi _localPlayer;
        private float _lastValue;
        private bool isHeld;

        private void Start()
        {
            _EnsureInit();
        }

        protected override void _Init()
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

            Reset();
            OnDeserialization();
        }

        protected override void AccessChanged()
        {
            // Log("_OnValidate");

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

        [NonSerialized] private float prevDefault;
        [NonSerialized] private PickupTrigger prevPickupTrigger;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public override AccessControl EditorACL
        {
            get => AccessControl;
            set
            {
                AccessControl = value;
                if (pickupTrigger)
                {
                    pickupTrigger.accessControl = accessControl;
                    pickupTrigger.MarkDirty();
                }
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);
            //TODO: check on localTransforms too
            if (prevDefault == defaultAngle && pickupTrigger == prevPickupTrigger)
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