using System;
using Texel;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace GoogieFaderSystem
{
    
    public abstract class ACLBase: BaseBehaviour
    {
        // protected AccessControl accessControl;
        protected virtual AccessControl AccessControl { get; set; }
        protected abstract bool UseACL { get; }

        protected bool isAuthorized;

        protected override void _PostInit()
        {
            base._PostInit();
            
            if (UseACL)
            {
                if (AccessControl)
                {
                    AccessControl._Register(AccessControl.EVENT_VALIDATE, this, nameof(_OnValidate));
                    AccessControl._Register(AccessControl.EVENT_ENFORCE_UPDATE, this, nameof(_OnValidate));

                    _OnValidate();
                }
                else
                {
                    LogError("No ACL set");
                    isAuthorized = false;
                    AccessChanged();
                }
            }
            else
            {
                isAuthorized = true;
                AccessChanged();
            }
        }

        public void _OnValidate()
        {
            bool oldAuth = this.isAuthorized;
            isAuthorized = AccessControl._LocalHasAccess();
            if (isAuthorized != oldAuth)
            {
                Log($"setting isAuthorized to {this.isAuthorized} for {Networking.LocalPlayer.displayName}");
            }
            AccessChanged();
        }

        protected abstract void AccessChanged();


#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public virtual AccessControl EditorACL
        {
            get => AccessControl;
            set => AccessControl = value;
        }
#endif
    }
}