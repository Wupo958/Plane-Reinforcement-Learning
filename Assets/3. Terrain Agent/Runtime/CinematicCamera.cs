using UnityEngine;
using UnityEngine.InputSystem;

public enum CameraShotMode { Chase, Side, Orbit, TopDown, Far }

public class CinematicCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
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
