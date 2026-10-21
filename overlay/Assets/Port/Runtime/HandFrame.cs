using UnityEngine;

namespace HandPlayground
{
    public enum TrackerStatus { Stopped = 0, Starting = 1, Running = 2, CameraError = 3, ModelError = 4, PermissionDenied = 5, Unavailable = 6 }

    /// <summary>One inference result. Arrays are reused: never keep a reference across frames.</summary>
    public sealed class HandFrame
    {
        public const int Landmarks = 21;
        public int frameId;
        public TrackerStatus status;
        public float timestampMs, inferenceMs, fovH = 65f, fovV = 50f, aspect = 4f / 3f, analysisFps;
        public int handCount;
        public readonly bool[] present = new bool[2];
        public readonly int[] label = new int[2];     // 0 = "Left", 1 = "Right" as reported by MediaPipe
        public readonly float[] score = new float[2];
        public readonly Vector3[][] norm = { new Vector3[Landmarks], new Vector3[Landmarks] };   // x,y in [0,1] image coords (y down), z relative
        public readonly Vector3[][] world = { new Vector3[Landmarks], new Vector3[Landmarks] };  // metres, hand-centred, x right / y down / z away
    }

    public interface IHandSource
    {
        TrackerStatus Status { get; }
        /// <summary>Returns true and fills <paramref name="f"/> when a new inference result is available.</summary>
        bool TryGetFrame(HandFrame f);
        void Start();
        void Stop();
    }
}
