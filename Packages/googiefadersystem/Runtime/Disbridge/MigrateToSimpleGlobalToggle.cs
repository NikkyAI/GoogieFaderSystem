//
// using UdonSharp;
// using UdonVR.DisBridge.Plugins;
// using UnityEngine;
// using VRC;
// using VRC.SDKBase;
// using VRC.Udon;
// using VRC.Core;
//
// namespace GoogieFaderSystem
// {
//     public class MigrateToSimpleGlobalToggle : UdonSharpBehaviour
//     {
// #if UNITY_EDITOR && !COMPILER_UDONSHARP
//         [ContextMenu("Migrate")]
//         public void Migrate()
//         {
//
//             if (Application.isPlaying) return;
//             UnityEditor.EditorUtility.SetDirty(this);
//
//             var dbGlobalToggles = GetComponentsInChildren<DisbridgeGlobalToggle>(true);
//             foreach (var dbGLobalToggle in dbGlobalToggles)
//             {
//                 var gameObject = dbGLobalToggle.gameObject;
//                 gameObject.AddComponent<SyncedToggle>();
//
//                 var simpleGlobalToggle = gameObject.GetComponent<SyncedToggle>();
//                 if (simpleGlobalToggle == null)
//                 {
//                     Debug.LogWarning(
//                         $"[MigrateToSimpleGLobalToggle] failed to add new component on {this.GetInstanceID()}");
//                     continue;
//                 }
//
//                 simpleGlobalToggle.targetsOn = dbGLobalToggle._targets;
//                 simpleGlobalToggle.targetsOff = dbGLobalToggle._targets2;
//                 simpleGlobalToggle.label = dbGLobalToggle.InteractionText;
//                 simpleGlobalToggle.MarkDirty();
//             }
//         }
// #endif
//     }
// }
