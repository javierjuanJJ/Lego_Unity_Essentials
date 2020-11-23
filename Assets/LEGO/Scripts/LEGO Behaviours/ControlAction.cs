using Unity.LEGO.Behaviours.Actions;
using Unity.LEGO.Behaviours.Controls;
using Unity.LEGO.Game;
using UnityEngine;

namespace Unity.LEGO.Behaviours {
    public class ControlAction : MovementAction
    {
        public enum ControlType
        {
            Hovercraft,
            Aircraft,
            Character
        }

        [SerializeField, Tooltip("Control like a hovercraft.\nor\nControl like an aircraft.\nor\nControl like a character.")]
        ControlType m_ControlType = ControlType.Hovercraft;

        enum InputType
        {
            Tank,
            Direct,
            Strafe
        }

        [SerializeField, Tooltip("Turn relative to self.\nor\nTurn relative to direction.\nor\nTurn relative to camera.")]
        InputType m_InputType = InputType.Direct;

        [SerializeField, Range(1, 30), Tooltip("The speed in LEGO modules per second.")]
        int m_Speed = 20;
        [SerializeField, Range(0, 720), Tooltip("The rotation speed in degrees per second.")]
        int m_RotationSpeed = 360;

        [SerializeField, Tooltip("Make other bricks behave as if this is the player.")]
        bool m_IsPlayer = true;

        [SerializeField, Tooltip("Always move in the forward direction.")]
        bool m_AlwaysMovingForward = false;

        bool m_CanMoveOnY;
        bool m_UseAcceleration = true;
        bool m_CameraRelativeMovement;
        bool m_CameraAlignedRotation;

        float m_NormalizedAcceleration = 2.0f; // It will take 1.0f / m_NormalizedAcceleration seconds to fully stop.

        IControl m_ControlMovement;

        Vector3 m_CurrentDirection;
        Vector3 m_TargetDirection;


        enum State
        {
            Moving,
            Bouncing
        }

        State m_State = State.Moving;

        protected override void Reset()
        {
            base.Reset();

            m_Repeat = false;
            m_IconPath = "Assets/LEGO/Gizmos/LEGO Behaviour Icons/Control Action.png";
        }

        protected override void Start()
        {
            base.Start();
            
            if (IsPlacedOnBrick())
            {
                SetupInputType();

                AddControlMovement();

                if (m_IsPlayer)
                {
                    // Tag all the part colliders to make other LEGO Behaviours act as if this is the player.
                    foreach (var brick in m_ScopedBricks)
                    {
                        foreach (var part in brick.parts)
                        {
                            foreach (var collider in part.colliders)
                            {
                                collider.gameObject.tag = "Player";
                                collider.gameObject.layer = LayerMask.NameToLayer("Player");
                            }
                        }
                    }
                }
            }
        }

        protected void Update()
        {
            if (m_Active)
            {
                // Update time.
                m_CurrentTime += Time.fixedDeltaTime;

                // Activate control movement.
                m_ControlMovement.IsActive = true;

                HandleInput();

                if (m_UseAcceleration)
                {
                    var speedDiff = m_TargetDirection - m_CurrentDirection;
                    if (speedDiff.sqrMagnitude < m_NormalizedAcceleration * m_NormalizedAcceleration * Time.deltaTime * Time.deltaTime)
                    {
                        m_CurrentDirection = m_TargetDirection;
                    }
                    else if (speedDiff.sqrMagnitude > 0.0f)
                    {
                        speedDiff.Normalize();

                        m_CurrentDirection += speedDiff * m_NormalizedAcceleration * Time.deltaTime;
                    }
                }
                else
                {
                    m_CurrentDirection = m_TargetDirection;
                }

                if (m_State == State.Moving)
                {
                    if (!IsColliding())
                    {
                        var currentVelocity = m_CurrentDirection * m_Speed * LEGOHorizontalModule;

                        if (m_AlwaysMovingForward)
                        {
                            // Move forward with half the top speed.
                            currentVelocity += transform.forward * m_Speed * 0.5f * LEGOHorizontalModule;
                        }

                        // Move and rotate bricks.
                        m_ControlMovement.Movement(currentVelocity);
                        m_ControlMovement.Rotation(m_RotationSpeed);
                    }
                    else
                    {
                        m_CurrentDirection = Vector3.zero;
                        m_CurrentTime = 0.0f;
                        m_State = State.Bouncing;
                    }
                }
                else if (m_State == State.Bouncing)
                {
                    // Slight delay before you can move again.
                    if (m_CurrentTime >= 0.1f)
                    {
                        m_CurrentTime -= 0.1f;
                        m_State = State.Moving;
                    }
                }

                // Perform synchronized update on control movement.
                m_ControlMovement.SynchronizedUpdate();

                // Update model position.
                m_MovementTracker.UpdateModelPosition();
            }
        }

