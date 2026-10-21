
using System.Collections;
using UnityEngine;

public class PenaltyPlayerController : MonoBehaviour
{
    public enum PlayerState
    {
        Ready,
        Charging,
        Kicking,
        ShotTaken
    }
    

    [Header("References")]
    [SerializeField] private PenaltyGameStateController gameStateController;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Transform ball;
    [SerializeField] private Rigidbody ballRigidbody;
    [SerializeField] private Transform goal;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform aimReticle;
    [SerializeField] private LineRenderer aimLine;
    
    [Header("Shot Power Audio")]
    [SerializeField] private ShotPowerTickAudio shotPowerTickAudio;

    [Header("Goal Target Area")]
    [Tooltip("Half-width of the aimable area, in goal local units.")]
    [SerializeField] private float goalHalfWidth = 3.5f;

    [Tooltip("Height of the bottom of the aimable area, relative to goal position.")]
    [SerializeField] private float minAimHeight = 0.2f;

    [Tooltip("Height of the top of the aimable area, relative to goal position.")]
    [SerializeField] private float maxAimHeight = 2.4f;

    [Header("Shot Power")]
    [SerializeField] private float minShotSpeed = 12f;
    [SerializeField] private float maxShotSpeed = 28f;

    [Header("Sweet Spot")]
    [SerializeField, Range(0f, 1f)]
    private float sweetSpotCenter = 0.75f;

    [SerializeField, Range(0.01f, 0.25f)]
    private float sweetSpotHalfWidth = 0.05f;

    public float LastShotPower01 { get; private set; }
    public float LastShotQuality01 { get; private set; }
    public bool LastShotWasPerfect { get; private set; }

    [Header("Kick Audio")]
    [SerializeField] private AudioSource kickAudioSource;
    [SerializeField] private AudioClip kickSound;
    [SerializeField, Range(0f, 1f)] private float kickVolume = 1f;

    public event System.Action<bool, float> OnShotQualityEvaluated;


    [Header("Power Timing")]
    [SerializeField] private float powerCycleDuration = 2f;
    [SerializeField] private bool startAtRandomPower = false;

    [Header("Kick")]
    [Tooltip("Delay before launching the ball. Match this to the kick animation.")]
    [SerializeField] private float kickDelay = 0.25f;

    [Tooltip("Animator trigger parameter for the kick animation.")]
    [SerializeField] private string kickTrigger = "Kick";

    [Header("Input")]
    [SerializeField] private int shootMouseButton = 0;
    [SerializeField] private bool lockCursor = false;

    public PlayerState State { get; private set; } = PlayerState.Ready;
    public float CurrentPower01 { get; private set; }
    public Vector3 CurrentAimPoint { get; private set; }

    public event System.Action<float, Vector3> OnShotTaken;

    private float chargeTimer;
    private Coroutine kickRoutine;
    private Plane aimPlane;

    private void Awake()
    {
        if (aimCamera == null)
            aimCamera = Camera.main;

        if (ballRigidbody == null && ball != null)
            ballRigidbody = ball.GetComponent<Rigidbody>();

        if (ball == null && ballRigidbody != null)
            ball = ballRigidbody.transform;

        if (ball == null || ballRigidbody == null || goal == null)
        {
            Debug.LogError(
                $"{nameof(PenaltyPlayerController)} on {name}: " +
                "Assign a ball Rigidbody and goal Transform.", this);

            enabled = false;
            return;
        }

        if (aimCamera == null)
        {
            Debug.LogError("Assign an aim camera.", this);
            enabled = false;
            return;
        }

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (State == PlayerState.Kicking ||
            State == PlayerState.ShotTaken)
        {
            UpdateAimVisuals(false);
            return;
        }

        UpdateAimPoint();

        if (State == PlayerState.Ready &&
            Input.GetMouseButtonDown(shootMouseButton))
        {
            BeginCharging();
        }

        if (State == PlayerState.Charging)
        {
            UpdateCharging();

            if (Input.GetMouseButtonUp(shootMouseButton))
                ReleaseShot();
        }

        UpdateAimVisuals(true);
    }

