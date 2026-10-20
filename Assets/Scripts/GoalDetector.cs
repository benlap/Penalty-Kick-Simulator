
using System.Collections;
using TMPro;
using UnityEngine;

public class GoalDetector : MonoBehaviour
{
    [Header("Goal Bounds")]
    [SerializeField] private Transform leftPost;
    [SerializeField] private Transform rightPost;
    [SerializeField] private Transform crossbar;

    [Header("Ball")]
    [SerializeField] private string ballTag = "Ball";
    [SerializeField] private Rigidbody ballRigidbody;
    [SerializeField] private Transform ballStartPosition;

    [Header("Player Reset")]
    [SerializeField] private PenaltyPlayerController playerController;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform playerStartPosition;

    [Header("Goalkeeper Reset")]
    [SerializeField] private GoalkeeperAI goalkeeperAI;

    [Header("Goal UI")]
    [SerializeField] private TMP_Text goalMessage;
    [SerializeField, Min(0f)] private float goalDisplayDuration = 2f;

    private bool goalAlreadyScored;
    private Coroutine resetRoutine;

    public bool GoalAlreadyScored => goalAlreadyScored;

    private void Start()
    {
        if (goalMessage != null)
            goalMessage.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (goalAlreadyScored || !other.CompareTag(ballTag))
            return;

        if (!IsInsideGoal(other.bounds.center))
            return;

        goalAlreadyScored = true;

        Debug.Log("GOAL!");

        if (goalMessage != null)
        {
            goalMessage.text = "GOAL!";
            goalMessage.gameObject.SetActive(true);
            goalMessage.transform.SetAsLastSibling();
        }

        if (resetRoutine != null)
            StopCoroutine(resetRoutine);

        resetRoutine = StartCoroutine(ResetAfterGoal());
    }

    private bool IsInsideGoal(Vector3 ballPosition)
    {
        if (leftPost == null || rightPost == null || crossbar == null)
        {
            Debug.LogWarning(
                "GoalDetector: Assign the left post, right post, and crossbar."
            );
            return false;
        }

        Vector3 left = transform.InverseTransformPoint(leftPost.position);
        Vector3 right = transform.InverseTransformPoint(rightPost.position);
        Vector3 bar = transform.InverseTransformPoint(crossbar.position);
        Vector3 ball = transform.InverseTransformPoint(ballPosition);

        float minX = Mathf.Min(left.x, right.x);
        float maxX = Mathf.Max(left.x, right.x);

        float ballRadius = 0.11f;

        if (ballRigidbody != null)
        {
            SphereCollider sphere =
                ballRigidbody.GetComponent<SphereCollider>();

            if (sphere != null)
            {
                Vector3 scale = sphere.transform.lossyScale;
                ballRadius = sphere.radius *
                    Mathf.Max(scale.x, scale.y, scale.z);
            }
        }

        return ball.x > minX + ballRadius &&
               ball.x < maxX - ballRadius &&
               ball.y < bar.y - ballRadius;
    }

    private IEnumerator ResetAfterGoal()
    {
        yield return new WaitForSecondsRealtime(goalDisplayDuration);

        ResetRound();

        resetRoutine = null;

        Debug.Log("Ready for the next penalty!");
    }

    /// <summary>
    /// Resets the ball, player, goalkeeper, and goal UI.
    /// Can also be called by GoalkeeperAI after a missed or saved shot.
    /// </summary>
    public void ResetRound()
    {
        ResetBall();
        ResetPlayer();

        if (goalkeeperAI != null)
            goalkeeperAI.ResetGoalkeeper();

        if (goalMessage != null)
            goalMessage.gameObject.SetActive(false);

        goalAlreadyScored = false;
    }

    private void ResetBall()
    {
        if (ballRigidbody == null || ballStartPosition == null)
        {
            Debug.LogWarning(
                "GoalDetector: Assign Ball Rigidbody and Ball Start Position."
            );
            return;
        }

        ballRigidbody.linearVelocity = Vector3.zero;
        ballRigidbody.angularVelocity = Vector3.zero;

        ballRigidbody.position = ballStartPosition.position;
        ballRigidbody.rotation = ballStartPosition.rotation;

        ballRigidbody.Sleep();
    }

    private void ResetPlayer()
    {
        if (playerRoot == null || playerStartPosition == null)
        {
            Debug.LogWarning(
                "GoalDetector: Assign Player Root and Player Start Position."
            );
            return;
        }

        CharacterController characterController =
            playerRoot.GetComponent<CharacterController>();

        if (characterController != null)
            characterController.enabled = false;

        playerRoot.SetPositionAndRotation(
            playerStartPosition.position,
            playerStartPosition.rotation
        );

        if (characterController != null)
            characterController.enabled = true;

        if (playerController != null)
            playerController.ResetForNextShot();
    }

    public void ResetGoal()
    {
        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
            resetRoutine = null;
        }

        goalAlreadyScored = false;

        if (goalMessage != null)
            goalMessage.gameObject.SetActive(false);
    }
}
