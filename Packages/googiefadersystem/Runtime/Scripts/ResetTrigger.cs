using System;
using Texel;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC;
using VRC.SDKBase;
using VRC.Udon;

namespace GoogieFaderSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ResetTrigger : ACLBase
    {
        [SerializeField] private Fader[] shaderFaders;
        [SerializeField] private SyncedToggle[] toggles;

        [Header("Internals")] // header
        [Tooltip("ACL used to check who can use the fader")]
        [SerializeField]
        private AccessControl accessControl;

        protected override AccessControl AccessControl
        {
            get => accessControl;
            set => accessControl = value;
        }

        protected override bool UseACL => true;

        protected override string LogPrefix => nameof(ResetTrigger);

        [Header("UI")] // header
        [SerializeField]
        private TextMeshPro tmpLabel;

        [SerializeField] private string label = "Reset";

        [Header("Debug")] // header
        [SerializeField]
        private DebugLog debugLog;

        protected override DebugLog DebugLog
        {
            get => debugLog;
            set => debugLog = value;
        }

        public const int EVENT_RESET = 0;
        public const int EVENT_COUNT = 1;
        protected override int EventCount => EVENT_COUNT;

        void Start()
        {
            _EnsureInit();
        }

        protected override void _Init()
        {
            DisableInteractive = true;

            SetState(0);

            InteractionText = label;
        }

        protected override void AccessChanged()
        {
            DisableInteractive = !isAuthorized;


            SetState(isAuthorized ? 1 : 0);
        }

        public override void Interact()
        {
            _UpdateHandlers(EVENT_RESET);
            foreach (var fader in shaderFaders)
            {
                fader.Reset();
                fader.OnDeserialization();
            }

            foreach (var toggle in toggles)
            {
                if (!Networking.IsOwner(toggle.gameObject))
                {
                    Networking.SetOwner(Networking.LocalPlayer, toggle.gameObject);
                }

                toggle.Reset();
                toggle.OnDeserialization();
            }
        }

        private void SetState(int state)
        {
            if (state0) state0.localScale = state == 0 ? Vector3.one : Vector3.zero;
            if (state1) state1.localScale = state == 1 ? Vector3.one : Vector3.zero;
            if (state2) state2.localScale = state == 2 ? Vector3.one : Vector3.zero;
            if (state3) state3.localScale = state == 3 ? Vector3.one : Vector3.zero;
        }

        [SerializeField] private Transform state0;
        [SerializeField] private Transform state1;
        [SerializeField] private Transform state2;
        [SerializeField] private Transform state3;

        [NonSerialized] private string prevLabel;
        [NonSerialized] private TextMeshPro prevTMPLabel;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        // [Header("Editor Only")] // header
        // [SerializeField] private TMP_FontAsset fontAsset;
        [ContextMenu("Assign Defaults")]
        public void AssignDefaults()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);

            if (tmpLabel == null)
            {
                tmpLabel = transform.Find("Label").GetComponent<TextMeshPro>();
            }

            // if (tmpLabel && fontAsset)
            // {
            //     tmpLabel.font = fontAsset;
            // }

            if (label != prevLabel || tmpLabel != prevTMPLabel)
            {
                // To prevent trying to apply the theme to often, as without it every single change in the scene causes it to be applied
                prevLabel = label;
                prevTMPLabel = tmpLabel;
            }

            if (state0 == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.EndsWith("S0"))
                    {
                        state0 = child;
                        state0.localScale = Vector3.zero;
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
                        state1.localScale = Vector3.zero;
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
                        state2.localScale = Vector3.one;
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
                        state3.localScale = Vector3.zero;
                        state3.MarkDirty();
                        break;
                    }
                }
            }

            SetState(0);

            if (state0)
            {
                state0.MarkDirty();
            }

            if (state1)
            {
                state1.MarkDirty();
            }

            if (state2)
            {
                state2.MarkDirty();
            }

            if (state3)
            {
                state3.MarkDirty();
            }

            this.MarkDirty();
        }
#endif
    }
}