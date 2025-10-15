
using Texel;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace GoogieFaderSystem {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ResetTrigger : ACLBase
    {
        [SerializeField] private ShaderFader[] shaderFaders;
        [SerializeField] private SyncedToggle[] toggles;

        [Header("Internals")] // header
        [Tooltip("ACL used to check who can use the fader")]
        [SerializeField] private AccessControl accessControl;
        protected override AccessControl AccessControl
        {
            get => accessControl;
            set => accessControl = value;
        }
        protected override bool UseACL => true;

        protected override string LogPrefix => nameof(ResetTrigger);
        
        [Header("Debug")] // header
        [SerializeField] private DebugLog debugLog;

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
            
        }
        
        protected override void AccessChanged()
        {
            if (isAuthorized)
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
    }
}
