using UnityEngine;
using UnityEngine.InputSystem;
using Room0317.Apartment;
using Room0317.Audio;

namespace Room0317.Player
{
    /// <summary>
    /// Top-down 4-directional movement for Adrian.
    /// Drives the Animator by playing one of 8 states:
    /// Idle_Down / Idle_Up / Idle_Left / Idle_Right / Walk_Down / Walk_Up / Walk_Left / Walk_Right.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    public class AdrianController : MonoBehaviour
    {
        public enum Facing { Down, Up, Left, Right }

        [SerializeField] private float moveSpeed = 2.6f;
        [SerializeField] private Facing startFacing = Facing.Down;

        private Rigidbody2D rb;
        private Animator animator;
        private Vector2 moveInput;
        private Facing facing;
        private string currentState;
        private Vector2 lastStepCheckPos;
        private float stepDistanceAccum = 0.68f;
        private const float StepStrideDistance = 0.95f;

        /// <summary>When false, input is ignored and Adrian idles (e.g. during dialogue or room transitions).</summary>
        public bool CanMove { get; set; } = true;

        public void SetCanMove(bool canMove) => CanMove = canMove;

        public Facing CurrentFacing => facing;

        /// <summary>Instantly positions Adrian, stops momentum, and sets facing direction.</summary>
        public void Teleport(Vector2 position, Facing newFacing)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            if (rb != null)
            {
                rb.position = position;
                rb.linearVelocity = Vector2.zero;
            }
            lastStepCheckPos = position;
            stepDistanceAccum = 0.68f;
            facing = newFacing;
            PlayState("Idle_" + facing);
        }

        public void SetFacing(Facing newFacing)
        {
            facing = newFacing;
            PlayState("Idle_" + facing);
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            facing = startFacing;
            lastStepCheckPos = transform.position;
        }

        private void Update()
        {
            moveInput = CanMove ? ReadMoveInput() : Vector2.zero;

            bool moving = moveInput.sqrMagnitude > 0.01f;
            if (moving)
            {
                // Dominant axis decides which way Adrian faces.
                if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
                    facing = moveInput.x > 0f ? Facing.Right : Facing.Left;
                else
                    facing = moveInput.y > 0f ? Facing.Up : Facing.Down;
            }

            PlayState((moving ? "Walk_" : "Idle_") + facing);
            UpdateFootsteps(moving);
        }

        private void UpdateFootsteps(bool movingInputActive)
        {
            Vector2 currentPos = transform.position;
            float frameDist = Vector2.Distance(currentPos, lastStepCheckPos);
            lastStepCheckPos = currentPos;

            if (!CanMove || !movingInputActive || Time.timeScale <= 0.01f || frameDist < 0.0005f)
            {
                // Keep slight lead-in ready so the first step plays promptly upon resuming movement
                stepDistanceAccum = 0.68f;
                return;
            }

            // Clamp large teleports/hitches just in case
            if (frameDist > 0.5f) return;

            stepDistanceAccum += frameDist;
            if (stepDistanceAccum >= StepStrideDistance)
            {
                stepDistanceAccum -= StepStrideDistance;
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayFootstep(GetCurrentRoomSurface());
                }
            }
        }

        private static FootstepSurface GetCurrentRoomSurface()
        {
            string roomId = ApartmentManager.Instance != null && ApartmentManager.Instance.CurrentRoom != null
                ? ApartmentManager.Instance.CurrentRoom.RoomId
                : string.Empty;

            if (roomId == RoomIds.Bathroom) return FootstepSurface.Tile;
            if (roomId == RoomIds.Hallway || roomId == RoomIds.Kitchen) return FootstepSurface.Wood;
            return FootstepSurface.Carpet;
        }

        private void FixedUpdate()
        {
            rb.linearVelocity = moveInput * moveSpeed;
        }

        private void PlayState(string state)
        {
            if (state == currentState) return;
            float normalizedTime = 0f;
            if (!string.IsNullOrEmpty(currentState) && currentState.StartsWith("Walk_") && state.StartsWith("Walk_"))
            {
                // Preserve walk stride cycle across direction changes to avoid leg snapping
                normalizedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
            }
            currentState = state;
            animator.Play(state, 0, normalizedTime);
        }

        private static Vector2 ReadMoveInput()
        {
            Vector2 input = Vector2.zero;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
            }

            var pad = Gamepad.current;
            if (pad != null && input == Vector2.zero)
            {
                input = pad.leftStick.ReadValue();
                if (input.sqrMagnitude < 0.04f) input = Vector2.zero;
            }

            return input.sqrMagnitude > 1f ? input.normalized : input;
        }
    }
}
