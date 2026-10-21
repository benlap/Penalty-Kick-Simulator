
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PenaltyPowerMeterUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PenaltyPlayerController playerController;
    [SerializeField] private GameObject meterPanel;
    [SerializeField] private Image barFill;
    [SerializeField] private TMP_Text percentageLabel;
    [SerializeField] private TMP_Text shotFeedbackLabel;

    [Header("Display")]
    [SerializeField] private bool showOnlyWhileCharging = true;
    [SerializeField] private Color lowPowerColor =
        new Color(0.25f, 0.75f, 1f);
    [SerializeField] private Color highPowerColor =
        new Color(1f, 0.3f, 0.2f);

    [Header("Shot Feedback")]
    [SerializeField] private float feedbackDuration = 1.2f;
    [SerializeField] private float goodShotThreshold = 0.65f;
    [SerializeField] private Color perfectColor =
        new Color(0.3f, 1f, 0.55f);
    [SerializeField] private Color goodColor =
        new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color weakColor =
        new Color(1f, 0.4f, 0.35f);

    [Header("Instructions")]
    [SerializeField] private CanvasGroup instructionGroup;

    [SerializeField]
    private string instructionText =
        "CLICK AND HOLD TO BUILD POWER\nRELEASE TO SHOOT";
    [SerializeField] private TMPro.TMP_Text instructionLabel;

    private Coroutine feedbackRoutine;

    private void Awake()
    {
        if (meterPanel == null)
            meterPanel = gameObject;

        SetVisible(false);

        if (shotFeedbackLabel != null)
            shotFeedbackLabel.gameObject.SetActive(false);

        UpdateMeter(0f);

        if (instructionLabel != null)
            instructionLabel.text = instructionText;

        ShowInstructions();
    }

    public void ShowInstructions()
    {
        if (instructionGroup != null)
        {
            instructionGroup.alpha = 1f;
            instructionGroup.interactable = false;
            instructionGroup.blocksRaycasts = false;
        }
    }

    public void HideInstructions()
    {
        if (instructionGroup != null)
        {
            instructionGroup.alpha = 0f;
            instructionGroup.interactable = false;
            instructionGroup.blocksRaycasts = false;
        }
    }

    private void OnEnable()
    {
        if (playerController != null)
            playerController.OnShotQualityEvaluated += ShowShotFeedback;
    }

    private void OnDisable()
    {
        if (playerController != null)
            playerController.OnShotQualityEvaluated -= ShowShotFeedback;

        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }
    }

    private void Update()
    {
        if (playerController == null)
            return;

        bool isCharging =
            playerController.State ==
            PenaltyPlayerController.PlayerState.Charging;

        bool isVisible = showOnlyWhileCharging
            ? isCharging
            : playerController.State !=
              PenaltyPlayerController.PlayerState.ShotTaken;

        SetVisible(isVisible);

        if (isCharging)
            UpdateMeter(playerController.CurrentPower01);
    }

    private void UpdateMeter(float power)
    {
        power = Mathf.Clamp01(power);

        if (barFill != null)
        {
            barFill.fillAmount = power;
            barFill.color = Color.Lerp(
                lowPowerColor,
                highPowerColor,
                power
            );
        }

        if (percentageLabel != null)
        {
            percentageLabel.text =
                $"{Mathf.RoundToInt(power * 100f)}%";
        }
    }

    private void ShowShotFeedback(bool isPerfect, float quality)
    {
        if (shotFeedbackLabel == null)
            return;

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);

        string message;
        Color color;

        if (isPerfect)
        {
            message = "PERFECT!";
            color = perfectColor;
        }
        else if (quality >= goodShotThreshold)
        {
            message = "GOOD TIMING";
            color = goodColor;
        }
        else
        {
            message = "KEEP PRACTICING";
            color = weakColor;
        }

        shotFeedbackLabel.text = message;
        shotFeedbackLabel.color = color;
        shotFeedbackLabel.gameObject.SetActive(true);

        feedbackRoutine = StartCoroutine(HideFeedback());

        HideInstructions();
    }

    private IEnumerator HideFeedback()
    {
        yield return new WaitForSeconds(feedbackDuration);

        if (shotFeedbackLabel != null)
            shotFeedbackLabel.gameObject.SetActive(false);

        feedbackRoutine = null;

        ShowInstructions();
    }

    private void SetVisible(bool visible)
    {
        if (meterPanel != null &&
            meterPanel.activeSelf != visible)
        {
            meterPanel.SetActive(visible);
        }
    }
}
