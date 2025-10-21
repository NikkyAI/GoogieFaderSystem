using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using Texel;
using TMPro;
using UnityEngine.TextCore.Text;
using VRC;
using VRC.Core;

namespace GoogieFaderSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class SyncedToggle : ACLBase
    {
        [SerializeField] private GameObject[] targetsOn = { null };
        [SerializeField] private GameObject[] targetsOff = { };

        [Tooltip(
             "The button will initialize into this value, toggle this for elements that should be enabled by default"),
         SerializeField]
        private bool defaultValue = false;

        [Tooltip("If the user cannot use the button, it will not be visible if this is set to True."),
         SerializeField]
        private bool offIfNotUsable = false;

        [Header("UI")] // header
        [SerializeField]
        private string label;

        [SerializeField] private TextMeshPro tmpLabel;

        [Header("Access Control")] // header
        [SerializeField]
        private bool useACL = true;

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
        [SerializeField]
        private DebugLog debugLog;

        protected override DebugLog DebugLog
        {
            get => debugLog;
            set => debugLog = value;
        }

        protected override string LogPrefix => nameof(SyncedToggle);

        [Header("Internals")] // header
        // [Tooltip(
        //     "This GameObject gets turned off if `Off If Not Usable` is TRUE.\n\n!!MAKE SURE THERE ARE NO SCRIPTS ON THIS OBJECT!!\nscripts do not run if they get turned off.")]
        // [SerializeField]
        // private GameObject button;

        // [Tooltip(
        //     "This Collider gets turned off if `Off If Not Usable` is TRUE.\nIf you using UI buttons, leave this empty.")]
        // [SerializeField]
        // private Collider buttonCollider;
        [SerializeField]
        private Transform state0;

        [SerializeField] private Transform state1;
        [SerializeField] private Transform state2;
        [SerializeField] private Transform state3;

        [UdonSynced] private bool _isOn = false;

        public string Key => $"{name}_{this.GetInstanceID()}";
        public bool ButtonState => _isOn;


        public const int EVENT_UPdATE = 0;
        public const int EVENT_COUNT = 1;

        protected override int EventCount => EVENT_COUNT;

        void Start()
        {
            _EnsureInit();
        }

        protected override void _Init()
        {
            // if (button == null)
            // {
            //     button = gameObject;
            // }
            //
            // if (buttonCollider == null)
            // {
            //     buttonCollider = button.GetComponent<Collider>();
            // }

            DisableInteractive = true;

            // if (button != null) button.SetActive(!offIfNotUsable);
            // if (buttonCollider != null) buttonCollider.enabled = !offIfNotUsable;


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
            DisableInteractive = !isAuthorized;
            _UpdateState();
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
            _Interact();
        }

        public void _Interact()
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
            Log($"_UpdateState {_isOn}");
            if (state0) state0.localScale = _isOn ? Vector3.zero : Vector3.one;
            if (state1) state1.localScale = _isOn ? Vector3.one : Vector3.zero;
            if (state2) state2.localScale = Vector3.zero;
            if (state3) state3.localScale = Vector3.zero;
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

            if (isAuthorized)
            {
                SetState(_isOn ? 3 : 2);
            }
            else
            {
                SetState(_isOn ? 1 : 0);
            }
        }

        private void SetState(int state)
        {
            if (state0) state0.localScale = state == 0 ? Vector3.one : Vector3.zero;
            if (state1) state1.localScale = state == 1 ? Vector3.one : Vector3.zero;
            if (state2) state2.localScale = state == 2 ? Vector3.one : Vector3.zero;
            if (state3) state3.localScale = state == 3 ? Vector3.one : Vector3.zero;
        }

        public override void OnDeserialization()
        {
            _UpdateState();
        }

        [NonSerialized] private string prevLabel;
        [NonSerialized] private TextMeshPro prevTMPLabel;

        // [Header("Editor Only")] // header
        // [SerializeField] private TMP_FontAsset fontAsset;
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

            // if (button == null)
            // {
            //     button = gameObject;
            // }
            //
            // if (buttonCollider == null)
            // {
            //     buttonCollider = button.GetComponent<Collider>();
            // }
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

            // if (tmpLabel && fontAsset)
            // {
            //     tmpLabel.font = fontAsset;
            // }
        }

        [ContextMenu("Assign Defaults")]
        public void AssignDefaults()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);

            // if (button == null)
            // {
            //     button = gameObject;
            // }
            //
            // if (buttonCollider == null)
            // {
            //     buttonCollider = button.GetComponent<Collider>();
            // }

            if (tmpLabel == null)
            {
                tmpLabel = transform.Find("Label").GetComponent<TextMeshPro>();
            }

            if (state0 == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.EndsWith("S0"))
                    {
                        state0 = child;
                        state0.localScale = Vector3.one;
                        state0.MarkDirty();
                        break;
                    }
                }
            }

            if (state1 == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.EndsWith("S1"))
                    {
                        state1 = child;
                        state1.MarkDirty();
                        break;
                    }
                }
            }

            if (state2 == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.EndsWith("S2"))
                    {
                        state2 = child;
                        state2.MarkDirty();
                        break;
                    }
                }
            }

            if (state3 == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.EndsWith("S3"))
                    {
                        state3 = child;
                        state3.MarkDirty();
                        break;
                    }
                }
            }

            if (state0)
            {
                state0.localScale = Vector3.one;
                state0.MarkDirty();
            }

            if (state1)
            {
                state1.localScale = Vector3.zero;
                state1.MarkDirty();
            }

            if (state2)
            {
                state2.localScale = Vector3.zero;
                state2.MarkDirty();
            }

            if (state3)
            {
                state3.localScale = Vector3.zero;
                state3.MarkDirty();
            }

            this.MarkDirty();
        }
#endif
    }
}