using UnityEngine;

// One route holds the polyline once and every car on it just carries a distance along it, so a
// hundred cars do not each serialise their own copy of a 3 km highway.
public class TrafficRoute : MonoBehaviour
{
    [SerializeField] private Vector3[] points;

    private float[] cumulative;
    private float totalLength;

    public float TotalLength
    {
        get
        {
            EnsureBuilt();
            return totalLength;
        }
    }

    public void SetPoints(Vector3[] value)
    {
        points = value;
        cumulative = null;
    }

    private void EnsureBuilt()
    {
        if (cumulative != null)
        {
            return;
        }
        if (points == null || points.Length < 2)
        {
            cumulative = new float[0];
            totalLength = 0f;
            return;
        }

        cumulative = new float[points.Length];
        cumulative[0] = 0f;
        for (int i = 1; i < points.Length; i++)
        {
            cumulative[i] = cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        }
        totalLength = cumulative[points.Length - 1];
    }

    public bool Sample(float distance, out Vector3 position, out Vector3 forward)
    {
        EnsureBuilt();
        position = Vector3.zero;
        forward = Vector3.forward;
        if (points == null || points.Length < 2 || totalLength <= 0.01f)
        {
            return false;
        }

        float wrapped = Mathf.Repeat(distance, totalLength);

        int low = 0;
        int high = points.Length - 1;
        while (low < high - 1)
        {
            int mid = (low + high) / 2;
            if (cumulative[mid] <= wrapped)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        float segmentLength = cumulative[low + 1] - cumulative[low];
        float t = 0f;
        if (segmentLength > 0.0001f)
        {
            t = (wrapped - cumulative[low]) / segmentLength;
        }

        position = Vector3.Lerp(points[low], points[low + 1], t);
        Vector3 direction = points[low + 1] - points[low];
        if (direction.sqrMagnitude > 0.0001f)
        {
            forward = direction.normalized;
        }
        return true;
    }
}
