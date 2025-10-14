
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LocalHandCollider : UdonSharpBehaviour
{

    [SerializeField] public GameObject leftHandCollider;
    [SerializeField] public GameObject rightHandCollider;
    private VRCPlayerApi _currentPlayer;
    private Vector3 _leftHandData;
    private Vector3 _rightHandData;
    private Quaternion _leftHandRot;
    private Quaternion _rightHandRot;

    private void Start()
    {
        _currentPlayer = Networking.LocalPlayer;
    }

    public override void PostLateUpdate()
    {
        _rightHandData = _currentPlayer.GetBonePosition(HumanBodyBones.RightIndexDistal);
        _rightHandRot = _currentPlayer.GetBoneRotation(HumanBodyBones.RightIndexDistal);
        if (_rightHandData == Vector3.zero)
        {
            _rightHandData = _currentPlayer.GetBonePosition(HumanBodyBones.RightIndexIntermediate);
        }

        if (_rightHandRot == Quaternion.identity)
        {
            _leftHandRot = _currentPlayer.GetBoneRotation(HumanBodyBones.RightIndexIntermediate);
        }
        _leftHandData = _currentPlayer.GetBonePosition(HumanBodyBones.LeftIndexDistal);
        _leftHandRot = _currentPlayer.GetBoneRotation(HumanBodyBones.LeftIndexDistal);
        if (_leftHandData == Vector3.zero)
        {
            _leftHandData = _currentPlayer.GetBonePosition(HumanBodyBones.LeftIndexIntermediate);
        }
        if (_rightHandRot == Quaternion.identity)
        {
            _leftHandRot = _currentPlayer.GetBoneRotation(HumanBodyBones.LeftIndexIntermediate);
        }

        rightHandCollider.transform.position = _rightHandData; // new Vector3(rightHandData.x, rightHandData.y, rightHandData.z);
        leftHandCollider.transform.position = _leftHandData; // new Vector3(leftHandData.x, leftHandData.y, leftHandData.z);
        rightHandCollider.transform.rotation = _rightHandRot;
        leftHandCollider.transform.rotation = _leftHandRot;
    }
}