        protected override bool IsColliding()
        {
            if (base.IsColliding())
            {
                foreach (var activeColliderPair in m_ActiveColliderPairs)
                {
                    if (Physics.ComputePenetration(activeColliderPair.Item1, activeColliderPair.Item1.transform.position, activeColliderPair.Item1.transform.rotation,
                        activeColliderPair.Item2, activeColliderPair.Item2.transform.position, activeColliderPair.Item2.transform.rotation,
                        out Vector3 direction, out _))
                    {
                        m_ControlMovement.Collision(direction);

                        if (Vector3.Dot(direction, m_CurrentDirection) < -0.0001f)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        void SetupInputType()
        {
            switch (m_InputType)
            {
                case InputType.Tank:
                    m_CameraRelativeMovement = false;
                    m_CameraAlignedRotation = false;
                    break;
                case InputType.Direct:
                    m_CameraRelativeMovement = true;
                    m_CameraAlignedRotation = false;
                    break;
                case InputType.Strafe:
                    m_CameraRelativeMovement = true;
                    m_CameraAlignedRotation = true;
                    break;
            }
        }

        void AddControlMovement()
        {
            switch (m_ControlType)
            {
                case ControlType.Hovercraft:
                    m_ControlMovement = gameObject.AddComponent<Hovercraft>();
                    m_CanMoveOnY = true;
                    break;
                case ControlType.Aircraft:
                    m_ControlMovement = gameObject.AddComponent<Aircraft>();
                    m_CanMoveOnY = true;
                    break;
                case ControlType.Character:
                    m_ControlMovement = gameObject.AddComponent<Character>();
                    break;
            }

            m_ControlMovement.Setup(m_Group, m_ScopedBricks, m_BrickPivotOffset, m_ScopedBounds, m_CameraAlignedRotation, m_CameraRelativeMovement);

            var animateBehaviour = GetComponent<IAnimate>();
            animateBehaviour?.AnimationSetup(m_scopedPartRenderers);
        }

        void HandleInput()
        {
            var right = transform.right;
            var forward = transform.forward;

            if (m_CameraRelativeMovement)
            {
                right = Camera.main.transform.right;
                right.y = 0.0f;
                right.Normalize();
                forward = Camera.main.transform.forward;
                forward.y = 0.0f;
                forward.Normalize();
            }

            m_TargetDirection = m_InputType == InputType.Tank ? Vector3.zero : right * Input.GetAxisRaw("Horizontal");
            m_TargetDirection += forward * Input.GetAxisRaw("Vertical");
            if (m_TargetDirection.sqrMagnitude > 0.0f)
            {
                m_TargetDirection.Normalize();
            }

            // Move up or down with half speed.
            if (m_CanMoveOnY)
            {
                if (Input.GetButton("Fire1"))
                {
                    m_TargetDirection += Vector3.up * 0.5f;
                }
                if (Input.GetButton("Fire2"))
                {
                    m_TargetDirection += Vector3.down * 0.5f;
                }
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (m_IsPlayer)
            {
                GameOverEvent evt = Events.GameOverEvent;
                evt.Win = false;
                EventManager.Broadcast(evt);
            }

            m_ControlMovement.IsActive = false;
        }
    }
}
