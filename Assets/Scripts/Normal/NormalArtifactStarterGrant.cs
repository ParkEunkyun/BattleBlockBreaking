using System.Collections.Generic;
using UnityEngine;

public sealed class NormalArtifactStarterGrant : MonoBehaviour
{
    [Header("Starter Artifacts")]
    [SerializeField] private List<NormalArtifactDefinition> starterArtifacts = new List<NormalArtifactDefinition>();

    [Header("Options")]
    [SerializeField] private bool grantOnAwake = true;

    private void Awake()
    {
        if (!grantOnAwake)
            return;

        GrantIfEmpty();
    }

    public void GrantIfEmpty()
    {
        if (NormalArtifactOwnershipStore.HasAnyOwned())
            return;

        for (int i = 0; i < starterArtifacts.Count; i++)
        {
            NormalArtifactDefinition def = starterArtifacts[i];

            if (def == null)
                continue;

            NormalArtifactOwnershipStore.AddOwned(def, 1);
        }

        Debug.Log($"[NormalArtifactStarterGrant] 기본 아티팩트 지급 완료: {starterArtifacts.Count}개");
    }

    public void GrantNow()
    {
        for (int i = 0; i < starterArtifacts.Count; i++)
        {
            NormalArtifactDefinition def = starterArtifacts[i];

            if (def == null)
                continue;

            NormalArtifactOwnershipStore.AddOwned(def, 1);
        }

        Debug.Log($"[NormalArtifactStarterGrant] 아티팩트 지급 완료: {starterArtifacts.Count}개");
    }
}