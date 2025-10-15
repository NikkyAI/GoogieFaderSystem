
using Texel;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace GoogieFaderSystem {
    public class ResetTrigger : EventBase
    {
        [SerializeField] private ShaderFader[] shaderFaders;
        [SerializeField] private SyncedToggle[] toggles;

        [Header("Internals")] // header
        [Tooltip("ACL used to check who can use the fader")]
        [SerializeField] private AccessControl accessControl;
        
        private bool _isAuthorized = false; // should be set by whitelist and false by default

        public const int EVENT_RESET = 0;
        public const int EVENT_COUNT = 1;
        protected override int EventCount => EVENT_COUNT;

        void Start()
        {
            _EnsureInit();
        }

        protected override void _Init()
        {
            
            if (accessControl)
            {
                Log($"registered _OnValidate on {accessControl}");
                accessControl._Register(AccessControl.EVENT_VALIDATE, this, nameof(_OnValidate));
                accessControl._Register(AccessControl.EVENT_ENFORCE_UPDATE, this, nameof(_OnValidate));

                _OnValidate();
                Log($"setting isInteractable to {_isAuthorized} for {Networking.LocalPlayer.displayName}");
            }
            else
            {
                LogError("No access control set");
            }
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
            }
            else
            {
                DisableInteractive = true;
            }
        }

        public override void Interact(){
            _UpdateHandlers(EVENT_RESET);
            foreach(var fader in shaderFaders){
                fader.Reset();
                fader.OnDeserialization();
            }
            foreach(var toggle in toggles){
                if (!Networking.IsOwner(toggle.gameObject))
                {
                    Networking.SetOwner(Networking.LocalPlayer, toggle.gameObject);
                }
                toggle.Reset();
                toggle.OnDeserialization();
            }
        }
        
        
        private const string logPrefix = "[ResetTrigger]";

        private void LogError(string message)
        {
            Debug.LogError($"{logPrefix} {message}");
            // if (Utilities.IsValid(debugLog))
            // {
            //     debugLog._WriteError(
            //         $"[{logPrefix} {materialPropertyId}]",
            //         message
            //     );
            // }
        }

        private void Log(string message)
        {
            Debug.Log($"{logPrefix} {message}");
            // if (Utilities.IsValid(debugLog))
            // {
            //     debugLog._Write(
            //         $"{logPrefix} {materialPropertyId}]",
            //         message
            //     );
            // }
        }
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public AccessControl ACL
        {
            get => accessControl;
            set => accessControl = value;
        }
#endif
    }
}
