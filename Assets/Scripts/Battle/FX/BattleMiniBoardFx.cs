using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BattleMiniBoardFx : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private RectTransform shakeTarget;

    [Header("Hit FX (Red)")]
    [SerializeField] private CanvasGroup hitFlashCanvasGroup;
    [SerializeField] private float hitFlashDuration = 0.28f;
    [SerializeField] private float maxHitFlashAlpha = 0.65f;
    [SerializeField] private float hitShakeDuration = 0.22f;
    [SerializeField] private float hitShakeMagnitude = 10f;

    [Header("Block FX (Blue Shield)")]
    [SerializeField] private CanvasGroup blockFlashCanvasGroup;
    [SerializeField] private Image blockShieldImage;
    [SerializeField] private float blockFxDuration = 0.45f;
    [SerializeField] private float maxBlockFlashAlpha = 0.55f;
    [SerializeField] private float shieldPopScale = 1.18f;

    private Coroutine _fxRoutine;
    private Vector2 _originAnchoredPos;
    private Vector3 _originShieldScale = Vector3.one;

    private void Awake()
    {
        if (shakeTarget != null)
            _originAnchoredPos = shakeTarget.anchoredPosition;

        if (hitFlashCanvasGroup != null)
            hitFlashCanvasGroup.alpha = 0f;

        if (blockFlashCanvasGroup != null)
            blockFlashCanvasGroup.alpha = 0f;

        if (blockShieldImage != null)
        {
            _originShieldScale = blockShieldImage.rectTransform.localScale;
            blockShieldImage.gameObject.SetActive(false);
        }
    }

    public void PlayAttackHitFx()
    {
        if (_fxRoutine != null)
            StopCoroutine(_fxRoutine);

        _fxRoutine = StartCoroutine(CoPlayAttackHitFx());
    }

    public void PlayAttackBlockedFx()
    {
        if (_fxRoutine != null)
            StopCoroutine(_fxRoutine);

        _fxRoutine = StartCoroutine(CoPlayAttackBlockedFx());
    }

    private IEnumerator CoPlayAttackHitFx()
    {
        ResetAllVisuals();

        float elapsed = 0f;
        float total = Mathf.Max(hitFlashDuration, hitShakeDuration);

        while (elapsed < total)
        {
            elapsed += Time.deltaTime;

            if (hitFlashCanvasGroup != null)
            {
                float t = Mathf.Clamp01(elapsed / hitFlashDuration);
                float alpha = 1f - t;
                hitFlashCanvasGroup.alpha = maxHitFlashAlpha * alpha;
            }

            if (shakeTarget != null && elapsed <= hitShakeDuration)
            {
                float damper = 1f - Mathf.Clamp01(elapsed / hitShakeDuration);
                float offsetX = Random.Range(-hitShakeMagnitude, hitShakeMagnitude) * damper;
                float offsetY = Random.Range(-hitShakeMagnitude, hitShakeMagnitude) * damper;
                shakeTarget.anchoredPosition = _originAnchoredPos + new Vector2(offsetX, offsetY);
            }

            yield return null;
        }

        ResetAllVisuals();
        _fxRoutine = null;
    }

    private IEnumerator CoPlayAttackBlockedFx()
    {
        ResetAllVisuals();

        if (blockShieldImage != null)
        {
            blockShieldImage.gameObject.SetActive(true);
            blockShieldImage.rectTransform.localScale = _originShieldScale * shieldPopScale;
        }

        float elapsed = 0f;

        while (elapsed < blockFxDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / blockFxDuration);

            if (blockFlashCanvasGroup != null)
                blockFlashCanvasGroup.alpha = maxBlockFlashAlpha * (1f - t);

            if (blockShieldImage != null)
            {
                float scaleT = Mathf.Lerp(shieldPopScale, 1f, t);
                blockShieldImage.rectTransform.localScale = _originShieldScale * scaleT;

                Color c = blockShieldImage.color;
                c.a = 1f - t * 0.15f;
                blockShieldImage.color = c;
            }

            yield return null;
        }

        ResetAllVisuals();
        _fxRoutine = null;
    }

    private void ResetAllVisuals()
    {
        if (hitFlashCanvasGroup != null)
            hitFlashCanvasGroup.alpha = 0f;

        if (blockFlashCanvasGroup != null)
            blockFlashCanvasGroup.alpha = 0f;

        if (shakeTarget != null)
            shakeTarget.anchoredPosition = _originAnchoredPos;

        if (blockShieldImage != null)
        {
            blockShieldImage.rectTransform.localScale = _originShieldScale;

            Color c = blockShieldImage.color;
            c.a = 1f;
            blockShieldImage.color = c;

            blockShieldImage.gameObject.SetActive(false);
        }
    }
}