using UnityEngine;

public class TrafficCar : MonoBehaviour
{
    [SerializeField] private TrafficRoute route;
    [SerializeField] private float speed = 16f;
    [SerializeField] private float startDistance;
    [SerializeField] private float lateralOffset = 5.5f;
    [SerializeField] private float rideHeight = 0.25f;
    [SerializeField] private bool reversed;

    private float travelled;

    public void Configure(TrafficRoute assignedRoute, float initialDistance, float carSpeed, float offset, bool driveReversed)
    {
        route = assignedRoute;
        startDistance = initialDistance;
        speed = carSpeed;
        lateralOffset = offset;
        reversed = driveReversed;
    }

    private void Start()
    {
        travelled = startDistance;
        Move(0f);
    }

    private void Update()
    {
        Move(Time.deltaTime);
    }

    private void Move(float deltaTime)
    {
        if (route == null)
        {
            return;
        }

        float direction = 1f;
        if (reversed)
        {
            direction = -1f;
        }
        travelled += speed * direction * deltaTime;

        Vector3 position;
        Vector3 forward;
        if (!route.Sample(travelled, out position, out forward))
        {
            return;
        }

        Vector3 facing = forward;
        if (reversed)
        {
            facing = -forward;
        }

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        transform.position = position + right * lateralOffset + Vector3.up * rideHeight;
        transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
    }
}