    private void UpdateAimPoint()
    {
        // Aim on a plane running through the goal.
        // The goal's local X axis defines its width,
        // and its local Y axis defines its height.
        aimPlane = new Plane(
            goal.forward,
            goal.position
        );

        Ray ray = aimCamera.ScreenPointToRay(Input.mousePosition);

        if (aimPlane.Raycast(ray, out float distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            Vector3 localOffset = goal.InverseTransformPoint(hitPoint);

            localOffset.x = Mathf.Clamp(
                localOffset.x, -goalHalfWidth, goalHalfWidth);

            localOffset.y = Mathf.Clamp(
                localOffset.y, minAimHeight, maxAimHeight);

            localOffset.z = 0f;

            CurrentAimPoint = goal.TransformPoint(localOffset);
        }
        else
        {
            CurrentAimPoint = goal.position + goal.up;
        }

        // Prevent aiming below the configured target area.
        Vector3 localAim = goal.InverseTransformPoint(CurrentAimPoint);
        localAim.x = Mathf.Clamp(localAim.x, -goalHalfWidth, goalHalfWidth);
        localAim.y = Mathf.Clamp(localAim.y, minAimHeight, maxAimHeight);
        localAim.z = 0f;
        CurrentAimPoint = goal.TransformPoint(localAim);
    }

    private void BeginCharging()
    {
        State = PlayerState.Charging;

        if (startAtRandomPower)
        {
            CurrentPower01 = Random.value;
            chargeTimer = CurrentPower01 * powerCycleDuration;
        }
        else
        {
            CurrentPower01 = 0f;
            chargeTimer = 0f;
        }
    }

    private void UpdateCharging()
    {
        if (powerCycleDuration <= 0f)
        {
            CurrentPower01 = 1f;
            return;
        }

        chargeTimer += Time.deltaTime;

        // Normalize time to a repeating 0-2 range.
        float cycle = Mathf.Repeat(
            chargeTimer / powerCycleDuration,
            2f
        );

        // Ping-pong between 0 and 1.
        CurrentPower01 = cycle <= 1f
            ? cycle
            : 2f - cycle;

        shotPowerTickAudio?.UpdatePower(CurrentPower01);
    }

    private void ReleaseShot()
    {
        if (State != PlayerState.Charging)
            return;

        State = PlayerState.Kicking;

        LastShotPower01 = CurrentPower01;

        float distanceFromSweetSpot =
            Mathf.Abs(LastShotPower01 - sweetSpotCenter);

        LastShotWasPerfect =
            distanceFromSweetSpot <= sweetSpotHalfWidth;

        LastShotQuality01 = Mathf.Clamp01(
            1f - distanceFromSweetSpot / 0.25f
        );

        OnShotQualityEvaluated?.Invoke(
            LastShotWasPerfect,
            LastShotQuality01
        );


        float shotSpeed = Mathf.Lerp(
            minShotSpeed,
            maxShotSpeed,
            CurrentPower01
        );

        if (animator != null && !string.IsNullOrEmpty(kickTrigger))
            animator.SetTrigger(kickTrigger);

        kickRoutine = StartCoroutine(
            PerformKick(shotSpeed, CurrentAimPoint)
        );
    }

    private IEnumerator PerformKick(float shotSpeed, Vector3 aimPoint)
    {
        shotPowerTickAudio?.StopTicking();

        if (kickDelay > 0f)
            yield return new WaitForSeconds(kickDelay);

        if (ballRigidbody == null)
        {
            State = PlayerState.ShotTaken;
            yield break;
        }

        gameStateController.ShowGoalMade();

        ballRigidbody.isKinematic = false;

        ballRigidbody.linearVelocity = Vector3.zero;
        ballRigidbody.angularVelocity = Vector3.zero;

        Vector3 direction = (aimPoint - ball.position).normalized;

        // Apply the new shot velocity.
        ballRigidbody.linearVelocity = direction * shotSpeed;
        ballRigidbody.angularVelocity =
            Vector3.Cross(Vector3.up, direction) * shotSpeed * 0.8f;


        // Give the shot a minimum upward component so it
        // doesn't travel flat into the ground.
        if (direction.y < 0.05f)
            direction.y = 0.05f;

        direction.Normalize();

        ballRigidbody.linearVelocity = direction * shotSpeed;

        // Add spin around the ball's local side axis.
        ballRigidbody.angularVelocity =
            Vector3.Cross(Vector3.up, direction) * shotSpeed * 0.8f;

        State = PlayerState.ShotTaken;

        OnShotTaken?.Invoke(shotSpeed, aimPoint);

        kickRoutine = null;

        if (kickAudioSource != null && kickSound != null)
            kickAudioSource.PlayOneShot(kickSound, kickVolume);
    }

    

    private void UpdateAimVisuals(bool visible)
    {
        if (aimReticle != null)
        {
            aimReticle.gameObject.SetActive(visible);

            if (visible)
                aimReticle.position = CurrentAimPoint;
        }

        if (aimLine != null)
        {
            aimLine.enabled = visible;

            if (visible)
            {
                aimLine.positionCount = 2;
                aimLine.SetPosition(0, ball.position);
                aimLine.SetPosition(1, CurrentAimPoint);
            }
        }
    }

    /// <summary>
    /// Call this after the shot has been resolved to prepare
    /// the player for another attempt.
    /// Reset the ball's position separately if needed.
    /// </summary>
    public void ResetForNextShot()
    {
        if (kickRoutine != null)
        {
            StopCoroutine(kickRoutine);
            kickRoutine = null;
        }

        chargeTimer = 0f;
        CurrentPower01 = 0f;
        State = PlayerState.Ready;

        if (ballRigidbody != null)
        {
            ballRigidbody.linearVelocity = Vector3.zero;
            ballRigidbody.angularVelocity = Vector3.zero;
        }

        if (animator != null)
        {
            animator.ResetTrigger(kickTrigger);
        }
    }

    private void OnDisable()
    {
        if (kickRoutine != null)
        {
            StopCoroutine(kickRoutine);
            kickRoutine = null;
        }

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
