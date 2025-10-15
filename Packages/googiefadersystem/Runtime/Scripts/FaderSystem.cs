using System;
using Texel;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC;
using VRC.SDKBase;
using VRC.Udon;

namespace GoogieFaderSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class FaderSystem : UdonSharpBehaviour
    {
        [SerializeField] private AccessControl accessControl;
        [SerializeField] private LocalHandCollider localHandCollider;

        [Header("Optional")] // header
        [SerializeField]
        private DebugLog debugLogDefault;

        [SerializeField] private DebugLog debugLogFader;
        [SerializeField] private DebugLog debugLogToggle;
        [SerializeField] private DebugLog debugLogTiltHandle;
        [SerializeField] private DebugLog debugLogResetTrigger;


        [NonSerialized] private AccessControl prevAccessControl;
        [NonSerialized] private DebugLog prevDebugLogDefault;
        [NonSerialized] private DebugLog prevDebugLogFader;
        [NonSerialized] private DebugLog prevDebugLogToggle;
        [NonSerialized] private DebugLog prevDebugLogTiltHandle;
        [NonSerialized] private DebugLog prevDebugLogResetTrigger;
        [NonSerialized] private LocalHandCollider prevLocalHandCollider;
        [NonSerialized] private bool childrenInitialized = false;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);

            if (
                !childrenInitialized
                || prevAccessControl != accessControl
                || prevDebugLogDefault != debugLogDefault
                || prevDebugLogFader != debugLogFader
                || prevDebugLogToggle != debugLogToggle
                || prevDebugLogTiltHandle != debugLogTiltHandle
                || prevDebugLogResetTrigger != debugLogResetTrigger
                || prevLocalHandCollider != localHandCollider
            )
            {
                SetupComponents();
                prevAccessControl = accessControl;
                prevDebugLogFader = debugLogFader;
                prevDebugLogToggle = debugLogTiltHandle;
                prevDebugLogTiltHandle = debugLogFader;
                prevDebugLogResetTrigger = debugLogResetTrigger;
                prevLocalHandCollider = localHandCollider;
                childrenInitialized = true;
            }
        }


        [ContextMenu("Setup Components")]
        private void SetupComponents()
        {
            foreach (var fader in gameObject.GetComponentsInChildren<ShaderFader>(true))
            {
                fader.EditorACL = accessControl;
                fader.EditorDebugLog = debugLogFader ?? debugLogDefault;
                if (localHandCollider)
                {
                    fader.LeftHandCollider = localHandCollider.leftHandCollider;
                    fader.RightHandCollider = localHandCollider.rightHandCollider;
                }

                fader.ApplyValues();
                fader.MarkDirty();
            }

            foreach (var toggle in gameObject.GetComponentsInChildren<SyncedToggle>(true))
            {
                toggle.EditorACL = accessControl;
                toggle.EditorDebugLog = debugLogToggle ?? debugLogDefault;
                toggle.MarkDirty();
            }

            foreach (var handle in gameObject.GetComponentsInChildren<TiltHandle>(true))
            {
                handle.EditorACL = accessControl;
                handle.EditorDebugLog = debugLogTiltHandle ?? debugLogDefault;
                handle.ApplyValues();
                handle.MarkDirty();
            }

            foreach (var resetTrigger in gameObject.GetComponentsInChildren<ResetTrigger>(true))
            {
                resetTrigger.EditorACL = accessControl;
                resetTrigger.EditorDebugLog = debugLogResetTrigger ?? debugLogDefault;
                // resetTrigger.ApplyValues();
                resetTrigger.MarkDirty();
            }
        }
#endif
    }
}