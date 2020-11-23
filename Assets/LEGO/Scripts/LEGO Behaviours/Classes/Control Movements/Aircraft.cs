using LEGOModelImporter;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.LEGO.Behaviours.Controls
{
    public class Aircraft : MonoBehaviour, IControl
    {
        public bool IsActive { get; set; }

        ModelGroup m_Group;

        Vector3 m_BrickPivotOffset;
        Vector3 m_CurrentVelocity;

        bool m_RotationEnabled;
        bool m_CameraAlignedRotation;
        bool m_UseRollCompensation = true;


        void Update()
        {
            if (IsActive)
            {
                if (!m_RotationEnabled)
                {
                    m_RotationEnabled = true;
                }
            }
        }

        public void Setup(ModelGroup group, HashSet<Brick> bricks, Vector3 brickPivotOffset, Bounds scopedBounds, bool cameraAlignedRotation, bool cameraRelativeMovement)
        {
            m_Group = group;
            m_BrickPivotOffset = brickPivotOffset;
            m_CameraAlignedRotation = cameraAlignedRotation;
        }

        public void Movement(Vector3 velocity)
        {
            m_CurrentVelocity = velocity;

            // Move bricks.
            m_Group.transform.position += velocity * Time.deltaTime;
        }

        public void Rotation(float rotationSpeed)
        {
            if (m_RotationEnabled)
            {
                var forward = transform.forward;
                var right = transform.right;
                var up = transform.up;

                var movingDirection = m_CameraAlignedRotation ? Camera.main.transform.forward : m_CurrentVelocity.normalized;

                var currentMagnitude = m_CurrentVelocity.magnitude;

                var movingXZ = new Vector3(movingDirection.x, 0.0f, movingDirection.z);
                var forwardXZ = new Vector3(forward.x, 0.0f, forward.z);

                var pitchAngle = Vector3.SignedAngle(movingDirection, forward, right);
                var jawAngle = Vector3.SignedAngle(forwardXZ, movingXZ, up);

                var currentRoll = Vector3.MoveTowards(up, Vector3.up, 40.0f * Time.deltaTime);
                var rollAngle = Vector3.SignedAngle(up, currentRoll, forward);

                // Rotate bricks.
                var worldPivot = transform.position + transform.TransformVector(m_BrickPivotOffset);
                if (currentMagnitude != 0.0f)
                {
                    m_Group.transform.RotateAround(worldPivot, right, pitchAngle * rotationSpeed * Time.deltaTime);
                    m_Group.transform.RotateAround(worldPivot, up, jawAngle * rotationSpeed * Time.deltaTime);

                    if (m_UseRollCompensation)
                    {
                        m_Group.transform.RotateAround(worldPivot, forward, rollAngle * Time.deltaTime);
                    }
                }
            }
        }

        public void Collision(Vector3 direction)
        {
        }

        public void SynchronizedUpdate()
        {
        }
    }
}
