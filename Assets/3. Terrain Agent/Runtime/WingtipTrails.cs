using UnityEngine;

// Trails are built at runtime rather than stored on the prefab: the training scene holds 50 agents
// and 100 TrailRenderers at 10x time scale is both wasted work and visually meaningless.
public class WingtipTrails : MonoBehaviour
{
    [SerializeField] private float wingHalfSpan = 5.4f;
    [SerializeField] private float wingHeight = 2.05f;
    [SerializeField] private float wingOffsetZ = 0.83f;
    [SerializeField] private float trailTime = 2.4f;
    [SerializeField] private float trailWidth = 0.55f;
    [SerializeField] private float minimumSpeed = 38f;
    [SerializeField] private Color trailColor = new Color(1f, 1f, 1f, 0.5f);

    private TrailRenderer leftTrail;
    private TrailRenderer rightTrail;
    private Rigidbody body;

    private void Start()
    {
        if (!ShowcaseVisuals.IsActive())
        {
            enabled = false;
            return;
        }

        body = GetComponent<Rigidbody>();
        leftTrail = CreateTrail(new Vector3(-wingHalfSpan, wingHeight, wingOffsetZ));
        rightTrail = CreateTrail(new Vector3(wingHalfSpan, wingHeight, wingOffsetZ));
    }

    private TrailRenderer CreateTrail(Vector3 localPosition)
    {
        GameObject holder = new GameObject("WingtipTrail");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = localPosition;

        TrailRenderer trail = holder.AddComponent<TrailRenderer>();
        trail.time = trailTime;
        trail.startWidth = trailWidth;
        trail.endWidth = 0f;
        trail.minVertexDistance = 1.5f;
        trail.autodestruct = false;
        trail.emitting = false;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;

        Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetColor("_BaseColor", trailColor);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        trail.material = material;

        return trail;
    }

    private void Update()
    {
        if (leftTrail == null || body == null)
        {
            return;
        }

        // Real wingtip vapour only shows when the wing is working hard, so the trails are gated on
        // speed instead of streaming constantly and turning the sky into spaghetti.
        bool shouldEmit = body.linearVelocity.magnitude > minimumSpeed;
        leftTrail.emitting = shouldEmit;
        rightTrail.emitting = shouldEmit;
    }
}
