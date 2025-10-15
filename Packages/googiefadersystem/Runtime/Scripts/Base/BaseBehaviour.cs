using System;
using Texel;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace GoogieFaderSystem
{
    public abstract class BaseBehaviour: EventBase
    {
        protected virtual DebugLog DebugLog
        {
            get => null;
            set { }
        }

        protected abstract string LogPrefix { get; }

        protected void LogError(string message)
        {
            Debug.LogError($"[{LogPrefix}] {message}");
            if (Utilities.IsValid(DebugLog))
            {
                DebugLog._WriteError(
                    LogPrefix,
                    message
                );
            }
        }
        protected void LogWarning(string message)
        {
            Debug.LogWarning($"[{LogPrefix}] {message}");
            if (Utilities.IsValid(DebugLog))
            {
                DebugLog._WriteError(
                    LogPrefix,
                    message
                );
            }
        }
        protected void Log(string message)
        {
            Debug.Log($"[{LogPrefix}] {message}");
            if (Utilities.IsValid(DebugLog))
            {
                DebugLog._Write(
                    LogPrefix,
                    message
                );
            }
        }
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public DebugLog EditorDebugLog
        {
            get => DebugLog;
            set => DebugLog = value;
        }
#endif
    }
}