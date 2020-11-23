using System.Collections.Generic;
using System.Linq;
using LEGOModelImporter;
using UnityEngine;

namespace Unity.LEGO.Behaviours.Controls
{
    public class Character : MonoBehaviour, IControl, IAnimate
    {
        public bool IsActive { get; set; }

        ModelGroup m_Group;
        HashSet<Brick> m_Bricks;
        Bounds m_Bounds;
        LayerMask m_LayerMask;

        float m_JumpSpeed = 8.0f;
        float m_JumpCooldown = 1.0f;
        float m_JumpTimer;
        float m_BoundsPointsHeight = 1.2f; // This height controls how tall bumps the character can go over
        float m_RaycastExtension = 0.2f;
        float m_AnimationTime;
        float m_AnimationDuration = 0.4f;
        float m_InAirTimer;

        Vector3 m_CurrentVelocity;
        Vector3 m_BrickPivotOffset;
        Vector3 m_Pivot;
        Vector3 m_JumpVector;
        Vector3 m_JumpMomentumModifier = new Vector3(0.0f, -0.5f, 0.0f);
        Vector3 m_Gravity = new Vector3(0.0f, -12.0f, 0.0f); // Adjust gravity to adjust fall speed and (de)acceleration
        Vector3 m_Scale = Vector3.one;

        Vector3[] localBoundsPoints = new Vector3[5];
        Vector3[] worldBoundsPoints = new Vector3[5];

        HashSet<float> hitDistances = new HashSet<float>();
        HashSet<float> hitsToUse = new HashSet<float>();

        bool m_IsInJump;
        bool m_DrawCornerGizmos = false; // TODO: Remove later
        bool m_OnGround = true;
        bool m_PrevOnGround;
        bool m_CameraRelativeRotation;

        enum AnimationState
        {
            InAnimation,
            NotAnimating
        }

        AnimationState m_AnimationState = AnimationState.NotAnimating;

        List<Shader> m_OriginalShaders = new List<Shader>();
        List<Material> m_Materials = new List<Material>();

        static readonly int s_DeformMatrix1ID = Shader.PropertyToID("_DeformMatrix1");
        static readonly int s_DeformMatrix2ID = Shader.PropertyToID("_DeformMatrix2");
        static readonly int s_DeformMatrix3ID = Shader.PropertyToID("_DeformMatrix3");


        void Start()
        {
            m_LayerMask = LayerMask.GetMask("Environment");
            m_LayerMask |= LayerMask.GetMask("Default");

            m_JumpSpeed += m_Bounds.extents.y;

            FindLocalBottomCornerBounds();
        }

        public void SynchronizedUpdate()
        {
            if (IsActive)
            {
                BoundsTransformations();

                m_OnGround = IsOnGroundCheck();

                if (m_OnGround && Input.GetButtonDown("Jump"))
                {
                    if (!m_IsInJump)
                    {
                        Jump();
                    }
                }

                if (!m_OnGround)
                {
                    m_InAirTimer += Time.deltaTime;

                    if (m_InAirTimer >= 0.2f || m_IsInJump)
                    {
                        InAir();
                    }
                }
                else
                {
                    if (m_PrevOnGround != m_OnGround && m_InAirTimer >= 0.05f)
                    {
                        PlayAnimation();
                    }

                    m_InAirTimer = 0.0f;
                }


                m_PrevOnGround = m_OnGround;

                AnimationLoop();

                m_JumpTimer += Time.deltaTime;

                if (m_IsInJump)
                {
                    if (m_JumpTimer >= m_JumpCooldown)
                    {
                        m_IsInJump = false;
                    }
                }
            }
        }

        public void Setup(ModelGroup group, HashSet<Brick> bricks, Vector3 brickPivotOffset, Bounds scopedBounds, bool rotationBool, bool controlBool)
        {
            m_Group = group;
            m_Bricks = bricks;
            m_Bounds = scopedBounds;
            m_BrickPivotOffset = brickPivotOffset;
            m_CameraRelativeRotation = rotationBool;
        }

