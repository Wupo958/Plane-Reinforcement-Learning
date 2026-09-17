using System.Collections.Generic;
using UnityEngine;

public class GenerationManager : MonoBehaviour
{
    [SerializeField] private int mapRebuildInterval = 25;
    [SerializeField] private MapRuntimeBuilder mapBuilder;
    [SerializeField] private float gatePlacementHalfExtent = 2900f;
    [SerializeField] private float gateMinAltitude = 45f;
    [SerializeField] private float gateMaxAltitude = 130f;
    [SerializeField] private float gateProbeHeight = 1500f;
    [SerializeField] private float gateMinGroundHeight = 2f;
    [SerializeField] private float gateClearRadius = 46f;
    [SerializeField] private float gateLiftStep = 12f;
    [SerializeField] private int gateLiftAttempts = 25;
    [SerializeField] private LayerMask gateGroundMask = ~0;

    private List<TerrainAgentScript> agents = new List<TerrainAgentScript>();
    private HashSet<TerrainAgentScript> parkedAgents = new HashSet<TerrainAgentScript>();
    private List<GameObject> gates = new List<GameObject>();
    private int generationNumber;

    public int GenerationNumber
    {
        get { return generationNumber; }
    }

    public void RegisterAgent(TerrainAgentScript agent)
    {
        if (!agents.Contains(agent))
        {
            agents.Add(agent);
        }
    }

    public void RegisterGates(GameObject[] checkpoints)
    {
        if (gates.Count > 0)
        {
            return;
        }
        for (int i = 0; i < checkpoints.Length; i++)
        {
            gates.Add(checkpoints[i]);
        }
    }

    public void ReportParked(TerrainAgentScript agent)
    {
        parkedAgents.Add(agent);
        if (parkedAgents.Count < agents.Count)
        {
            return;
        }

        StartNextGeneration();
    }

    private void StartNextGeneration()
    {
        generationNumber++;

        if (mapBuilder != null && mapRebuildInterval > 0 && generationNumber % mapRebuildInterval == 0)
        {
            mapBuilder.Rebuild(Random.Range(int.MinValue, int.MaxValue));
        }

        RandomizeGates();
        parkedAgents.Clear();
        for (int i = 0; i < agents.Count; i++)
        {
            agents[i].Release();
        }
    }

    private void RandomizeGates()
    {
        for (int i = 0; i < gates.Count; i++)
        {
            Vector3 spot;
            if (FindGateSpot(out spot))
            {
                gates[i].transform.position = spot;
            }
        }
    }

    private bool FindGateSpot(out Vector3 spot)
    {
        for (int attempt = 0; attempt < 24; attempt++)
        {
            float x = Random.Range(-gatePlacementHalfExtent, gatePlacementHalfExtent);
            float z = Random.Range(-gatePlacementHalfExtent, gatePlacementHalfExtent);
            Vector3 origin = new Vector3(x, gateProbeHeight, z);

            RaycastHit hit;
            if (!Physics.Raycast(origin, Vector3.down, out hit, gateProbeHeight * 2f,
                                 gateGroundMask, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            if (hit.point.y < gateMinGroundHeight)
            {
                continue;
            }

            Vector3 candidate = new Vector3(x, hit.point.y + Random.Range(gateMinAltitude, gateMaxAltitude), z);
            if (LiftUntilClear(ref candidate))
            {
                spot = candidate;
                return true;
            }
        }

        spot = Vector3.zero;
        return false;
    }

    private bool LiftUntilClear(ref Vector3 candidate)
    {
        for (int attempt = 0; attempt < gateLiftAttempts; attempt++)
        {
            if (!Physics.CheckSphere(candidate, gateClearRadius, gateGroundMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }
            candidate.y += gateLiftStep;
        }
        return false;
    }

}
