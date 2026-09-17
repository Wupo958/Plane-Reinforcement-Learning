using UnityEngine;

// The scene is regenerated from scratch on every build, so a gate dragged around in the editor is
// thrown away unless its position lives outside the scene. This asset is that home: bake the scene
// into it and the generator will use it instead of its own defaults.
[CreateAssetMenu(fileName = "CheckpointLayout", menuName = "Terrain Map/Checkpoint Layout")]
public class CheckpointLayout : ScriptableObject
{
    [SerializeField] private Vector3[] positions;

    public int Count
    {
        get
        {
            if (positions == null)
            {
                return 0;
            }
            return positions.Length;
        }
    }

    public Vector3 Get(int index)
    {
        return positions[index];
    }

    public void SetPositions(Vector3[] value)
    {
        positions = value;
    }
}