        public void AnimationSetup(List<MeshRenderer> scopedPartRenderers)
        {
            // Change the shader of all scoped part renderers.
            foreach (var partRenderer in scopedPartRenderers)
            {
                m_OriginalShaders.Add(partRenderer.sharedMaterial.shader);

                // The renderQueue value is reset when changing the shader, so transfer it.
                var renderQueue = partRenderer.material.renderQueue;
                partRenderer.material.shader = Shader.Find("Deformed");
                partRenderer.material.renderQueue = renderQueue;

                m_Materials.Add(partRenderer.material);
            }

            m_Pivot = transform.InverseTransformVector(new Vector3(m_Bounds.center.x, m_Bounds.min.y, m_Bounds.center.z) - transform.position);
        }

        public void Movement(Vector3 velocity)
        {
            if (IsActive)
            {
                m_CurrentVelocity = velocity;

                // Move bricks.
                m_Group.transform.position += velocity * Time.deltaTime;
            }
        }

        public void Rotation(float rotationSpeed)
        {
            if (IsActive)
            {
                var movingDirection = m_CameraRelativeRotation ? new Vector3(Camera.main.transform.forward.x, 0.0f, Camera.main.transform.forward.z) :
                    new Vector3(m_CurrentVelocity.normalized.x, 0.0f, m_CurrentVelocity.normalized.z);

                var forward = new Vector3(transform.forward.x, 0.0f, transform.forward.z);
                var angle = Vector3.SignedAngle(forward, movingDirection, Vector3.up);
                var worldPivot = transform.position + transform.TransformVector(m_BrickPivotOffset);

                // Rotate bricks.
                m_Group.transform.RotateAround(worldPivot, Vector3.up, angle * (rotationSpeed * 7.0f) * Time.deltaTime);
            }
        }

        void AnimationLoop()
        {
            if (m_AnimationState != AnimationState.NotAnimating)
            {

                m_AnimationTime += Time.deltaTime;

                if (m_AnimationState == AnimationState.InAnimation)
                {
                    Animation(m_AnimationTime);
                }

                var worldPivot = transform.position + transform.TransformVector(m_Pivot);

                var deformMatrix = Matrix4x4.Translate(worldPivot) * Matrix4x4.Scale(m_Scale) * Matrix4x4.Translate(-worldPivot);

                foreach (var material in m_Materials)
                {
                    material.SetVector(s_DeformMatrix1ID, deformMatrix.GetRow(0));
                    material.SetVector(s_DeformMatrix2ID, deformMatrix.GetRow(1));
                    material.SetVector(s_DeformMatrix3ID, deformMatrix.GetRow(2));
                }

                if (m_AnimationTime >= m_AnimationDuration)
                {
                    m_AnimationTime = 0.0f;
                    m_AnimationState = AnimationState.NotAnimating;
                }
            }
        }

        void PlayAnimation()
        {
            m_AnimationTime = 0.0f;
            m_Scale = Vector3.one;
            m_AnimationState = AnimationState.InAnimation;
        }

        void Animation(float time)
        {
            var clampedTime = Mathf.Min(1.0f, time / m_AnimationDuration);
            m_Scale.x = 1.0f + Mathf.Clamp(Mathf.Sin(clampedTime * Mathf.PI), -1.0f, 1.0f) * 0.1f;
            m_Scale.y = 1.0f - Mathf.Clamp(Mathf.Sin(clampedTime * Mathf.PI), -1.0f, 1.0f) * 0.1f;
            m_Scale.z = 1.0f + Mathf.Clamp(Mathf.Sin(clampedTime * Mathf.PI), -1.0f, 1.0f) * 0.1f;
        }

        public void Collision(Vector3 direction)
        {
            if (direction.y >= 0.90f)
            {
                m_InAirTimer = 0.0f; // Stops InAir loop
            }
            else
            {
                m_IsInJump = false;
            }
        }

        void Jump()
        {
            PlayAnimation();

            m_JumpVector = new Vector3(0.0f, m_JumpSpeed, 0.0f);

            m_JumpTimer = 0.0f;

            m_IsInJump = true;

            InAir(); // Call InAir to start jump this frame
        }

