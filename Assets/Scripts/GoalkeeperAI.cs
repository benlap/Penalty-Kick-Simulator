
using System.Collections;
using UnityEngine;

public class GoalkeeperAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PenaltyPlayerController playerController;
    [SerializeField] private Rigidbody ballRigidbody;

    [Tooltip("Goal line reference. Local X = goal width, Y = up, Z = depth.")]
    [SerializeField] private Transform goalPlane;

    [Tooltip("The goalkeeper's movement root, not the animated mesh.")]
    [SerializeField] private Transform goalkeeperRoot;

    [SerializeField] private GoalDetector goalDetector;

    [Header("AI Settings")]
    [SerializeField, Min(0f)] private float reactionDelay = 0.2f;
    [SerializeField, Min(0.01f)] private float diveDuration = 0.35f;
    [SerializeField, Min(0f)] private float predictionTimeLimit = 2f;

    [Tooltip("Maximum horizontal distance the goalkeeper can move from its starting position, in meters.")]
    [SerializeField, Min(0f)] private float maxDiveDistance = 1.5f;

    [Tooltip("Random horizontal error added to the predicted ball position, in goal-local units.")]
    [SerializeField, Min(0f)] private float aimError = 0.35f;

    [Tooltip("Random multiplier applied to the calculated dive distance. Lower values make dives less accurate.")]
    [SerializeField, Range(0f, 1f)] private float diveAccuracy = 0.85f;

    [Header("Round Reset")]
    [Tooltip("How long after a shot before resetting if no goal was detected.")]
    [SerializeField, Min(0.1f)] private float roundResetDelay = 3f;

    [Header("Animation (Optional)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string leftDiveTrigger = "DiveLeft";
    [SerializeField] private string rightDiveTrigger = "DiveRight";

    private Vector3 readyPosition;
    private Quaternion readyRotation;

    private bool isReacting;
    private Coroutine diveRoutine;
    private Coroutine roundResetRoutine;

    public bool IsReacting => isReacting;

    private void Awake()
    {
        if (goalkeeperRoot == null)
            goalkeeperRoot = transform;

        readyPosition = goalkeeperRoot.position;
        readyRotation = goalkeeperRoot.rotation;
    }

    private void OnEnable()
    {
        if (playerController != null)
            playerController.OnShotTaken += ReactToShot;
    }

    private void OnDisable()
    {
        if (playerController != null)
            playerController.OnShotTaken -= ReactToShot;

        StopActiveCoroutines();
    }

    private void ReactToShot(float shotSpeed, Vector3 aimPoint)
    {
        if (isReacting || ballRigidbody == null || goalPlane == null)
            return;

        if (roundResetRoutine != null)
            StopCoroutine(roundResetRoutine);

        roundResetRoutine = StartCoroutine(ResetRoundAfterDelay());
        diveRoutine = StartCoroutine(ReactAndDive());
    }

    private IEnumerator ReactAndDive()
    {
        isReacting = true;

        if (reactionDelay > 0f)
            yield return new WaitForSeconds(reactionDelay);

        Vector3 predictedPoint;

        if (TryPredictGoalPoint(out predictedPoint))
        {
            // Add unpredictable horizontal error to the prediction.
            predictedPoint.x += Random.Range(-aimError, aimError);

            // Only use the ball's horizontal position to choose a dive.
            float horizontalOffset =
                predictedPoint.x -
                goalPlane.InverseTransformPoint(readyPosition).x;

            // Scale the dive to simulate imperfect reactions.
            horizontalOffset *= Random.Range(diveAccuracy, 1f);

            horizontalOffset = Mathf.Clamp(
                horizontalOffset,
                -maxDiveDistance,
                maxDiveDistance
            );

            TriggerDiveAnimation(horizontalOffset);

            yield return MoveHorizontally(horizontalOffset);
        }

        diveRoutine = null;
    }

    private bool TryPredictGoalPoint(out Vector3 predictedPoint)
    {
        predictedPoint = Vector3.zero;

        Vector3 localPosition =
            goalPlane.InverseTransformPoint(ballRigidbody.position);

        Vector3 localVelocity =
            goalPlane.InverseTransformDirection(ballRigidbody.linearVelocity);

        // Ball must be moving toward the goal plane.
        if (Mathf.Abs(localVelocity.z) < 0.01f)
            return false;

        float timeToGoal = -localPosition.z / localVelocity.z;

        if (timeToGoal <= 0f || timeToGoal > predictionTimeLimit)
            return false;

        Vector3 predictedWorldPosition =
            ballRigidbody.position +
            ballRigidbody.linearVelocity * timeToGoal +
            0.5f * Physics.gravity * timeToGoal * timeToGoal;

        predictedPoint =
            goalPlane.InverseTransformPoint(predictedWorldPosition);

        return true;
    }

    private void TriggerDiveAnimation(float horizontalOffset)
    {
        if (animator == null)
            return;

        string trigger = horizontalOffset < 0f
            ? leftDiveTrigger
            : rightDiveTrigger;

        if (!string.IsNullOrEmpty(trigger))
            animator.SetTrigger(trigger);
    }

    private IEnumerator MoveHorizontally(float horizontalOffset)
    {
        Vector3 startPosition = goalkeeperRoot.position;

        // Convert the horizontal goal-local offset into world space.
        Vector3 worldOffset = goalPlane.right * horizontalOffset;

        // Preserve the original height and depth exactly.
        Vector3 endPosition = startPosition + worldOffset;
        endPosition.y = startPosition.y;

        // Prevent movement along the goal's depth axis.
        Vector3 localStart = goalPlane.InverseTransformPoint(startPosition);
        Vector3 localEnd = goalPlane.InverseTransformPoint(endPosition);
        localEnd.z = localStart.z;
        localEnd.y = localStart.y;
        endPosition = goalPlane.TransformPoint(localEnd);

        float elapsed = 0f;

        while (elapsed < diveDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / diveDuration);
            t = t * t * (3f - 2f * t);

            goalkeeperRoot.position =
                Vector3.Lerp(startPosition, endPosition, t);

            // Keep the goalkeeper's original rotation.
            goalkeeperRoot.rotation = readyRotation;

            yield return null;
        }

        goalkeeperRoot.position = endPosition;
        goalkeeperRoot.rotation = readyRotation;
        diveRoutine = null;
    }

    private IEnumerator ResetRoundAfterDelay()
    {
        yield return new WaitForSecondsRealtime(roundResetDelay);

        roundResetRoutine = null;

        if (goalDetector != null && goalDetector.GoalAlreadyScored)
            yield break;

        if (goalDetector != null)
        {
            goalDetector.ResetRound();
        }
        else
        {
            ResetGoalkeeper();

            if (ballRigidbody != null)
            {
                ballRigidbody.linearVelocity = Vector3.zero;
                ballRigidbody.angularVelocity = Vector3.zero;
            }

            Debug.LogWarning(
                "GoalkeeperAI: No GoalDetector assigned. " +
                "Only the goalkeeper and ball velocity were reset."
            );
        }
    }

    public void ResetGoalkeeper()
    {
        if (diveRoutine != null)
        {
            StopCoroutine(diveRoutine);
            diveRoutine = null;
        }

        isReacting = false;

        if (goalkeeperRoot != null)
        {
            goalkeeperRoot.SetPositionAndRotation(
                readyPosition,
                readyRotation
            );
        }

        ResetAnimationTriggers();
    }

    private void ResetAnimationTriggers()
    {
        if (animator == null)
            return;

        if (!string.IsNullOrEmpty(leftDiveTrigger))
            animator.ResetTrigger(leftDiveTrigger);

        if (!string.IsNullOrEmpty(rightDiveTrigger))
            animator.ResetTrigger(rightDiveTrigger);
    }

    private void StopActiveCoroutines()
    {
        if (diveRoutine != null)
        {
            StopCoroutine(diveRoutine);
            diveRoutine = null;
        }

        if (roundResetRoutine != null)
        {
            StopCoroutine(roundResetRoutine);
            roundResetRoutine = null;
        }

        isReacting = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (goalPlane == null)
            return;

        Vector3 center = goalPlane.position;
        float size = 2f;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(center, goalPlane.right * size);

        Gizmos.color = Color.green;
        Gizmos.DrawRay(center, goalPlane.up * size);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(center, goalPlane.forward * size);

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(center, 0.1f);
    }
}
