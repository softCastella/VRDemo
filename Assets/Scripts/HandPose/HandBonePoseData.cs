using System;
using UnityEngine;
using UnityEngine.XR.Hands;

[Serializable]
public struct HandBonePoseData
{
    public XRHandJointID jointId;
    public string boneName;
    public Vector3 localPosition;
    public Quaternion localRotation;
}
