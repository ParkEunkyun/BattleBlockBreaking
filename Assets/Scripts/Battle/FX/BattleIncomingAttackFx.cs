using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class BattleIncomingAttackFx : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject rootObject;
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("Board")]
    [SerializeField] private RectTransform boardRoot;

    [Header("Common Visuals")]
    [SerializeField] private Image impactIconImage;
    [SerializeField] private RectTransform impactIconRect;
    [SerializeField] private CanvasGroup impactIconCanvasGroup;

    [SerializeField] private Image sweepImage;
    [SerializeField] private RectTransform sweepRect;
    [SerializeField] private CanvasGroup sweepCanvasGroup;

    [SerializeField] private Image shockwaveImage;
    [SerializeField] private RectTransform shockwaveRect;
    [SerializeField] private CanvasGroup shockwaveCanvasGroup;

    [SerializeField] private Image boardFlashImage;
    [SerializeField] private CanvasGroup boardFlashCanvasGroup;

    [Header("Sprites")]
    [SerializeField] private Sprite obstacleSprite;
    [SerializeField] private Sprite sealSprite;
    [SerializeField] private Sprite curseSprite;
    [SerializeField] private Sprite disableSprite;
    [SerializeField] private Sprite deleteLineSprite;
    [SerializeField] private Sprite bombSprite;

    [Header("Timing")]
    [SerializeField] private float closeGapBeforeFx = 0.08f;
    [SerializeField] private float postImpactDelay = 0.10f;

    private Vector2 _boardBasePos;
    private Vector3 _boardBaseScale = Vector3.one;
    private Vector3 _iconBaseScale = Vector3.one;
    private Vector3 _sweepBaseScale = Vector3.one;
    private Vector3 _shockwaveBaseScale = Vector3.one;

    private void Awake()
    {
        if (boardRoot != null)
        {
            _boardBasePos = boardRoot.anchoredPosition;
            _boardBaseScale = boardRoot.localScale;
        }

        if (impactIconRect != null)
            _iconBaseScale = impactIconRect.localScale;

        if (sweepRect != null)
            _sweepBaseScale = sweepRect.localScale;

        if (shockwaveRect != null)
            _shockwaveBaseScale = shockwaveRect.localScale;

        HideImmediate();
    }

    public IEnumerator PlayRoutine(BattleManager.BattleItemId itemId, Action onImpact)
    {
        yield return new WaitForSeconds(closeGapBeforeFx);

        ShowRoot();
        ResetVisuals();

        switch (itemId)
        {
            case BattleManager.BattleItemId.AttackObstacle2:
                yield return CoPlayObstacle(onImpact);
                break;

            case BattleManager.BattleItemId.AttackSealRandomSlot:
                yield return CoPlaySeal(onImpact);
                break;

            case BattleManager.BattleItemId.AttackCurseBlock:
                yield return CoPlayCurse(onImpact);
                break;

            case BattleManager.BattleItemId.AttackDisableItemUse:
                yield return CoPlayDisable(onImpact);
                break;

            case BattleManager.BattleItemId.AttackDeleteRandomLine:
                yield return CoPlayDeleteLine(onImpact);
                break;

            case BattleManager.BattleItemId.AttackBomb3x3:
                yield return CoPlayBomb(onImpact);
                break;

            default:
                onImpact?.Invoke();
                yield return new WaitForSeconds(0.08f);
                break;
        }

        yield return new WaitForSeconds(postImpactDelay);
        HideImmediate();
    }

    private IEnumerator CoPlayObstacle(Action onImpact)
    {
        SetIcon(obstacleSprite, new Color(1f, 1f, 1f, 1f));
        SetBoardFlashColor(new Color(0.85f, 0.15f, 0.15f, 1f));

        impactIconCanvasGroup.alpha = 0f;
        impactIconRect.localScale = _iconBaseScale * 2.1f;

        Sequence seq = DOTween.Sequence();
        seq.Join(impactIconCanvasGroup.DOFade(1f, 0.08f));
        seq.Join(impactIconRect.DOScale(_iconBaseScale * 0.95f, 0.22f).SetEase(Ease.InBack));
        seq.Join(boardRoot.DOShakeAnchorPos(0.16f, 12f, 20, 90, false, true));

        yield return new WaitForSeconds(0.18f);

        onImpact?.Invoke();

        if (boardFlashCanvasGroup != null)
            yield return boardFlashCanvasGroup.DOFade(0.45f, 0.04f).WaitForCompletion();

        if (boardFlashCanvasGroup != null)
            yield return boardFlashCanvasGroup.DOFade(0f, 0.14f).WaitForCompletion();

        if (impactIconCanvasGroup != null)
            yield return impactIconCanvasGroup.DOFade(0f, 0.10f).WaitForCompletion();
    }

    private IEnumerator CoPlaySeal(Action onImpact)
    {
        SetIcon(sealSprite, new Color(1f, 1f, 1f, 1f));
        SetBoardFlashColor(new Color(0.30f, 0.45f, 0.95f, 1f));

        impactIconCanvasGroup.alpha = 0f;
        impactIconRect.localScale = _iconBaseScale * 1.6f;

        Sequence seq = DOTween.Sequence();
        seq.Join(impactIconCanvasGroup.DOFade(1f, 0.06f));
        seq.Join(impactIconRect.DOScale(_iconBaseScale, 0.18f).SetEase(Ease.OutBack));
        seq.Join(impactIconRect.DORotate(new Vector3(0f, 0f, -14f), 0.18f));

        yield return new WaitForSeconds(0.14f);
        onImpact?.Invoke();

        if (boardFlashCanvasGroup != null)
            yield return boardFlashCanvasGroup.DOFade(0.35f, 0.05f).WaitForCompletion();

        if (boardFlashCanvasGroup != null)
            yield return boardFlashCanvasGroup.DOFade(0f, 0.16f).WaitForCompletion();

        if (impactIconCanvasGroup != null)
            yield return impactIconCanvasGroup.DOFade(0f, 0.12f).WaitForCompletion();
    }

    private IEnumerator CoPlayCurse(Action onImpact)
    {
        SetIcon(curseSprite, new Color(1f, 1f, 1f, 1f));
        SetBoardFlashColor(new Color(0.45f, 0.10f, 0.65f, 1f));

        impactIconCanvasGroup.alpha = 0f;
        impactIconRect.localScale = _iconBaseScale * 0.7f;
        impactIconRect.localRotation = Quaternion.Euler(0f, 0f, -45f);

        Sequence seq = DOTween.Sequence();
        seq.Join(impactIconCanvasGroup.DOFade(1f, 0.08f));
        seq.Join(impactIconRect.DOScale(_iconBaseScale * 1.15f, 0.22f).SetEase(Ease.OutBack));
        seq.Join(impactIconRect.DORotate(Vector3.zero, 0.22f).SetEase(Ease.OutQuad));

        yield return new WaitForSeconds(0.16f);
        onImpact?.Invoke();

        if (boardFlashCanvasGroup != null)
            yield return boardFlashCanvasGroup.DOFade(0.32f, 0.04f).WaitForCompletion();

        if (boardFlashCanvasGroup != null)
            yield return boardFlashCanvasGroup.DOFade(0f, 0.16f).WaitForCompletion();

        if (impactIconCanvasGroup != null)
            yield return impactIconCanvasGroup.DOFade(0f, 0.14f).WaitForCompletion();
    }

    private IEnumerator CoPlayDisable(Action onImpact)
    {
        SetIcon(disableSprite, new Color(1f, 1f, 1f, 1f));
        SetBoardFlashColor(new Color(0.90f, 0.18f, 0.18f, 1f));

        impactIconCanvasGroup.alpha = 0f;
        impactIconRect.localScale = _iconBaseScale * 1.8f;

        Sequence seq = DOTween.Sequence();
        seq.Join(impactIconCanvasGroup.DOFade(1f, 0.05f));
        seq.Join(impactIconRect.DOScale(_iconBaseScale, 0.16f).SetEase(Ease.OutBack));
        seq.Join(impactIconRect.DOPunchRotation(new Vector3(0f, 0f, -18f), 0.18f, 8, 0.5f));

        yield return new WaitForSeconds(0.12f);
        onImpact?.Invoke();

        if (boardFlashCanvasGroup != null)
            yield return boardFlashCanvasGroup.DOFade(0.38f, 0.04f).WaitForCompletion();

        if (boardFlashCanvasGroup != null)
            yield return boardFlashCanvasGroup.DOFade(0f, 0.12f).WaitForCompletion();

        if (impactIconCanvasGroup != null)
            yield return impactIconCanvasGroup.DOFade(0f, 0.10f).WaitForCompletion();
    }

    private IEnumerator CoPlayDeleteLine(Action onImpact)
    {
        SetIcon(deleteLineSprite, new Color(1f, 1f, 1f, 1f));
        SetBoardFlashColor(new Color(1f, 0.25f, 0.25f, 1f));

        if (sweepCanvasGroup != null)
            sweepCanvasGroup.alpha = 1f;

        if (sweepRect != null)
        {
            sweepRect.localScale = _sweepBaseScale;
            sweepRect.anchoredPosition = new Vector2(-420f, 0f);
        }

        if (impactIconCanvasGroup != null)
        {
            impactIconCanvasGroup.alpha = 0.9f;
            impactIconRect.localScale = _iconBaseScale;
        }

        Sequence seq = DOTween.Sequence();
        if (sweepRect != null)
            seq.Join(sweepRect.DOAnchorPosX(420f, 0.22f).SetEase(Ease.InOutQuad));

        if (impactIconCanvasGroup != null)
            seq.Join(impactIconCanvasGroup.DOFade(0f, 0.22f));

        yield return new WaitForSeconds(0.14f);
        onImpact?.Invoke();

        if (boardFlashCanvasGroup != null)
            yield return boardFlashCanvasGroup.DOFade(0.40f, 0.04f).WaitForCompletion();

        if (boardFlashCanvasGroup != null)
            yield return boardFlashCanvasGroup.DOFade(0f, 0.12f).WaitForCompletion();

        if (sweepCanvasGroup != null)
            sweepCanvasGroup.alpha = 0f;
    }

    private IEnumerator CoPlayBomb(Action onImpact)
    {
        SetIcon(bombSprite, new Color(1f, 1f, 1f, 1f));
        SetBoardFlashColor(new Color(1f, 0.35f, 0.18f, 1f));

        impactIconCanvasGroup.alpha = 1f;
        impactIconRect.localScale = _iconBaseScale * 0.75f;

        Sequence pre = DOTween.Sequence();
        pre.Append(impactIconRect.DOScale(_iconBaseScale * 1.05f, 0.10f).SetEase(Ease.OutQuad));
        pre.Append(impactIconRect.DOScale(_iconBaseScale * 0.92f, 0.08f).SetEase(Ease.InQuad));
        yield return pre.WaitForCompletion();

        onImpact?.Invoke();

        if (shockwaveCanvasGroup != null)
            shockwaveCanvasGroup.alpha = 0.85f;

        if (shockwaveRect != null)
            shockwaveRect.localScale = _shockwaveBaseScale * 0.4f;

        boardRoot.DOShakeAnchorPos(0.20f, 18f, 24, 90, false, true);

        Sequence boom = DOTween.Sequence();

        if (boardFlashCanvasGroup != null)
            boom.Join(boardFlashCanvasGroup.DOFade(0.65f, 0.04f));

        if (shockwaveRect != null)
            boom.Join(shockwaveRect.DOScale(_shockwaveBaseScale * 1.8f, 0.20f).SetEase(Ease.OutQuad));

        if (shockwaveCanvasGroup != null)
            boom.Join(shockwaveCanvasGroup.DOFade(0f, 0.20f));

        if (impactIconCanvasGroup != null)
            boom.Join(impactIconCanvasGroup.DOFade(0f, 0.12f));

        yield return boom.WaitForCompletion();

        if (boardFlashCanvasGroup != null)
            yield return boardFlashCanvasGroup.DOFade(0f, 0.12f).WaitForCompletion();
    }

    private void SetIcon(Sprite sprite, Color color)
    {
        if (impactIconImage != null)
        {
            impactIconImage.sprite = sprite;
            impactIconImage.color = color;
        }

        if (impactIconRect != null)
        {
            impactIconRect.localRotation = Quaternion.identity;
            impactIconRect.localScale = _iconBaseScale;
        }
    }

    private void SetBoardFlashColor(Color color)
    {
        if (boardFlashImage != null)
            boardFlashImage.color = color;
    }

    private void ShowRoot()
    {
        if (rootObject != null)
            rootObject.SetActive(true);

        if (rootCanvasGroup != null)
            rootCanvasGroup.alpha = 1f;
    }

    private void ResetVisuals()
    {
        if (impactIconCanvasGroup != null)
            impactIconCanvasGroup.alpha = 0f;

        if (sweepCanvasGroup != null)
            sweepCanvasGroup.alpha = 0f;

        if (shockwaveCanvasGroup != null)
            shockwaveCanvasGroup.alpha = 0f;

        if (boardFlashCanvasGroup != null)
            boardFlashCanvasGroup.alpha = 0f;

        if (impactIconRect != null)
        {
            impactIconRect.DOKill();
            impactIconRect.localScale = _iconBaseScale;
            impactIconRect.localRotation = Quaternion.identity;
        }

        if (sweepRect != null)
        {
            sweepRect.DOKill();
            sweepRect.localScale = _sweepBaseScale;
        }

        if (shockwaveRect != null)
        {
            shockwaveRect.DOKill();
            shockwaveRect.localScale = _shockwaveBaseScale;
        }

        if (boardRoot != null)
        {
            boardRoot.DOKill();
            boardRoot.anchoredPosition = _boardBasePos;
            boardRoot.localScale = _boardBaseScale;
        }
    }

    private void HideImmediate()
    {
        ResetVisuals();

        if (rootCanvasGroup != null)
            rootCanvasGroup.alpha = 0f;

        if (rootObject != null)
            rootObject.SetActive(false);
    }
}