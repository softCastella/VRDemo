using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public static class HandPoseCaptureSession
{
    public const string DefaultPoseFolder = "Assets/Poses";

    public static bool IsListening { get; set; }

    public static Handedness CaptureHandedness { get; set; } = Handedness.Left;

    public static string NextPoseName { get; set; } = "HandPose";

    public static string StatusMessage { get; private set; } = "대기 중";

    public static event Action<HandPoseSnapshot> PoseCaptured;

    public static void SetStatus(string message)
    {
        StatusMessage = message;
    }

    public static void NotifyPoseCaptured(HandPoseSnapshot snapshot)
    {
        PoseCaptured?.Invoke(snapshot);
    }
}

public readonly struct HandPoseSnapshot
{
    public HandPoseSnapshot(string poseName, Handedness handedness, IReadOnlyList<HandBonePoseData> bones)
    {
        PoseName = poseName;
        Handedness = handedness;
        Bones = bones;
    }

    public string PoseName { get; }
    public Handedness Handedness { get; }
    public IReadOnlyList<HandBonePoseData> Bones { get; }
}
