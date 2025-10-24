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
using VRC.Udon.Common.Interfaces;
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

        [SerializeField] private bool invertAngle = false;

        [SerializeField] private Axis rotationAxis = Axis.X;

        [SerializeField] private Vector3 forwardVector = Vector3.forward;
        
        [SerializeField] private Axis upAxis = Axis.Y;

        // [SerializeField]
        // [Tooltip("divides the vertical look distance by this number")]
        // [SerializeField]
        private const float desktopDampening = 25;

        [Header("State")] // header
        [UdonSynced]
        private float syncedValue = 0;

        public float SyncedValue => syncedValue;

        [Header("Debug")] // header
        [SerializeField]
        private DebugLog debugLog;

        protected override DebugLog DebugLog
        {
            get => debugLog;
            set => debugLog = value;
        }

        protected override string LogPrefix => nameof(TiltHandle);
        // [SerializeField] DebugState debugState;

        [Header("Internals")] // header
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
        [SerializeField] private Transform hingeBase;

        [SerializeField] private PickupTrigger pickupTrigger;
        private VRC_Pickup pickup;
        private Rigidbody pickupRigidBody;
        [SerializeField] private Transform pickupReset;

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

            if (pickupTrigger == null)
            {
                pickupTrigger = gameObject.GetComponent<PickupTrigger>();
            }

            if (pickupTrigger)
            {
                pickup = pickupTrigger.GetComponent<VRC_Pickup>();
                pickupTrigger.accessControl = accessControl;
                pickupTrigger._Register(PickupTrigger.EVENT_PICKUP, this, nameof(_OnPickup));
                pickupTrigger._Register(PickupTrigger.EVENT_DROP, this, nameof(_OnDrop));
            }
            else
            {
                LogError("missing pickup trigger");
            }

            if (pickup == null)
            {
                pickup = gameObject.GetComponent<VRC_Pickup>();
            }

            pickupRigidBody = pickup.GetComponent<Rigidbody>();
            pickupRigidBody.useGravity = false;
            pickupRigidBody.isKinematic = false;
            if (pickupReset == null)
            {
                pickupReset = transform;
            }

            pickup.transform.SetPositionAndRotation(pickupReset.position, pickupReset.rotation);

            if (hingeTransform)
            {
                // hingeBase = hingeTransform;
                // hingeBase.rotation = Quaternion.FromToRotation(Vector3.zero, forwardVector);
                // if (rotationAxis == Axis.X)
                // {
                //     hingeBase.localRotation = Quaternion.Euler(
                //         0,
                //         hingeTransform.localEulerAngles.y,
                //         hingeTransform.localEulerAngles.z
                //     );
                // }
                // else if (rotationAxis == Axis.Y)
                // {
                //     hingeBase.localRotation = Quaternion.Euler(
                //         hingeTransform.localEulerAngles.x,
                //         0,
                //         hingeTransform.localEulerAngles.z
                //     );
                // }
                // else if (rotationAxis == Axis.Z)
                // {
                //     hingeBase.localRotation = Quaternion.Euler(
                //         hingeTransform.localEulerAngles.x,
                //         hingeTransform.localEulerAngles.y,
                //         0
                //     );
                // }
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


            if (!_localPlayer.IsOwner(gameObject))
            {
                Networking.SetOwner(_localPlayer, gameObject);
            }

            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ResetPosition));
        }

        public void ResetPosition()
        {
            Log("handle released, resetting position");
            pickupRigidBody.angularVelocity = Vector3.zero;
            pickupRigidBody.velocity = Vector3.zero;
            pickup.transform.SetPositionAndRotation(pickupReset.position, pickupReset.rotation);
        }

        public void FollowPickup()
        {
            if (!isHeld) return;

            
            var relativePos = hingeBase.transform.InverseTransformPoint(pickup.transform.position);
            if (rotationAxis == Axis.X)
            {
                relativePos.x = 0;
            }
            else if (rotationAxis == Axis.Y)
            {
                relativePos.y = 0;
            }
            else if (rotationAxis == Axis.Z)
            {
                relativePos.z = 0;
            }
            relativePos[(int) upAxis] = Mathf.Clamp(relativePos[(int) upAxis], 0, Mathf.Infinity);

            // var angle = Vector3.Angle(Vector3.forward, relativePos);
            // var angle = Vector3.Angle(relativePos, forwardVector);
            // Log($"Angle({relativePos}, {forwardVector}) => {angle}");
            var angle = Vector3.Angle(forwardVector, relativePos);
            Log($"Angle({forwardVector}, {relativePos}) => {angle}");
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
                hingeTransform.transform.localRotation.x,
                hingeTransform.transform.localRotation.y,
                hingeTransform.transform.localRotation.z
            );

            var angle = syncedValue;
            if (invertAngle)
            {
                angle = -angle;
            }

            if (rotationAxis == Axis.X)
            {
                newRot = Quaternion.Euler(
                    angle,
                    hingeTransform.transform.localRotation.y,
                    hingeTransform.transform.localRotation.z
                );
            }
            else if (rotationAxis == Axis.Y)
            {
                newRot = Quaternion.Euler(
                    hingeTransform.transform.localRotation.x,
                    angle,
                    hingeTransform.transform.localRotation.z
                );
            }
            else if (rotationAxis == Axis.Z)
            {
                newRot = Quaternion.Euler(
                    hingeTransform.transform.localRotation.x,
                    hingeTransform.transform.localRotation.y,
                    angle
                );
            }

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
        [NonSerialized] private Vector3 prevResetPos;
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
            if (prevDefault == defaultAngle && pickupTrigger == prevPickupTrigger && pickupReset?.transform.localPosition == prevResetPos)
                return; // To prevent trying to apply the theme to often, as without it every single change in the scene causes it to be applied
            ApplyValues();
            prevDefault = defaultAngle;
            prevPickupTrigger = pickupTrigger;
            if(pickupReset)
                prevResetPos = pickupReset.localPosition;
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
            if (pickupTrigger == null)
            {
                pickupTrigger = gameObject.GetComponent<PickupTrigger>();
            }
            if (pickupTrigger)
            {
                pickup = pickupTrigger.GetComponent<VRC_Pickup>();
                pickupTrigger.accessControl = accessControl;
                pickupTrigger.MarkDirty();
            }
            pickupRigidBody = pickup.GetComponent<Rigidbody>();
            
            if (pickupReset == null)
            {
                pickupReset = transform;
            }
            ResetPosition();
            this.MarkDirty();
            hingeTransform.transform.MarkDirty();
        }
#endif
    }
}