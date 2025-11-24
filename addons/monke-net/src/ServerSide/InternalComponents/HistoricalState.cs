// HistoricalState.cs
using Godot;

namespace MonkeNet.Server
{
    // Simple struct to hold the data we need to store per tick
    public struct HistoricalState
    {
        public Vector3 Position;
        public Basis Rotation; // Store rotation if hitboxes rotate with the player model

        public HistoricalState(Transform3D transform)
        {
            Position = transform.Origin;
            Rotation = transform.Basis;
        }
    }
}
