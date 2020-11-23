using LEGOModelImporter;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.LEGO.Behaviours.Controls
{
    public interface IControl
    {
        void Setup(ModelGroup group, HashSet<Brick> bricks, Vector3 brickPivotOffset, Bounds scopedBounds, bool cameraAlignedRotation, bool cameraRelativeMovement);
        void Movement(Vector3 velocity);
        void Rotation(float rotationSpeed);
        void SynchronizedUpdate();
        void Collision(Vector3 direction);
        bool IsActive
        {
            get;
            set;
        }
    }
}
