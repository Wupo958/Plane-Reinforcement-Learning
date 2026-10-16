using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum CameraShotMode { Chase, Side, Orbit, TopDown, Far }

public class CinematicCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    // A plane that crashes sits parked on the runway until the rest of the fleet is down, which is
    // a long time to watch a stationary aircraft. Hand the shot to one that is still flying.
    [SerializeField] private bool followLiveAgent = true;
    [SerializeField] private CameraShotMode mode = CameraShotMode.Chase;
    [SerializeField] private bool autoCycle = true;
    [SerializeField] private float autoCycleSeconds = 14f;

    [SerializeField] private float positionSmoothTime = 0.45f;
    [SerializeField] private float rotationSharpness = 3.2f;
    [SerializeField] private float orbitSpeed = 14f;
    [SerializeField] private float lookAheadDistance = 28f;

    [SerializeField] private float chaseDistance = 26f;
    [SerializeField] private float chaseHeight = 7f;
    [SerializeField] private float sideDistance = 32f;
    [SerializeField] private float orbitRadius = 45f;
    [SerializeField] private float topDownHeight = 95f;
    [SerializeField] private float farDistance = 150f;
    [SerializeField] private float farHeight = 45f;

    // An episode reset teleports the plane back to the runway. Without this the camera would spend
    // the next few seconds flying across the island to catch up.
    [SerializeField] private float snapDistance = 120f;

    private Vector3 velocity;
    private float orbitAngle;
    private float cycleTimer;
    private Vector3 lastTargetPosition;
    private bool hasLastPosition;
    private GenerationManager generationManager;
    private CheckpointHighlighter highlighter;
    private TerrainAgentScript watched;
    private TerrainAgentScript trailAgent;

    private void Start()
    {
        if (target == null)
        {
            target = transform.parent;
        }
        if (target == null)
        {
            enabled = false;
            return;
        }

        generationManager = FindFirstObjectByType<GenerationManager>();
        watched = target.GetComponent<TerrainAgentScript>();

        // Parented to the aircraft the camera inherits every roll and every physics step, which is
        // most of what reads as jitter. It tracks in world space instead.
        transform.SetParent(null, true);
        SnapToDesired();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.cKey.wasPressedThisFrame)
        {
            NextMode();
        }
        if (keyboard.vKey.wasPressedThisFrame)
        {
            autoCycle = !autoCycle;
        }
    }

    private void NextMode()
    {
        int next = ((int)mode + 1) % 5;
        mode = (CameraShotMode)next;
        cycleTimer = 0f;
    }

    private void LateUpdate()
    {
        if (followLiveAgent)
        {
            CutToSomeoneFlying();
            KeepHighlightOnWatched();
        }

        if (target == null)
        {
            return;
        }

        if (autoCycle)
        {
            cycleTimer += Time.deltaTime;
            if (cycleTimer >= autoCycleSeconds)
            {
                NextMode();
            }
        }

        orbitAngle += orbitSpeed * Time.deltaTime;

        if (hasLastPosition && Vector3.Distance(target.position, lastTargetPosition) > snapDistance)
        {
            SnapToDesired();
        }
        lastTargetPosition = target.position;
        hasLastPosition = true;

        Vector3 desiredPosition = DesiredPosition();
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, positionSmoothTime);

        Quaternion desiredRotation = DesiredRotation();
        float blend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);
    }

    private void CutToSomeoneFlying()
    {
        TerrainAgentScript replacement = PickShot();
        if (replacement == null || replacement == watched)
        {
            // Between generations the whole fleet is parked, so there is nobody to cut to and the
            // shot stays where it is rather than flicking about.
            return;
        }

        watched = replacement;
        target = replacement.transform;

        // A map rebuild replaces the highlighter, so the cached one goes stale rather than null
        // only on the frames in between. Re-resolving on a cut is cheap and always current.
        if (highlighter == null)
        {
            highlighter = FindFirstObjectByType<CheckpointHighlighter>();
        }
        if (highlighter != null)
        {
            highlighter.Watch(replacement);
        }

        SnapToDesired();
        hasLastPosition = false;
    }

    // A map rebuild destroys the highlighter, and RewireShowcase attaches a fresh one that binds
    // itself to whatever FindFirstObjectByType happens to return. That is rarely the plane on
    // camera, so the wrong gate would glow until the next cut happened to correct it. Re-claiming
    // the replacement as soon as it appears keeps the highlight and the shot on one aircraft.
    private void KeepHighlightOnWatched()
    {
        if (watched == null || highlighter != null)
        {
            return;
        }

        highlighter = FindFirstObjectByType<CheckpointHighlighter>();
        if (highlighter != null)
        {
            highlighter.Watch(watched);
        }
    }

    // The showcase aircraft is the one WireShowcase bakes WingtipTrails onto, so it is the shot
    // the scene was dressed for. It wins whenever it is flying, including cutting back to it once
    // it respawns, and the rest of the fleet is only ever cover for while it sits parked.
    private TerrainAgentScript PickShot()
    {
        if (generationManager == null)
        {
            return null;
        }

        IReadOnlyList<TerrainAgentScript> candidates = generationManager.Agents;

        TerrainAgentScript trailed = FindTrailAgent(candidates);
        if (trailed != null && !trailed.IsParked)
        {
            return trailed;
        }

        // Holding the current shot while it lasts stops the camera walking back down the fleet
        // list to a lower-numbered aircraft every time the one it is on outlives it.
        if (watched != null && !watched.IsParked)
        {
            return watched;
        }

        return FindFlyingAgent(candidates);
    }

    // Only one agent carries the component, so the lookup is cached; it is re-resolved if that
    // agent is ever destroyed, which is what the null check on the cache is for.
    private TerrainAgentScript FindTrailAgent(IReadOnlyList<TerrainAgentScript> candidates)
    {
        if (trailAgent != null)
        {
            return trailAgent;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] != null && candidates[i].GetComponent<WingtipTrails>() != null)
            {
                trailAgent = candidates[i];
                return trailAgent;
            }
        }
        return null;
    }

    private TerrainAgentScript FindFlyingAgent(IReadOnlyList<TerrainAgentScript> candidates)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] != null && !candidates[i].IsParked)
            {
                return candidates[i];
            }
        }
        return null;
    }

    private void SnapToDesired()
    {
        transform.position = DesiredPosition();
        transform.rotation = DesiredRotation();
        velocity = Vector3.zero;
    }

    // Every shot is built from a yaw-only frame so the camera never rolls with the aircraft.
    private Quaternion HeadingOnly()
    {
        return Quaternion.Euler(0f, target.eulerAngles.y, 0f);
    }

    private Vector3 DesiredPosition()
    {
        Quaternion heading = HeadingOnly();

        if (mode == CameraShotMode.Side)
        {
            return target.position + heading * new Vector3(sideDistance, 6f, 2f);
        }
        if (mode == CameraShotMode.Orbit)
        {
            float radians = orbitAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(radians) * orbitRadius, 13f, Mathf.Sin(radians) * orbitRadius);
            return target.position + offset;
        }
        if (mode == CameraShotMode.TopDown)
        {
            return target.position + new Vector3(0f, topDownHeight, -6f);
        }
        if (mode == CameraShotMode.Far)
        {
            return target.position + heading * new Vector3(0f, farHeight, -farDistance);
        }
        return target.position + heading * new Vector3(0f, chaseHeight, -chaseDistance);
    }

    private Quaternion DesiredRotation()
    {
        Vector3 focus = target.position;
        if (mode == CameraShotMode.Chase || mode == CameraShotMode.Far)
        {
            focus += target.forward * lookAheadDistance;
        }

        Vector3 toFocus = focus - transform.position;
        if (toFocus.sqrMagnitude < 0.001f)
        {
            return transform.rotation;
        }

        Vector3 up = Vector3.up;
        if (mode == CameraShotMode.TopDown)
        {
            up = HeadingOnly() * Vector3.forward;
        }
        return Quaternion.LookRotation(toFocus.normalized, up);
    }
}
