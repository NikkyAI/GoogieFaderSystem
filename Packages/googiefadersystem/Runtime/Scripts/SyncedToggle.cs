using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using Texel;
using TMPro;
using VRC;

namespace GoogieFaderSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class SyncedToggle : ACLBase
    {

        [SerializeField] private GameObject[] targetsOn = { null, null };
        [SerializeField] private GameObject[] targetsOff = { null };

        [Tooltip("The button will initialize into this value, toggle this for elements that should be enabled by default"),
         SerializeField]
        private bool defaultValue = false;

        [Tooltip("If the user cannot use the button, it will not be visible if this is set to True."),
         SerializeField]
        private bool offIfNotUsable = false;

        [SerializeField] private string label;

        [Header("UI")] // header
        [SerializeField]
        public TextMeshPro tmpLabel;

        [Header("Access Control")] // header
        [SerializeField] private bool useACL = true;
        protected override bool UseACL => useACL;
        
        [Tooltip("ACL used to check who can use the toggle"),
         SerializeField]
        private AccessControl accessControl;
        protected override AccessControl AccessControl
        {
            get => accessControl;
            set => accessControl = value;
        }
        
        [Header("External")] // header
        [SerializeField]
        private UdonBehaviour externalBehaviour;

        [SerializeField] private string externalBool = "";
        [SerializeField] private string externalEvent = "";

        [Header("Debug")] // header
        [SerializeField] private DebugLog debugLog;

        protected override DebugLog DebugLog
        {
            get => debugLog;
            set => debugLog = value;
        }
        protected override string LogPrefix => nameof(SyncedToggle);

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

            _isOn = defaultValue;
            OnDeserialization();
        }

        protected override void AccessChanged()
        {
            if (isAuthorized)
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
            if (useACL && !isAuthorized) return;
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

        public override void Interact()
        {
            if (useACL && !isAuthorized) return;

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
                if (obj)
                {
                    obj.SetActive(_isOn);
                }
            }

            foreach (var obj in targetsOff)
            {
                if (obj)
                {
                    obj.SetActive(!_isOn);
                }
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

        [NonSerialized] private string prevLabel;
        [NonSerialized] private TextMeshPro prevTMPLabel;

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
                this.MarkDirty();
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