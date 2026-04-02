using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class BattleDefenseBoardFx : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject rootObject;
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("Images")]
    [SerializeField] private RectTransform shieldImage;
    [SerializeField] private CanvasGroup shieldCanvasGroup;
    [SerializeField] private RectTransform ringImage;
    [SerializeField] private CanvasGroup ringCanvasGroup;

    [Header("Timing")]
    [SerializeField] private float totalDuration = 0.42f;
    [SerializeField] private float shieldPopDuration = 0.16f;
    [SerializeField] private float fadeOutDuration = 0.18f;

    [Header("Scale")]
    [SerializeField] private float shieldStartScale = 0.6f;
    [SerializeField] private float shieldPeakScale = 1.12f;
    [SerializeField] private float ringStartScale = 0.7f;
    [SerializeField] private float ringEndScale = 1.35f;

    private Coroutine _routine;
    private Vector3 _shieldBaseScale = Vector3.one;
    private Vector3 _ringBaseScale = Vector3.one;

    private void Awake()
    {
        if (shieldImage != null)
            _shieldBaseScale = shieldImage.localScale;

        if (ringImage != null)
            _ringBaseScale = ringImage.localScale;

        HideImmediate();
    }

    public void Play()
    {
        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(CoPlay());
    }

    public IEnumerator PlayRoutine()
    {
        if (_routine != null)
            StopCoroutine(_routine);

        yield return CoPlay();
    }

    private IEnumerator CoPlay()
    {
        ShowRoot();

        if (shieldImage != null)
        {
            shieldImage.DOKill();
            shieldImage.localScale = _shieldBaseScale * shieldStartScale;
        }

        if (ringImage != null)
        {
            ringImage.DOKill();
            ringImage.localScale = _ringBaseScale * ringStartScale;
        }

        if (rootCanvasGroup != null)
            rootCanvasGroup.alpha = 1f;

        if (shieldCanvasGroup != null)
            shieldCanvasGroup.alpha = 0f;

        if (ringCanvasGroup != null)
            ringCanvasGroup.alpha = 0f;

        Sequence seq = DOTween.Sequence();

        if (shieldCanvasGroup != null)
            seq.Join(shieldCanvasGroup.DOFade(1f, 0.08f));

        if (shieldImage != null)
        {
            seq.Join(shieldImage.DOScale(_shieldBaseScale * shieldPeakScale, shieldPopDuration).SetEase(Ease.OutBack));
            seq.Append(shieldImage.DOScale(_shieldBaseScale, 0.10f).SetEase(Ease.OutQuad));
        }

        if (ringCanvasGroup != null)
        {
            seq.Join(ringCanvasGroup.DOFade(0.9f, 0.06f));
            seq.Append(ringCanvasGroup.DOFade(0f, fadeOutDuration).SetEase(Ease.OutQuad));
        }

        if (ringImage != null)
            seq.Join(ringImage.DOScale(_ringBaseScale * ringEndScale, totalDuration).SetEase(Ease.OutQuad));

        if (shieldCanvasGroup != null)
            seq.Append(shieldCanvasGroup.DOFade(0f, fadeOutDuration).SetEase(Ease.OutQuad));

        yield return seq.WaitForCompletion();

        HideImmediate();
        _routine = null;
    }

    private void ShowRoot()
    {
        if (rootObject != null)
            rootObject.SetActive(true);
    }

    private void HideImmediate()
    {
        if (rootCanvasGroup != null)
            rootCanvasGroup.alpha = 0f;

        if (shieldCanvasGroup != null)
            shieldCanvasGroup.alpha = 0f;

        if (ringCanvasGroup != null)
            ringCanvasGroup.alpha = 0f;

        if (shieldImage != null)
            shieldImage.localScale = _shieldBaseScale;

        if (ringImage != null)
            ringImage.localScale = _ringBaseScale;

        if (rootObject != null)
            rootObject.SetActive(false);
    }
}