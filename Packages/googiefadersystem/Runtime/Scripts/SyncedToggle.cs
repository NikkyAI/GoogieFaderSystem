using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using Texel;
using TMPro;
using UnityEngine.Serialization;
using VRC;

namespace GoogieFaderSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class SyncedToggle : EventBase
    {

        [FormerlySerializedAs("_targets")] public GameObject[] targetsOn = { null, null };
        [FormerlySerializedAs("_targets2")] public GameObject[] targetsOff = { null };

        [Tooltip("The button will initialize into this value, toggle this for elements that should be enabled by default")]
        [SerializeField]
        private bool defaultValue = false;

        [Tooltip("If the user cannot use the button, it will not be visible if this is set to True.")] [SerializeField]
        private bool offIfNotUsable = false;

        [SerializeField] public string label;

        [Header("UI")] // header
        [SerializeField]
        public TextMeshPro tmpLabel;

        [Header("Access Control")] // header
        [SerializeField] private bool useACL = true;
        [Tooltip("ACL used to check who can use the toggle")]
        public AccessControl accessControl;
        
        [Header("External")] // header
        [SerializeField]
        private UdonBehaviour externalBehaviour;

        [SerializeField] private string externalBool = "";
        [SerializeField] private string externalEvent = "";

        [Header("Debug")] // header
        public DebugLog debugLog;

        [Header("Backend")] // header
        [Tooltip(
            "This GameObject gets turned off if `Off If Not Usable` is TRUE.\n\n!!MAKE SURE THERE ARE NO SCRIPTS ON THIS OBJECT!!\nscripts do not run if they get turned off.")]
        [SerializeField]
        private GameObject button;

        [Tooltip(
            "This Collider gets turned off if `Off If Not Usable` is TRUE.\nIf you using UI buttons, leave this empty.")]
        [SerializeField]
        private Collider buttonCollider;

        [UdonSynced] private bool _isOn = false;

        private bool _isAuthorized = false;

        public string key => $"{name}_{this.GetInstanceID()}";
        public bool state => _isOn;

        public const int EVENT_UPdATE = 0;
        public const int EVENT_COUNT = 1;

        protected override int EventCount => EVENT_COUNT;

        void Start()
        {
            _EnsureInit();
        }

        protected override void _Init()
        {
            if (button == null)
            {
                button = gameObject;
            }

            if (buttonCollider == null)
            {
                buttonCollider = button.GetComponent<Collider>();
            }

            DisableInteractive = true;
            if (button != null) button.SetActive(!offIfNotUsable);
            if (buttonCollider != null) buttonCollider.enabled = !offIfNotUsable;


            if (!string.IsNullOrEmpty(label))
            {
                InteractionText = label;
                if (tmpLabel)
                {
                    tmpLabel.text = label;
                }
            }

            if (useACL)
            {
                if (accessControl)
                {
                    accessControl._Register(AccessControl.EVENT_VALIDATE, this, nameof(_OnValidate));
                    accessControl._Register(AccessControl.EVENT_ENFORCE_UPDATE, this, nameof(_OnValidate));

                    _OnValidate();
                }
                else
                {
                    LogError("No access control set");
                    DisableInteractive = true;
                }
            }

            _isOn = defaultValue;
            OnDeserialization();
        }

        public void _OnValidate()
        {
            // Log("_OnValidate");
            var oldAuthorized = _isAuthorized;
            _isAuthorized = accessControl._LocalHasAccess();
            if (_isAuthorized != oldAuthorized)
            {
                Log($"setting isAuthorized to {_isAuthorized} for {Networking.LocalPlayer.displayName}");
            }

            if (_isAuthorized)
            {
                DisableInteractive = false;
                if (button)
                {
                    button.SetActive(true);
                }

                if (buttonCollider != null)
                {
                    buttonCollider.enabled = true;
                }
            }
            else
            {
                DisableInteractive = true;
                if (button)
                {
                    button.SetActive(!offIfNotUsable);
                }

                if (buttonCollider != null)
                {
                    buttonCollider.enabled = (!offIfNotUsable);
                }
            }
        }

        public void SetState(bool newValue)
        {
            if (useACL && !_isAuthorized) return;
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            _isOn = newValue;
            RequestSerialization();
            OnDeserialization();
        }

        public void Reset()
        {
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            _isOn = defaultValue;
            RequestSerialization();
            OnDeserialization();
        }

        // // some magic that somehow.. works ?
        // public void _UVR_Init()
        // {
        //     if (isAuthorized)
        //     {
        //         if (button != null) button.SetActive(true);
        //         if (buttonCollider != null) buttonCollider.enabled = true;
        //     }
        // }

        public override void Interact()
        {
            if (useACL && !_isAuthorized) return;

            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            _isOn = !_isOn;
            _UpdateState();
            RequestSerialization();
        }

        private void _UpdateState()
        {
            foreach (var obj in targetsOn)
            {
                obj.SetActive(_isOn);
            }

            foreach (var obj in targetsOff)
            {
                obj.SetActive(!_isOn);
            }

            _UpdateHandlers(EVENT_UPdATE);
            if (externalBehaviour)
            {
                if (externalBool != "")
                {
                    externalBehaviour.SetProgramVariable(externalBool, _isOn);
                }

                if (externalEvent != "")
                {
                    externalBehaviour.SendCustomEvent(externalEvent);
                }
            }
        }

        public override void OnDeserialization()
        {
            _UpdateState();
        }

        private const string logPrefix = "[GlobalToggleWithACL]";

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

        private void Log(string message)
        {
            Debug.Log($"{logPrefix} {message}");
            if (Utilities.IsValid(debugLog))
            {
                debugLog._Write(
                    logPrefix,
                    message
                );
            }
        }

        private string prevLabel;
        private TextMeshPro prevTMPLabel;

#if UNITY_EDITOR && !COMPILER_UDONSHARP

        private void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);

            //TODO: check on localTransforms too
            if (label != prevLabel || tmpLabel != prevTMPLabel)
            {
                // To prevent trying to apply the theme to often, as without it every single change in the scene causes it to be applied
                prevLabel = label;
                prevTMPLabel = tmpLabel;

                ApplyValues();
            }

            if (button == null)
            {
                button = gameObject;
            }

            if (buttonCollider == null)
            {
                buttonCollider = button.GetComponent<Collider>();
            }
        }

        [ContextMenu("Apply Values")]
        public void ApplyValues()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);

            if (!string.IsNullOrEmpty(label))
            {
                InteractionText = label;
                (this as UdonSharpBehaviour).MarkDirty();
                // this.MarkDirty();
                if (tmpLabel != null)
                {
                    tmpLabel.text = label;
                    tmpLabel.MarkDirty();
                }
            }

            // if (pickup != null)
            // {
            //     pickup.InteractionText = labelMain + " - " + labelSub;
            //     if (faderHandle)
            //     {
            //         pickup.transform.localPosition = faderHandle.transform.localPosition;
            //     }
            //     else
            //     {
            //         pickup.transform.localPosition = gameObject.transform.localPosition;
            //     }
            //     pickup.MarkDirty();
            // }
        }

        [ContextMenu("Assign Defaults")]
        public void AssignDefaults()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);

            if (button == null)
            {
                button = gameObject;
            }

            if (buttonCollider == null)
            {
                buttonCollider = button.GetComponent<Collider>();
            }

            this.MarkDirty();
        }
#endif
    }
}