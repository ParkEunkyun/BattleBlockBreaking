using System.Collections.Generic;

public static class NormalArtifactSession
{
    private static readonly List<NormalArtifactId> _selectedArtifacts = new List<NormalArtifactId>(4);

    public static IReadOnlyList<NormalArtifactId> SelectedArtifacts => _selectedArtifacts;

    public static void SetArtifacts(IList<NormalArtifactId> artifacts)
    {
        _selectedArtifacts.Clear();

        if (artifacts == null)
            return;

        for (int i = 0; i < artifacts.Count && _selectedArtifacts.Count < 4; i++)
        {
            NormalArtifactId id = artifacts[i];

            if (id == NormalArtifactId.None)
                continue;

            if (_selectedArtifacts.Contains(id))
                continue;

            _selectedArtifacts.Add(id);
        }
    }

    public static List<NormalArtifactId> GetCopy()
    {
        return new List<NormalArtifactId>(_selectedArtifacts);
    }

    public static void Clear()
    {
        _selectedArtifacts.Clear();
    }
}