using UnityEngine;

// Every gate looks identical from the air, so watching a run gives no clue which one the plane is
// actually aiming at. One agent is nominated as the one being watched and its target lights up.
public class CheckpointHighlighter : MonoBehaviour
{
    [SerializeField] private TerrainAgentScript watchedAgent;
    [SerializeField] private CheckpointGate[] gates;

    private GameObject lastTarget;

    private void Start()
    {
        if (watchedAgent == null)
        {
            watchedAgent = FindFirstObjectByType<TerrainAgentScript>();
        }
    }

    private void Update()
    {
        if (watchedAgent == null || gates == null)
        {
            return;
        }

        GameObject target = watchedAgent.ActiveCheckpoint;
        if (target == lastTarget)
        {
            return;
        }
        lastTarget = target;

        for (int i = 0; i < gates.Length; i++)
        {
            if (gates[i] == null)
            {
                continue;
            }
            gates[i].SetHighlighted(gates[i].gameObject == target);
        }
    }
}
