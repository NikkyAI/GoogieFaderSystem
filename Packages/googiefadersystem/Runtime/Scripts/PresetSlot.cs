
using Texel;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

// ReSharper disable ArrangeObjectCreationWhenTypeEvident

namespace GoogieFaderSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PresetSlot : UdonSharpBehaviour
    {
        public AccessControl accessControl;
        public PresetController presetController;

        [SerializeField] private InteractTrigger clearButton;
        [SerializeField] private InteractTrigger saveButton;
        [SerializeField] private InteractTrigger applyButton;
        [SerializeField] private InteractTrigger debugButton;
        [SerializeField] private TextMeshPro labelText;

        [UdonSynced] public string _jsonFaders = "{}";
        [UdonSynced] public string _jsonToggles = "{}";
        private DataDictionary _faderValues = new DataDictionary();
        private DataDictionary _toggleValues = new DataDictionary();

        private bool _isAuthorized;

        void Start()
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
                LogError("No access control set on SimpleGlobalToggle");
            }

            OnDeserialization();
        }

        public void _OnValidate()
        {
            // Log("_OnValidate");
            _isAuthorized = accessControl._LocalHasAccess();
            // DisableInteractive = !_isAuthorized;

            // UpdateInteractive();
            debugButton.DisableInteractive = !_isAuthorized;
            applyButton.DisableInteractive = !_isAuthorized;
            saveButton.DisableInteractive = !_isAuthorized;
            clearButton.DisableInteractive = !_isAuthorized;
        }

        public void InitButtonsAndLabels(string identifier)
        {
            if (labelText)
                labelText.text = identifier;
            if (applyButton)
                applyButton.InteractionText = $"Apply Preset {identifier}";
            if (saveButton)
                saveButton.InteractionText = $"Save Preset {identifier}";
            if (clearButton)
                clearButton.InteractionText = $"Clear Preset {identifier}";
        }

        private void UpdateInteractive()
        {
            var valuesCount = _faderValues.Count + _toggleValues.Count;
            Log("values count: " + valuesCount);
            // applyButton.DisableInteractive = !(_isAuthorized && valuesCount > 0);
            // saveButton.DisableInteractive = !(_isAuthorized && valuesCount == 0);
            // clearButton.DisableInteractive = !(_isAuthorized && valuesCount > 0);
            applyButton.gameObject.SetActive(valuesCount > 0);
            clearButton.gameObject.SetActive(valuesCount > 0);
            saveButton.gameObject.SetActive(valuesCount == 0);
            debugButton.gameObject.SetActive(valuesCount > 0);
        }

        public override void OnPreSerialization()
        {
            if (VRCJson.TrySerializeToJson(_faderValues, JsonExportType.Minify, out DataToken resultFaders))
            {
                _jsonFaders = resultFaders.String;
            }
            else
            {
                Debug.LogError(resultFaders.ToString());
            }

            if (VRCJson.TrySerializeToJson(_toggleValues, JsonExportType.Minify, out DataToken resultToggles))
            {
                _jsonToggles = resultToggles.String;
            }
            else
            {
                Debug.LogError(resultToggles.ToString());
            }
        }

        public override void OnDeserialization()
        {
            if (VRCJson.TryDeserializeFromJson(_jsonFaders, out DataToken resultFaders))
            {
                _faderValues = resultFaders.DataDictionary;
            }
            else
            {
                Debug.LogError(resultFaders.ToString());
            }

            if (VRCJson.TryDeserializeFromJson(_jsonToggles, out DataToken resultToggles))
            {
                _toggleValues = resultToggles.DataDictionary;
            }
            else
            {
                Debug.LogError(resultToggles.ToString());
            }

            UpdateInteractive();
        }

        public void OnSave()
        {
            if (!presetController) return;

            foreach (var fader in presetController.faders)
            {
                var key = fader.Key;
                //var key = fader.gameObject.GetInstanceID();
                Log("Saving Fader" + key + " = " + fader.Value);
                _faderValues.SetValue(key, new DataToken(fader.Value));
            }

            foreach (var toggle in presetController.toggles)
            {
                var key = toggle.Key;
                // var key = toggle.gameObject.GetInstanceID();
                Log("Saving Toggle" + key + " = " + toggle.ButtonState);
                _toggleValues.SetValue(key, new DataToken(toggle.ButtonState));
            }

            RequestSerialization();
            UpdateInteractive();
        }

        public void OnClear()
        {
            _faderValues.Clear();
            _toggleValues.Clear();

            RequestSerialization();
            UpdateInteractive();
        }

        public void OnApply()
        {
            // if (!isAuthorized) return;
            if (!presetController) return;

            Log($"Apply preset {name}");

            var localPlayer = Networking.LocalPlayer;

            foreach (var fader in presetController.faders)
            {
                var key = fader.Key;
                Log($"loading value for {key}");

                if (_faderValues.ContainsKey(key))
                {
                    Networking.SetOwner(Networking.LocalPlayer, fader.gameObject);
                    var token = _faderValues[key];
                    Log($"{key} = {token}");
                    var newValue = token.Float;
                    fader.SetValue(newValue);
                }
                else
                {
                    LogError($"no value found for {key}");
                }

            }

            foreach (var toggle in presetController.toggles)
            {
                var key = toggle.Key;
                Log($"loading value for {key}");

                if (_toggleValues.ContainsKey(key))
                {
                    Networking.SetOwner(Networking.LocalPlayer, toggle.gameObject);
                    var token = _toggleValues[key];
                    Log($"{key} = {token}");
                    var newValue = token.Boolean;
                    toggle.SetState(newValue);
                }
                else
                {
                    LogError($"no value found for {key}");
                }
            }

        }

        public void OnDebug()
        {
            if (!_isAuthorized) return;
            if (!presetController) return;

            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ShowDebug));
        }

        public void ShowDebug()
        {
            if (!_isAuthorized) return;
            if (!presetController) return;

            presetController.SetDebug(
                _faderValues,
                _toggleValues
            );
        }

        private const string logPrefix = "[PresetSlot]";

        private void LogError(string message)
        {
            Debug.LogError($"{logPrefix} {message}");
            // if (Utilities.IsValid(debugLog))
            // {
            //     debugLog._WriteError(
            //         $"{logPrefix} {materialPropertyId}",
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
            //         $"{logPrefix} {materialPropertyId}",
            //         message
            //     );
            // }
        }
    }
}