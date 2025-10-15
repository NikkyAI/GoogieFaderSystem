using System.Runtime.CompilerServices;
using Texel;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDKBase;
using VRC.Udon;

namespace GoogieFaderSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LocalToggle : ACLBase
    {
        [SerializeField] private GameObject[] targetsOn = { };
        [SerializeField] private GameObject[] targetsOff = { };

        [Tooltip(
            "The button will initialize into this value, toggle this for elements that should be enabled by default")]
        [SerializeField] private bool defaultValue = false;

        [Header("Persistence")] // header
        [Tooltip("Turn on if this toggle should be saved using Persistence.")]
        [SerializeField] private bool usePersistence = false;
        [Tooltip("Data Key that will be used to save / load this Setting, everything using Persistence should have a different Data Key.")]
        [SerializeField] private string dataKey = "CHANGE THIS";

        [Header("Access Control")] // header
        [SerializeField] private bool useACL;
        protected override bool UseACL => useACL;

        [Tooltip("ACL used to check who can use the toggle")]
        [SerializeField] private AccessControl accessControl;
        protected override AccessControl AccessControl
        {
            get => accessControl;
            set => accessControl = value;
        }

        [Header("External")] // header
        [SerializeField] private UdonBehaviour externalBehaviour;

        [SerializeField] private string externalBool = "";
        [SerializeField] private string externalEvent = "";

        [Header("Debug")] // header
        [SerializeField] private DebugLog debugLog;
        protected override DebugLog DebugLog
        {
            get => debugLog;
            set => debugLog = value;
        }
        protected override string LogPrefix => nameof(LocalToggle);

        private bool _isOn;

        public bool State => _isOn;

        public const int EVENT_UPdATE = 0;
        public const int EVENT_COUNT = 1;

        protected override int EventCount => EVENT_COUNT;

        void Start()
        {
            _EnsureInit();
        }

        protected override void _Init()
        {

            _isOn = defaultValue;
            _UpdateState();
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

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (!player.isLocal || !usePersistence) return;

            bool storedState = false;
            if (PlayerData.TryGetBool(player, dataKey, out bool boolValue))
            {
                storedState = boolValue;
                // persistenceLoaded = true;
            }
            else
            {
                return;
            }

            _isOn = storedState;
            _UpdateState();
        }

        public override void Interact()
        {
            if (useACL && !isAuthorized) return;

            _isOn = !_isOn;
            _UpdateState();
        }

        private void _UpdateState()
        {
            if (usePersistence)
            {
                PlayerData.SetBool(dataKey, _isOn);
            }

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
    }
}