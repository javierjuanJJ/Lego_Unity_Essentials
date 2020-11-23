using LEGOModelImporter;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.LEGO.Behaviours.Controls
{
    public class Hovercraft : MonoBehaviour, IControl
    {
        public bool IsActive { get; set; }

        ModelGroup m_Group;

        Vector3 m_BrickPivotOffset;
        Vector3 m_MovementVelocity;
        Vector3 m_CollisionVelocity;

        float m_CollisionAcceleration = 25.0f;

        bool m_CameraRelativeMovement;
        bool m_CameraAlignedRotation;

        public void Setup(ModelGroup group, HashSet<Brick> bricks, Vector3 brickPivotOffset, Bounds scopedBounds, bool cameraAlignedRotation, bool cameraRelativeMovement)
        {
            m_Group = group;
            m_BrickPivotOffset = brickPivotOffset;
            m_CameraAlignedRotation = cameraAlignedRotation;
            m_CameraRelativeMovement = cameraRelativeMovement;
        }

        public void Movement(Vector3 velocity)
        {
            m_MovementVelocity = velocity;

            // Move bricks.
            m_Group.transform.position += velocity * Time.deltaTime;
        }

        public void Rotation(float rotationSpeed)
        {
            float angleDiff;

            if (m_CameraAlignedRotation)
            {
                var pointingDirection = new Vector3(transform.forward.x, 0.0f, transform.forward.z);
                pointingDirection = pointingDirection.normalized;

                var forwardXZ = new Vector3(Camera.main.transform.forward.x, 0.0f, Camera.main.transform.forward.z);

                angleDiff = Vector3.SignedAngle(pointingDirection, forwardXZ, Vector3.up);
            }
            else if (m_CameraRelativeMovement)
            {
                var pointingDirection = new Vector3(m_MovementVelocity.x, 0.0f, m_MovementVelocity.z);
                pointingDirection = pointingDirection.normalized;

                var forwardXZ = new Vector3(transform.forward.x, 0.0f, transform.forward.z);

                angleDiff = Vector3.SignedAngle(forwardXZ, pointingDirection, Vector3.up);
            }
            else
            {
                angleDiff = Input.GetAxisRaw("Horizontal") * rotationSpeed;
            }

            if (angleDiff < 0.0f)
            {
                rotationSpeed = -rotationSpeed;
            }

            // Assumes that x > NaN is false - otherwise we need to guard against Time.deltaTime being zero.
            if (Mathf.Abs(rotationSpeed) > Mathf.Abs(angleDiff) / Time.deltaTime)
            {
                rotationSpeed = angleDiff / Time.deltaTime;
            }

            // Rotate bricks.
            var worldPivot = transform.position + transform.TransformVector(m_BrickPivotOffset);
            m_Group.transform.RotateAround(worldPivot, Vector3.up, rotationSpeed * Time.deltaTime);
        }

        public void Collision(Vector3 direction)
        {
            if (m_MovementVelocity.magnitude > 0.0f)
            {
                m_CollisionVelocity = Vector3.Reflect(m_MovementVelocity, direction);
            }
        }

        public void SynchronizedUpdate()
        {
            if (IsActive)
            {
                var speedDiff = Vector3.zero - m_CollisionVelocity;
                if (speedDiff.sqrMagnitude < m_CollisionAcceleration * m_CollisionAcceleration * Time.deltaTime * Time.deltaTime)
                {
                    m_CollisionVelocity = Vector3.zero;
                }
                else if (speedDiff.sqrMagnitude > 0.0f)
                {
                    speedDiff.Normalize();

                    m_CollisionVelocity += speedDiff * m_CollisionAcceleration * Time.deltaTime;
                }

                // Move bricks.
                m_Group.transform.position += m_CollisionVelocity * Time.deltaTime;
            }
        }
    }
}
