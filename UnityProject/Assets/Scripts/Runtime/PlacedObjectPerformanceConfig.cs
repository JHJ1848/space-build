using UnityEngine;

[CreateAssetMenu(fileName = "PlacedObjectPerformanceConfig", menuName = "SpaceBuild/Placed Object Performance Config")]
public sealed class PlacedObjectPerformanceConfig : ScriptableObject
{
    public float chunkSize = 28f;
    public float visibilityUpdateInterval = 0.15f;
    public float activeDistance = 70f;
    public int maxTrackedChunkRadius = 4;
    public int maxPoolSizePerType = 128;
}
