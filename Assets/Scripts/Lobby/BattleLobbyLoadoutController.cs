using UnityEngine;
using UnityEngine.SceneManagement;

public class BattleLobbyLoadoutController : MonoBehaviour
{
    [Header("Battle Scene Name")]
    [SerializeField] private string battleSceneName = "Scene_Battle";

    [Header("Mode")]
    [SerializeField] private bool isRankedMode = true;

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

        BattleLoadoutSession.SetLoadout(attacks, supports, isRankedMode);
        SceneManager.LoadScene(battleSceneName);
    }
}