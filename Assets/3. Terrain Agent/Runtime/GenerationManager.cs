using System.Collections.Generic;
using UnityEngine;

public class GenerationManager : MonoBehaviour
{
    // The switch. Off keeps whatever map is currently standing, and the fleet just keeps flying
    // it; flipping it back on rebuilds at the next interval. Safe to toggle during play.
    [SerializeField] private bool mapGenerationEnabled = true;
    // Counted in trainer steps, so this lines up with checkpoint_interval in the trainer config
    // and each checkpoint lands on a fresh map.
    [SerializeField] private int mapRebuildStepInterval = 250000;
    [SerializeField] private MapRuntimeBuilder mapBuilder;
    [SerializeField] private float gatePlacementHalfExtent = 2900f;
    [SerializeField] private float gateMinAltitude = 45f;
    [SerializeField] private float gateMaxAltitude = 130f;
    [SerializeField] private float gateProbeHeight = 1500f;
    [SerializeField] private float gateMinGroundHeight = 2f;
    [SerializeField] private float gateClearRadius = 46f;
    [SerializeField] private float gateLiftStep = 12f;
    [SerializeField] private int gateLiftAttempts = 25;
    [SerializeField] private int gatePlacementAttempts = 24;
    [SerializeField] private float gateSeparation = 130f;
    [SerializeField] private LayerMask gateGroundMask = ~0;

    private List<TerrainAgentScript> agents = new List<TerrainAgentScript>();
    private HashSet<TerrainAgentScript> parkedAgents = new HashSet<TerrainAgentScript>();
    private List<GameObject> gates = new List<GameObject>();
    private List<Vector3> placedGates = new List<Vector3>();
    private int generationNumber;
    private bool generationPending;
    private long trainerSteps;
    private long nextRebuildStep;
    private bool interruptPending;
    // Generation 1 still rebuilds, so the first flight sees a randomized map rather than the
    // fixed layout the editor happened to save into the scene.
    private bool rebuildDue = true;

    public int GenerationNumber
    {
        get { return generationNumber; }
    }

    public IReadOnlyList<TerrainAgentScript> Agents
    {
        get { return agents; }
    }

    public bool MapGenerationEnabled
    {
        get { return mapGenerationEnabled; }
        set { mapGenerationEnabled = value; }
    }

    public long TrainerSteps
    {
        get { return trainerSteps; }
    }

    private void Awake()
    {
        nextRebuildStep = mapRebuildStepInterval;
    }

    // One call per agent decision, which is exactly what the trainer counts as a step.
    public void ReportDecision()
    {
        trainerSteps++;

        if (!mapGenerationEnabled || mapRebuildStepInterval <= 0 || trainerSteps < nextRebuildStep)
        {
            return;
        }

        nextRebuildStep = trainerSteps + mapRebuildStepInterval;
        rebuildDue = true;
        interruptPending = true;
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

        if (gates.Count == 0)
        {
            Debug.LogError("No checkpoints were registered, so there is nothing to fly towards."
                + " Rebuild the scene with Terrain Map/Build World Scene.");
        }
    }

    public void ReportParked(TerrainAgentScript agent)
    {
        parkedAgents.Add(agent);
        if (parkedAgents.Count < agents.Count)
        {
            return;
        }

        // This runs inside an ML-Agents episode callback, which is itself inside a physics step.
        // Rebuilding the world from here destroys colliders while Unity is still working with them,
        // so the generation is started from the next FixedUpdate instead. Every agent is parked by
        // this point, so nothing moves in the meantime.
        generationPending = true;
    }

    private void FixedUpdate()
    {
        // Same reason the rebuild itself is deferred to here: cutting episodes short from inside a
        // physics callback would tear down colliders Unity is still working with.
        if (interruptPending)
        {
            interruptPending = false;
            InterruptFlyingAgents();
            return;
        }

        if (!generationPending)
        {
            return;
        }
        generationPending = false;
        StartNextGeneration();
    }

    // Waiting for the whole fleet to crash before the map is allowed to change puts the rebuild
    // tens of thousands of steps behind the interval, because one plane that happens to be flying
    // well holds up every other one. Cutting the still-flying episodes short parks every agent
    // back on the runway, which is the state the rebuild path below already expects.
    private void InterruptFlyingAgents()
    {
        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i] == null || parkedAgents.Contains(agents[i]))
            {
                continue;
            }
            agents[i].InterruptEpisode();
        }
    }

    private void StartNextGeneration()
    {
        generationNumber++;

        if (mapBuilder != null && mapGenerationEnabled && rebuildDue)
        {
            rebuildDue = false;
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
        // Physics queries read the last poses pushed to the physics scene, and auto sync is off in
        // the project settings. A rebuild has just created a whole new terrain, so without this the
        // probes below can miss it entirely and every gate would keep its stale position.
        Physics.SyncTransforms();

        placedGates.Clear();
        int missing = 0;
        int failed = 0;
        for (int i = 0; i < gates.Count; i++)
        {
            if (gates[i] == null)
            {
                // A destroyed gate means the checkpoint group did not survive a rebuild, which
                // leaves the agents with nothing to fly towards.
                missing++;
                continue;
            }

            Vector3 spot;
            if (!FindGateSpot(out spot))
            {
                // Keeping the old position is a poor outcome after a rebuild, since it may now sit
                // inside a hill where no agent can ever reach it.
                failed++;
                continue;
            }

            gates[i].transform.position = spot;
            placedGates.Add(spot);
        }

        if (missing > 0)
        {
            Debug.LogError(missing + " of " + gates.Count + " checkpoints no longer exist. They were"
                + " destroyed with the world root instead of being detached, so the agents have no"
                + " reachable gates.");
        }
        if (failed > 0)
        {
            Debug.LogWarning(failed + " of " + gates.Count + " checkpoints found no clear spot in "
                + gatePlacementAttempts + " attempts and kept their previous positions, which may be"
                + " buried in the current terrain.");
        }

        // The gates just moved; their trigger volumes would otherwise still be tested at the old
        // positions until the next physics step runs.
        Physics.SyncTransforms();
    }

    private bool FindGateSpot(out Vector3 spot)
    {
        for (int attempt = 0; attempt < gatePlacementAttempts; attempt++)
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
            if (!LiftUntilClear(ref candidate))
            {
                continue;
            }

            // Gates are triggers, so the clearance sweep above cannot see them and two gates would
            // happily land on the same spot. Compare against the ones already placed this round.
            if (OverlapsPlacedGate(candidate))
            {
                continue;
            }

            spot = candidate;
            return true;
        }

        spot = Vector3.zero;
        return false;
    }

    private bool OverlapsPlacedGate(Vector3 candidate)
    {
        for (int i = 0; i < placedGates.Count; i++)
        {
            if (Vector3.Distance(placedGates[i], candidate) < gateSeparation)
            {
                return true;
            }
        }
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
