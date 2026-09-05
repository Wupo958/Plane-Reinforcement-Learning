using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.InputSystem;

public class PrototypeAgentScript : Agent
{
    [SerializeField]
    List<AeroSurface> controlSurfaces = null;
    [SerializeField]
    List<WheelCollider> wheels = null;
    [SerializeField]
    float rollControlSensitivity = 0.2f;
    [SerializeField]
    float pitchControlSensitivity = 0.2f;
    [SerializeField]
    float yawControlSensitivity = 0.2f;

    [SerializeField]
    bool verboseLogging = false;

    [SerializeField]
    GameObject[] checkpoints;

    [SerializeField]
    float groundProbeDistance = 0.5f;
    [SerializeField]
    float groundProbeUpOffset = 1f;
    [SerializeField]
    LayerMask groundProbeMask = ~0;

    [Range(-1, 1)]
    public float Pitch;
    [Range(-1, 1)]
    public float Yaw;
    [Range(-1, 1)]
    public float Roll;
    [Range(0, 1)]
    public float Flap;
    [Range(-1, 1)]
    public float thrustPercent;

    private Vector3 dirToTarget;
    private GameObject activeCheckpoint;
    private int activeIndex;
    private float prevDist;

    private float checkpointAmount = 0;

    AircraftPhysics aircraftPhysics;
    Rigidbody rb;

    public override void Initialize()
    {
        aircraftPhysics = GetComponent<AircraftPhysics>();
        rb = GetComponent<Rigidbody>();
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
    }

    private void SelectCheckpoint(int index)
    {
        activeIndex = index;
        activeCheckpoint = checkpoints[activeIndex];
        prevDist = Vector3.Distance(transform.position, activeCheckpoint.transform.position);
    }

    public override void OnEpisodeBegin()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.localPosition = new Vector3(0, 0, -690);
        transform.localRotation = Quaternion.identity;
        Academy.Instance.StatsRecorder.Add("Checkpoints/PerEpisode", checkpointAmount);
        checkpointAmount = 0;
        SelectCheckpoint(0);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        CalcValues();
        sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity)/100);
        sensor.AddObservation(transform.InverseTransformDirection(rb.angularVelocity)/10);
        sensor.AddObservation(transform.up);
        sensor.AddObservation(transform.forward);
        sensor.AddObservation(Mathf.Clamp01(transform.position.y / 200f));
        sensor.AddObservation(dirToTarget.normalized);
        sensor.AddObservation(dirToTarget.magnitude / 1000);
        sensor.AddObservation(thrustPercent);
        sensor.AddObservation(Flap / 0.3f);
    }

    private void CalcValues()
    {
        dirToTarget = transform.InverseTransformDirection(activeCheckpoint.transform.position - transform.position);
    }

    // Called if the trainer connection drops. Without an override, ML-Agents logs a
    // stack-traced warning every step per agent, which floods the Editor log.
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        actionsOut.ContinuousActions.Clear();
        actionsOut.DiscreteActions.Clear();
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var c = actions.ContinuousActions;
        var d = actions.DiscreteActions;

        Pitch = Mathf.Clamp(c[0], -1f, 1f);
        Roll = Mathf.Clamp(c[1], -1f, 1f);
        Yaw = Mathf.Clamp(c[2], -1f, 1f);
        thrustPercent = (Mathf.Clamp(c[3], -1f, 1f) + 1) * 0.5f;

        Flap = d[0] == 1 ? 0.3f : 0f;

        GiveRewards();
    }

    private void GiveRewards()
    {
        float dist = Vector3.Distance(transform.position, activeCheckpoint.transform.position);
        AddReward((prevDist - dist) * 0.001f);
        prevDist = dist;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject != activeCheckpoint) return;

        AddReward(1f);
        checkpointAmount++;
        if (verboseLogging) print($"Plane collected {checkpointAmount} checkpoints in a row");

        int offset = Random.Range(1, checkpoints.Length);
        SelectCheckpoint((activeIndex + offset) % checkpoints.Length);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            TouchedGround();
        }
    }

    private void CheckGroundProbe()
    {
        Vector3 origin = transform.position + Vector3.up * groundProbeUpOffset;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                             groundProbeUpOffset + groundProbeDistance,
                             groundProbeMask, QueryTriggerInteraction.Ignore))
            return;

        if (hit.collider.CompareTag("Ground"))
        {
            TouchedGround();
        }
        if (hit.collider.CompareTag("Runway"))
        {
            AddReward(-0.00001f);
            thrustPercent = 1;
        }
    }

    private void FixedUpdate()
    {
        CheckGroundProbe();
        SetControlSurfecesAngles(Pitch, Roll, Yaw, Flap);
        aircraftPhysics.SetThrustPercent(thrustPercent);
        foreach (var wheel in wheels)
        {
            // small torque to wake up wheel collider
            wheel.motorTorque = 0.01f;
            wheel.brakeTorque = 0.0f;
        }
    }

    public void SetControlSurfecesAngles(float pitch, float roll, float yaw, float flap)
    {
        foreach (var surface in controlSurfaces)
        {
            if (surface == null || !surface.IsControlSurface) continue;
            switch (surface.InputType)
            {
                case ControlInputType.Pitch:
                    surface.SetFlapAngle(pitch * pitchControlSensitivity * surface.InputMultiplyer);
                    break;
                case ControlInputType.Roll:
                    surface.SetFlapAngle(roll * rollControlSensitivity * surface.InputMultiplyer);
                    break;
                case ControlInputType.Yaw:
                    surface.SetFlapAngle(yaw * yawControlSensitivity * surface.InputMultiplyer);
                    break;
                case ControlInputType.Flap:
                    surface.SetFlapAngle(Flap * surface.InputMultiplyer);
                    break;
            }
        }
    }

    private void TouchedGround()
    {
        SetReward(-1f);
        EndEpisode();
        if (verboseLogging) print("Plane hit the ground");
    }
}
