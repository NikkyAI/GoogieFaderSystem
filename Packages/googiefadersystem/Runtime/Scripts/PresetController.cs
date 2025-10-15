
using System;
using System.Linq;
using Texel;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace GoogieFaderSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PresetController : UdonSharpBehaviour
    {
        [SerializeField] private AccessControl accessControl;

        [Tooltip("objects that contain ShaderFaders and SimpleGLobalToggles")] [SerializeField]
        private GameObject[] objects;
        // [SerializeField] private GameObject presetSlotHolder;

        [SerializeField] public ShaderFader[] faders;
        [SerializeField] public SyncedToggle[] toggles;

        [FormerlySerializedAs("debugState")] [SerializeField]
        DebugState debugStateFaders;

        [SerializeField] DebugState debugStateToggles;
        [HideInInspector] public DataDictionary debugFaderValues = new DataDictionary();
        [HideInInspector] public DataDictionary debugToggleValues = new DataDictionary();

        void Start()
        {
            var presetSlots = gameObject.GetComponentsInChildren<PresetSlot>(true);
            int i = 1;
            foreach (var presetSlot in presetSlots)
            {
                presetSlot.presetController = this;
                presetSlot.accessControl = accessControl;
                presetSlot.InitButtonsAndLabels($"{i++}");
            }

            if (debugStateFaders)
            {
                debugStateFaders._Register(DebugState.EVENT_UPDATE, this, nameof(_InternalUpdateDebugStateFaders));
                debugStateFaders._SetContext(this, nameof(_InternalUpdateDebugStateFaders), "Faders");
            }

            if (debugStateToggles)
            {
                debugStateToggles._Register(DebugState.EVENT_UPDATE, this, nameof(_InternalUpdateDebugStateToggles));
                debugStateToggles._SetContext(this, nameof(_InternalUpdateDebugStateToggles), "Toggles");
            }
        }

        public void _InternalUpdateDebugStateFaders()
        {
            foreach (var key in debugFaderValues.GetKeys().ToArray())
            {
                var token = debugFaderValues[key];
                debugStateFaders._SetValue(key.ToString(), token.ToString());
            }
        }

        public void _InternalUpdateDebugStateToggles()
        {
            foreach (var key in debugToggleValues.GetKeys().ToArray())
            {
                var token = debugToggleValues[key];
                debugStateToggles._SetValue(key.ToString(), token.ToString());
            }
        }

        public void SetDebug(DataDictionary debugfaderValues, DataDictionary debugtoggleValues)
        {
            this.debugFaderValues = debugfaderValues;
            this.debugToggleValues = debugtoggleValues;
        }

        [NonSerialized] private GameObject[] prevObjects;
        [NonSerialized] private AccessControl prevAccessControl;
        [NonSerialized] private bool childrenInitialized = false;
#if UNITY_EDITOR && !COMPILER_UDONSHARP

        private void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);

            if (objects != null && prevObjects != null && !objects.SequenceEqual(prevObjects))
            {
                // To prevent trying to apply the theme to often, as without it every single change in the scene causes it to be applied
                prevObjects = objects;

                FindAllFadersAndButtons();
            }

            if (!childrenInitialized)
            {
                InitChildren();
                childrenInitialized = true;
            }
        }

        private void FindAllFadersAndButtons()
        {
            faders = objects
                .SelectMany(o => o.GetComponentsInChildren<ShaderFader>(true))
                .Distinct()
                .ToArray();
            toggles = objects
                .SelectMany(o => o.GetComponentsInChildren<SyncedToggle>(true))
                .Distinct()
                .ToArray();
            this.MarkDirty();
        }

        [ContextMenu("Init Children")]
        private void InitChildren()
        {
            var presetSlots = gameObject.GetComponentsInChildren<PresetSlot>(true);
            var i = 0;
            foreach (var presetSlot in presetSlots)
            {
                presetSlot.presetController = this;
                presetSlot.accessControl = accessControl;

                presetSlot.transform.localPosition = new Vector3(i * 0.045f, 0, 0);

                presetSlot.MarkDirty();
                i++;
            }

            // for (int i = 0; i < presetSlotHolder.transform.childCount; i++)
            // {
            //     var presetSlotTransform = presetSlotHolder.transform.GetChild(i);
            //     
            // }
            // Transform t = presetSlotHolder.transform.childCount;
            //
            // if (t.gameObject)
            //
            //     print(t.name); 
        }
#endif
    }
}
