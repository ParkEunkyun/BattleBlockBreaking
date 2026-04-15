using System.Collections.Generic;

/// <summary>
/// 로비에서 선택한 아티팩트 Definition을 노말씬으로 넘기는 세션 데이터.
/// </summary>
public static class NormalArtifactSession
{
    private static readonly List<NormalArtifactDefinition> _selected =
        new List<NormalArtifactDefinition>(4);

    public static IReadOnlyList<NormalArtifactDefinition> Selected => _selected;

    public static void Set(IList<NormalArtifactDefinition> defs)
    {
        _selected.Clear();
        if (defs == null) return;
        foreach (var d in defs)
        {
            if (d == null) continue;
            if (_selected.Count >= 4) break;
            _selected.Add(d);
        }
    }

    public static void Clear() => _selected.Clear();
}