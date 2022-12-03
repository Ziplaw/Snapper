using UnityEngine;

namespace Snapper
{
    public sealed class SnapData : ScriptableObject
    {
        [field: SerializeField] public string Name;
        [field: SerializeField] public Texture2D Snapshot { get; set; }

        [field: SerializeField] public Vector3 PivotPosition { get; set; }
        [field: SerializeField] public float CameraDistance { get; set; }
        [field: SerializeField] public Vector3 RotationEulerAngles { get; set; }

        public void Setup( string _name, Texture2D _snapshot, Vector3 _pivotPosition, Vector3 _rotationEulerAngles, float _cameraDistance )
        {
            Name = _name;
            Snapshot = _snapshot;
            PivotPosition = _pivotPosition;
            RotationEulerAngles = _rotationEulerAngles;
            CameraDistance = _cameraDistance;
        }
    }
}