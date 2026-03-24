using UnityEngine;
using UnityEngine.SceneManagement;

public class BattleLobbyLoadoutController : MonoBehaviour
{
    [Header("Scene Name")]
    [SerializeField] private string normalSceneName = "Scene_Normal";

    [Header("Mode")]
    [SerializeField] private GameMode gameMode = GameMode.Ranked;

    [Header("Selected Attack Loadout (3)")]
    [SerializeField] private BattleManager.AttackItemId attack1 = BattleManager.AttackItemId.Obstacle2;
    [SerializeField] private BattleManager.AttackItemId attack2 = BattleManager.AttackItemId.SealRandomSlot;
    [SerializeField] private BattleManager.AttackItemId attack3 = BattleManager.AttackItemId.Bomb3x3;

    [Header("Selected Support Loadout (2)")]
    [SerializeField] private BattleManager.SupportItemId support1 = BattleManager.SupportItemId.RotateRight90;
    [SerializeField] private BattleManager.SupportItemId support2 = BattleManager.SupportItemId.ResetRemaining;

    public void EnterBattleWithCurrentLoadout()
    {
        BattleManager.AttackItemId[] attacks =
        {
            attack1,
            attack2,
            attack3
        };

        BattleManager.SupportItemId[] supports =
        {
            support1,
            support2
        };

        BattleLoadoutSession.SetLoadout(attacks, supports, gameMode);

        switch (gameMode)
        {
            case GameMode.Ranked:
                {
                    if (MatchManager.I == null)
                    {
                        Debug.LogError("[BattleLobbyLoadoutController] MatchManager가 씬에 없습니다.");
                        return;
                    }

                    MatchManager.I.StartRankedMatch();
                    break;
                }

            case GameMode.Normal:
                {
                    SceneManager.LoadScene(normalSceneName);
                    break;
                }

            default:
                {
                    Debug.LogError("[BattleLobbyLoadoutController] 지원하지 않는 GameMode 입니다.");
                    break;
                }
        }
    }
}