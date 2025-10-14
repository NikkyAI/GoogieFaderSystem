
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace GoogieFaderSystem {
    public class ResetShaderFaders : UdonSharpBehaviour
    {
        public ShaderFader[] shaderFaders;
        public SyncedToggle[] toggles;
        public override void Interact(){
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
