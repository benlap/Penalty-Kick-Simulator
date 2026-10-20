
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

    [Header("Dive Targets")]
    [SerializeField] private Transform leftDiveTarget;
    [SerializeField] private Transform centerDiveTarget;
    [SerializeField] private Transform rightDiveTarget;

    [Header("AI Settings")]
    [SerializeField, Min(0f)] private float reactionDelay = 0.2f;
    [SerializeField, Min(0.01f)] private float diveDuration = 0.35f;
    [SerializeField, Min(0f)] private float predictionTimeLimit = 2f;
    [SerializeField, Min(0f)] private float saveReach = 0.8f;

    [Tooltip("Random error added to the predicted target, in goal-local units.")]
    [SerializeField, Range(0f, 1f)] private float aimError = 0.12f;

    [Header("Round Reset")]
    [Tooltip("How long after a shot before resetting if no goal was detected.")]
    [SerializeField, Min(0.1f)] private float roundResetDelay = 3f;

    [Header("Animation (Optional)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string leftDiveTrigger = "DiveLeft";
    [SerializeField] private string rightDiveTrigger = "DiveRight";
    [SerializeField] private string centerSaveTrigger = "SaveCenter";

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
            predictedPoint.x += Random.Range(-aimError, aimError);
            predictedPoint.y += Random.Range(-aimError, aimError);

            Transform target = ChooseDiveTarget(predictedPoint);

            if (target != null)
            {
                TriggerDiveAnimation(predictedPoint);
                yield return MoveToTarget(target);
            }
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

        if (Mathf.Abs(localVelocity.z) < 0.01f)
            return false;

        // Goal plane is local Z = 0.
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

    private Transform ChooseDiveTarget(Vector3 predictedPoint)
    {
        if (predictedPoint.x < -saveReach)
            return leftDiveTarget;

        if (predictedPoint.x > saveReach)
            return rightDiveTarget;

        return centerDiveTarget;
    }

    private void TriggerDiveAnimation(Vector3 predictedPoint)
    {
        if (animator == null)
            return;

        string trigger;

        if (predictedPoint.x < -saveReach)
            trigger = leftDiveTrigger;
        else if (predictedPoint.x > saveReach)
            trigger = rightDiveTrigger;
        else
            trigger = centerSaveTrigger;

        if (!string.IsNullOrEmpty(trigger))
            animator.SetTrigger(trigger);
    }

    private IEnumerator MoveToTarget(Transform target)
    {
        Vector3 startPosition = goalkeeperRoot.position;
        Quaternion startRotation = goalkeeperRoot.rotation;

        Vector3 endPosition = target.position;
        Quaternion endRotation = target.rotation;

        float elapsed = 0f;

        while (elapsed < diveDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / diveDuration);
            t = t * t * (3f - 2f * t);

            goalkeeperRoot.SetPositionAndRotation(
                Vector3.Lerp(startPosition, endPosition, t),
                Quaternion.Slerp(startRotation, endRotation, t)
            );

            yield return null;
        }

        goalkeeperRoot.SetPositionAndRotation(endPosition, endRotation);
        diveRoutine = null;
    }

    private IEnumerator ResetRoundAfterDelay()
    {
        yield return new WaitForSecondsRealtime(roundResetDelay);

        roundResetRoutine = null;

        // If the goal detector is already showing a goal,
        // let its goal-display reset flow finish instead.
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

        if (!string.IsNullOrEmpty(centerSaveTrigger))
            animator.ResetTrigger(centerSaveTrigger);
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