        void InAir()
        {
            var inAirVector = m_Gravity * Time.deltaTime;

            if (m_IsInJump)
            {
                if (m_JumpVector.y > m_Gravity.y)
                {
                    m_JumpVector += m_JumpMomentumModifier;
                }

                inAirVector = m_JumpVector * Time.deltaTime;
            }

            // Move bricks.
            m_Group.transform.position += inAirVector;
        }

        void FindLocalBottomCornerBounds()
        {

            var boundsMin = m_Bounds.min;
            var boundsMax = m_Bounds.max;

            localBoundsPoints[0] = new Vector3(boundsMin.x, boundsMin.y, boundsMax.z);
            localBoundsPoints[1] = new Vector3(boundsMax.x, boundsMin.y, boundsMax.z);
            localBoundsPoints[2] = new Vector3(boundsMin.x, boundsMin.y, boundsMin.z);
            localBoundsPoints[3] = new Vector3(boundsMax.x, boundsMin.y, boundsMin.z);
            localBoundsPoints[4] = new Vector3(m_Bounds.center.x, boundsMin.y, m_Bounds.center.z);

            var yModified = new Vector3(0.0f, m_BoundsPointsHeight, 0.0f);

            for (var i = 0; i < localBoundsPoints.Length; i++)
            {
                localBoundsPoints[i] += yModified;

                localBoundsPoints[i] = transform.InverseTransformPoint(localBoundsPoints[i]);
            }
        }

        void BoundsTransformations()
        {

            var currentDirection = transform.right * Input.GetAxisRaw("Horizontal");
            currentDirection += transform.forward * Input.GetAxisRaw("Vertical");
            currentDirection *= 2.0f;

            for (var i = 0; i < worldBoundsPoints.Length; i++)
            {
                worldBoundsPoints[i] = transform.TransformPoint(localBoundsPoints[i]);

                if (i != worldBoundsPoints.Length - 1)
                {
                    worldBoundsPoints[i] += currentDirection;
                }
            }
        }

        bool IsOnGroundCheck()
        {

            var result = false;

            hitDistances.Clear();
            Collider[] hitColliders = new Collider[5];

            foreach (var point in worldBoundsPoints)
            {

                var inColliders =
                    Physics.OverlapSphereNonAlloc(point, 0.1f, hitColliders, m_LayerMask, QueryTriggerInteraction.Ignore);
                var otherColliders = 0;

                foreach (var collider in hitColliders)
                {
                    if (collider)
                    {
                        if (!m_Bricks.Contains(collider.transform.GetComponentInParent<Brick>()))
                        {
                            otherColliders++;
                        }
                    }
                }

                if (otherColliders == 0)
                {
                    var hits = Physics.RaycastAll(point, Vector3.down, m_BoundsPointsHeight + m_RaycastExtension,
                        m_LayerMask, QueryTriggerInteraction.Ignore);

                    hitsToUse.Clear();

                    foreach (var hit in hits)
                    {
                        if (!m_Bricks.Contains(hit.transform.GetComponentInParent<Brick>()))
                        {
                            hitsToUse.Add(hit.distance);
                        }
                    }

                    if (hitsToUse.Count > 0)
                    {
                        hitDistances.Add(hitsToUse.Min());
                        result = true;
                    }

                    // Debug.DrawRay(point, Vector3.down, Color.magenta);
                }
            }

            if (hitDistances.Count > 0 && !m_IsInJump)
            {
                BumpCheck(hitDistances.Min());
            }

            return result;
        }

        void BumpCheck(float distance)
        {
            var bumpDistance = m_BoundsPointsHeight - distance;

            if (bumpDistance > 0.01f && bumpDistance < m_BoundsPointsHeight)
            {
                var moveDistance = new Vector3(0.0f, bumpDistance, 0.0f);

                // Move bricks.
                m_Group.transform.position += moveDistance;

                if (bumpDistance > m_BoundsPointsHeight / 2.0f)
                {
                    PlayAnimation();
                }
            }
        }

        void OnDrawGizmos() // TODO: Remove later?
        {
            if (m_DrawCornerGizmos && worldBoundsPoints != null)
            {
                Gizmos.color = Color.red;

                for (var i = worldBoundsPoints.Length - 1; i >= 0; i--)
                {
                    Gizmos.DrawWireSphere(worldBoundsPoints[i], 0.05f);
                }
            }
        }
    }
}
