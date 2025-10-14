
using Texel;
using UdonSharp;
using UnityEngine;
using VRC;
using VRC.SDKBase;
using VRC.Udon;


namespace GoogieFaderSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SyncedToggleManager : UdonSharpBehaviour
    {
        [SerializeField] private AccessControl accessControl;

        [Header("Optional")] // header
        [SerializeField]
        private DebugLog debugLog;


        private AccessControl prevAccessControl;
        private DebugLog prevDebugLog;
        private bool childrenInitialized = false;
#if UNITY_EDITOR && !COMPILER_UDONSHARP

        private void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);

            if (!childrenInitialized || prevAccessControl != accessControl)
            {
                ApplyACLsAndLog();
                prevAccessControl = accessControl;
                childrenInitialized = true;
            }

            if (!childrenInitialized || prevDebugLog != debugLog)
            {
                ApplyACLsAndLog();
                prevDebugLog = debugLog;
                childrenInitialized = true;
            }
        }

        [ContextMenu("Apply ACLs and Log")]
        private void ApplyACLsAndLog()
        {
            foreach (var toggle in gameObject.GetComponentsInChildren<SyncedToggle>(true))
            {
                toggle.accessControl = accessControl;
                toggle.debugLog = debugLog;
                toggle.MarkDirty();
            }
        }
#endif
    }
}
