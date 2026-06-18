using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

[CreateAssetMenu(fileName = "NewHandPose", menuName = "Hand Pose/Hand Pose Data")]
public class HandPoseData : ScriptableObject
{
    [SerializeField]
    string m_PoseName;

    [SerializeField]
    Handedness m_Handedness = Handedness.Left;

    [SerializeField]
    string m_CapturedAtUtc;

    [SerializeField]
    List<HandBonePoseData> m_Bones = new();

    [SerializeField]
    bool m_NormalizedToWristOrigin;

    public string PoseName => m_PoseName;
    public Handedness Handedness => m_Handedness;
    public string CapturedAtUtc => m_CapturedAtUtc;
    public IReadOnlyList<HandBonePoseData> Bones => m_Bones;
    public bool NormalizedToWristOrigin => m_NormalizedToWristOrigin;

    public bool IsWristAtOrigin()
    {
        if (!HandPoseBoneHierarchy.TryGetWristBoneName(m_Bones, out var wristBoneName))
            return m_NormalizedToWristOrigin;

        foreach (var bone in m_Bones)
        {
            if (bone.boneName != wristBoneName)
                continue;

            return bone.localPosition.sqrMagnitude <= 1e-8f;
        }

        return false;
    }

    public bool NeedsWristOriginMigration() => m_Bones.Count > 0 && !IsWristAtOrigin();

    public void SetPose(string poseName, Handedness handedness, IReadOnlyList<HandBonePoseData> bones, bool normalizedToWristOrigin = true)
    {
        m_PoseName = poseName;
        m_Handedness = handedness;
        m_CapturedAtUtc = System.DateTime.UtcNow.ToString("o");
        m_NormalizedToWristOrigin = normalizedToWristOrigin;
        m_Bones.Clear();
        m_Bones.AddRange(bones);
    }

    public bool TryMigrateLegacyToWristOrigin()
    {
        if (!NeedsWristOriginMigration())
            return false;

        var normalized = HandPoseNormalizer.NormalizeLegacyBones(m_Bones);
        if (normalized.Count == 0)
            return false;

        m_Bones.Clear();
        m_Bones.AddRange(normalized);
        m_NormalizedToWristOrigin = true;
        return true;
    }
}
