using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class TerrainAgentScript : Agent
{
    [SerializeField] private List<AeroSurface> controlSurfaces = null;
    [SerializeField] private List<WheelCollider> wheels = null;
    [SerializeField] private float rollControlSensitivity = 0.2f;
    [SerializeField] private float pitchControlSensitivity = 0.2f;
    [SerializeField] private float yawControlSensitivity = 0.2f;

    [SerializeField] private bool verboseLogging = false;

    [SerializeField] private GameObject[] checkpoints;
    [SerializeField] private float checkpointReward = 10f;
    [SerializeField] private float crashPenalty = -8f;
    [SerializeField] private float seaLevelAltitude = 0f;

    [SerializeField] private float groundProbeDistance = 0.5f;
    [SerializeField] private float groundProbeUpOffset = 1f;
    [SerializeField] private LayerMask groundProbeMask = ~0;

    [SerializeField] private LayerMask worldMask = ~0;
    [SerializeField] private float maxTerrainClearance = 300f;

    [SerializeField] private float distanceRewardScale = 0.003f;
    [SerializeField] private float altitudeErrorNormalizer = 450f;
    [SerializeField] private float timeCostPerStep = -0.0002f;

    [SerializeField] private int gateStallLimit = 1500;
    [SerializeField] private float gateStallPenalty = -1f;

    [SerializeField] private Vector3 runwaySpawnPosition = new Vector3(250f, 25f, -3010f);

    [SerializeField, Range(0f, 1f)] private float departureGateChance = 0.5f;
    [SerializeField] private int departureGateIndex = 0;

    [SerializeField, Range(-1f, 1f)] private float pitch;
    [SerializeField, Range(-1f, 1f)] private float yaw;
    [SerializeField, Range(-1f, 1f)] private float roll;
    [SerializeField, Range(0f, 1f)] private float flap;
    [SerializeField, Range(-1f, 1f)] private float thrustPercent;

    private Vector3 dirToTarget;
    private GameObject activeCheckpoint;
    private int activeIndex;
    private float prevDist;
    private float bestDist;
    private int stallSteps;

    private float checkpointAmount;
    private float gateReassignCount;

    private float clearanceBelow;
    private float altitudeError;

    private TerrainAgentStats stats;
    private AircraftPhysics aircraftPhysics;
    private Rigidbody rb;
    private GenerationManager generationManager;
    private DecisionRequester decisionRequester;
    private bool isParked;

    public GameObject ActiveCheckpoint
    {
        get { return activeCheckpoint; }
    }

    public override void Initialize()
    {
        aircraftPhysics = GetComponent<AircraftPhysics>();
        rb = GetComponent<Rigidbody>();
        decisionRequester = GetComponent<DecisionRequester>();
        stats = new TerrainAgentStats(Academy.Instance.StatsRecorder);

        generationManager = FindFirstObjectByType<GenerationManager>();
        if (generationManager != null)
        {
            generationManager.RegisterAgent(this);
            generationManager.RegisterGates(checkpoints);
        }

        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
    }

    private void Park()
    {
        isParked = true;
        decisionRequester.enabled = false;
        rb.isKinematic = true;
        generationManager.ReportParked(this);
    }

    public void Release()
    {
        isParked = false;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        decisionRequester.enabled = true;
        SelectStartingCheckpoint();
    }

    private void SelectStartingCheckpoint()
    {
        if (Random.value < departureGateChance)
        {
            SelectCheckpoint(Mathf.Clamp(departureGateIndex, 0, checkpoints.Length - 1));
        }
        else
        {
            SelectCheckpoint(Random.Range(0, checkpoints.Length));
        }
    }

    private void SelectCheckpoint(int index)
    {
        activeIndex = index;
        activeCheckpoint = checkpoints[activeIndex];
        prevDist = Vector3.Distance(transform.position, activeCheckpoint.transform.position);
        bestDist = prevDist;
        stallSteps = 0;
    }

    private void SelectDifferentCheckpoint()
    {
        int offset = Random.Range(1, checkpoints.Length);
        SelectCheckpoint((activeIndex + offset) % checkpoints.Length);
    }

    public override void OnEpisodeBegin()
    {
        stats.EndEpisode(checkpointAmount, gateReassignCount);
        checkpointAmount = 0f;
        gateReassignCount = 0f;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.localPosition = runwaySpawnPosition;
        transform.localRotation = Quaternion.identity;

        pitch = 0f;
        roll = 0f;
        yaw = 0f;
        flap = 0f;
        thrustPercent = 0f;

        SelectStartingCheckpoint();

        if (generationManager != null)
        {
            Park();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        CalcValues();
        sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity) / 100f);
        sensor.AddObservation(transform.InverseTransformDirection(rb.angularVelocity) / 10f);
        sensor.AddObservation(transform.up);
        sensor.AddObservation(transform.forward);
        sensor.AddObservation(clearanceBelow);
        sensor.AddObservation(dirToTarget.normalized);
        sensor.AddObservation(dirToTarget.magnitude / 1000f);
        sensor.AddObservation(altitudeError);
        sensor.AddObservation(thrustPercent);
        sensor.AddObservation(flap / 0.3f);
    }

    private void CalcValues()
    {
        dirToTarget = transform.InverseTransformDirection(activeCheckpoint.transform.position - transform.position);
        clearanceBelow = ClearanceBelow();
        altitudeError = Mathf.Clamp(
            (activeCheckpoint.transform.position.y - transform.position.y) / altitudeErrorNormalizer,
            -1f, 1f);
    }

    private float ClearanceBelow()
    {
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit,
                             maxTerrainClearance,
                             worldMask, QueryTriggerInteraction.Ignore))
        {
            return 1f;
        }

        return (transform.position.y - hit.point.y) / maxTerrainClearance;
    }

    private bool IsAirborne()
    {
        return clearanceBelow * maxTerrainClearance >= 15f;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        actionsOut.ContinuousActions.Clear();
        actionsOut.DiscreteActions.Clear();
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (isParked)
        {
            return;
        }

        ActionSegment<float> continuousActions = actions.ContinuousActions;
        ActionSegment<int> discreteActions = actions.DiscreteActions;

        pitch = Mathf.Clamp(continuousActions[0], -1f, 1f);
        roll = Mathf.Clamp(continuousActions[1], -1f, 1f);
        yaw = Mathf.Clamp(continuousActions[2], -1f, 1f);
        thrustPercent = (Mathf.Clamp(continuousActions[3], -1f, 1f) + 1f) * 0.5f;

        if (discreteActions[0] == 1)
        {
            flap = 0.3f;
        }
        else
        {
            flap = 0f;
        }

        GiveRewards();

        if (IsAirborne())
        {
            stats.RecordFlight(rb.linearVelocity.magnitude, transform.position.y);
        }
    }

    private void GiveRewards()
    {
        float dist = Vector3.Distance(transform.position, activeCheckpoint.transform.position);
        AddReward((prevDist - dist) * distanceRewardScale);
        prevDist = dist;
        AddReward(timeCostPerStep);
        CheckGateProgress(dist);
    }

    private void CheckGateProgress(float dist)
    {
        if (dist < bestDist)
        {
            bestDist = dist;
            stallSteps = 0;
            return;
        }

        stallSteps++;
        if (stallSteps > gateStallLimit)
        {
            AddReward(gateStallPenalty);
            gateReassignCount++;
            SelectDifferentCheckpoint();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject != activeCheckpoint)
        {
            return;
        }

        AddReward(checkpointReward);
        checkpointAmount++;
        if (verboseLogging)
        {
            print("Plane collected " + checkpointAmount + " checkpoints in a row");
        }

        SelectDifferentCheckpoint();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            Crashed("Ground");
        }
        else if (collision.gameObject.CompareTag("Building"))
        {
            Crashed("Building");
        }
    }

    private void CheckGroundProbe()
    {
        Vector3 origin = transform.position + Vector3.up * groundProbeUpOffset;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                             groundProbeUpOffset + groundProbeDistance,
                             groundProbeMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        if (hit.collider.CompareTag("Ground"))
        {
            Crashed("Ground");
        }
        if (hit.collider.CompareTag("Building"))
        {
            Crashed("Building");
        }
        if (hit.collider.CompareTag("Runway"))
        {
            thrustPercent = 1f;
        }
    }

    private void FixedUpdate()
    {
        if (isParked)
        {
            return;
        }

        if (transform.position.y < seaLevelAltitude)
        {
            Crashed("Water");
            return;
        }

        CheckGroundProbe();
        SetControlSurfecesAngles(pitch, roll, yaw, flap);
        aircraftPhysics.SetThrustPercent(thrustPercent);
        foreach (WheelCollider wheel in wheels)
        {
            wheel.motorTorque = 0.01f;
            wheel.brakeTorque = 0.0f;
        }
    }

    public void SetControlSurfecesAngles(float pitchInput, float rollInput, float yawInput, float flapInput)
    {
        foreach (AeroSurface surface in controlSurfaces)
        {
            if (surface == null || !surface.IsControlSurface)
            {
                continue;
            }
            switch (surface.InputType)
            {
                case ControlInputType.Pitch:
                    surface.SetFlapAngle(pitchInput * pitchControlSensitivity * surface.InputMultiplyer);
                    break;
                case ControlInputType.Roll:
                    surface.SetFlapAngle(rollInput * rollControlSensitivity * surface.InputMultiplyer);
                    break;
                case ControlInputType.Yaw:
                    surface.SetFlapAngle(yawInput * yawControlSensitivity * surface.InputMultiplyer);
                    break;
                case ControlInputType.Flap:
                    surface.SetFlapAngle(flapInput * surface.InputMultiplyer);
                    break;
            }
        }
    }

    private void Crashed(string what)
    {
        stats.RecordCrash(what, rb.linearVelocity.y, rb.linearVelocity.magnitude);
        SetReward(crashPenalty);
        EndEpisode();
        if (verboseLogging)
        {
            print("Plane hit " + what);
        }
    }
}
