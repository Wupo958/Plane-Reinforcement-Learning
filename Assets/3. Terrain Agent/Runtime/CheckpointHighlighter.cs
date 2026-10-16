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
        Rebind();
    }

    // A map rebuild destroys the object this component was attached to, so the runtime builder
    // attaches a fresh copy with nothing wired into it. Resolving both ends here means the
    // highlighting keeps working without relying on serialized references that cannot survive.
    public void Rebind()
    {
        if (watchedAgent == null)
        {
            watchedAgent = FindFirstObjectByType<TerrainAgentScript>();
        }
        if (gates == null || gates.Length == 0)
        {
            gates = FindObjectsByType<CheckpointGate>(FindObjectsSortMode.None);
        }
        lastTarget = null;
    }

    // The camera cuts away to whoever is still flying when the plane it was watching goes down,
    // and the highlight has to follow it there, or it keeps lighting up the gate belonging to an
    // aircraft that is parked on the runway.
    public void Watch(TerrainAgentScript agent)
    {
        if (agent == null)
        {
            return;
        }
        watchedAgent = agent;
        lastTarget = null;
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
