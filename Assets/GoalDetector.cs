
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

    [Header("Goal UI")]
    [SerializeField] private TMP_Text goalMessage;
    [SerializeField] private float goalDisplayDuration = 2f;

    private bool goalAlreadyScored;
    private Coroutine resetRoutine;

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

        return ball.x > minX &&
               ball.x < maxX &&
               ball.y < bar.y;
    }

    private IEnumerator ResetAfterGoal()
    {
        // Let the player see the goal message.
        yield return new WaitForSecondsRealtime(goalDisplayDuration);

        ResetBall();
        ResetPlayer();

        if (goalMessage != null)
            goalMessage.gameObject.SetActive(false);

        goalAlreadyScored = false;
        resetRoutine = null;

        Debug.Log("Ready for the next penalty!");
    }

    private void ResetBall()
    {
        if (ballRigidbody == null || ballStartPosition == null)
        {
            Debug.LogWarning(
                "GoalDetector: Assign the ball Rigidbody and ball start position."
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
                "GoalDetector: Assign the player root and player start position."
            );
            return;
        }

        // Temporarily disable a CharacterController if one is present.
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
