using UnityEngine;

namespace SpaceBuild.Editor
{
    public enum GroundTileTheme
    {
        Dirt,
        Grass,
        Rocky,
        Sand
    }

    public sealed class GroundTileConfig : ScriptableObject
    {
        public GroundTileTheme theme = GroundTileTheme.Rocky;
        public float tileWorldSize = 7f;
        public int gridColumns = 36;
        public int gridRows = 36;
    }
}
