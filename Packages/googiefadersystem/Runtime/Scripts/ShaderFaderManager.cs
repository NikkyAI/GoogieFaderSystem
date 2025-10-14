
using Texel;
using UdonSharp;
using UnityEngine;
using VRC;
using VRC.SDKBase;
using VRC.Udon;

namespace GoogieFaderSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ShaderFaderManager : UdonSharpBehaviour
    {
        [SerializeField] private AccessControl accessControl;
        [SerializeField] private LocalHandCollider localHandCollider;

        [Header("Optional")] // header
        [SerializeField]
        private DebugLog debugLog;


        private AccessControl prevAccessControl;
        private DebugLog prevDebugLog;
        private LocalHandCollider prevLocalhandCollider;
        private bool childrenInitialized = false;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);

            if (!childrenInitialized || prevAccessControl != accessControl)
            {
                SetupFaders();
                prevAccessControl = accessControl;
                childrenInitialized = true;
                return;
            }

            if (!childrenInitialized || prevDebugLog != debugLog)
            {
                SetupFaders();
                prevDebugLog = debugLog;
                childrenInitialized = true;
                return;
            }

            if (!childrenInitialized || prevLocalhandCollider != localHandCollider)
            {
                SetupFaders();
                prevDebugLog = debugLog;
                childrenInitialized = true;
                return;
            }
        }


        [ContextMenu("Setup Faders")]
        private void SetupFaders()
        {
            foreach (var fader in gameObject.GetComponentsInChildren<ShaderFader>(true))
            {
                fader.accessControl = accessControl;
                fader.debugLog = debugLog;
                if (localHandCollider)
                {
                    fader.leftHandCollider = localHandCollider.leftHandCollider;
                    fader.rightHandCollider = localHandCollider.rightHandCollider;
                }

                fader.MarkDirty();
            }
        }
#endif
    }
}
