using UnityEngine;

public class CheckpointGate : MonoBehaviour
{
    [SerializeField] private float spinSpeed = 12f;
    [SerializeField] private float pulseSpeed = 2.2f;
    [SerializeField] private Color activeColor = new Color(0.15f, 1f, 0.75f, 1f);
    [SerializeField] private Color idleColor = new Color(0.08f, 0.45f, 0.72f, 1f);
    [SerializeField] private float activeEmission = 3.4f;
    [SerializeField] private float idleEmission = 0.35f;

    private MeshRenderer[] ringRenderers;
    private MaterialPropertyBlock propertyBlock;
    private bool isHighlighted;
    private float phase;

    private void Awake()
    {
        ringRenderers = GetComponentsInChildren<MeshRenderer>();
        RestoreMissingRingAssets();
        propertyBlock = new MaterialPropertyBlock();
        phase = Random.Range(0f, 10f);
        Apply(0f);
    }

    // The ring mesh and material are baked into Generated/, which is build output rather than
    // source, so a checkout without it deserializes every ring with a null mesh and material.
    // The gates are the only part of the world that shows the damage: MapRuntimeBuilder throws
    // the rest of it away on load and rebuilds it procedurally, but it deliberately keeps the
    // baked Checkpoints group alive, references and all. Rebuilding the pair here is one torus
    // and one material for the whole scene, and costs nothing when the assets are present.
    private static Mesh fallbackRing;
    private static Material fallbackMaterial;

    private void RestoreMissingRingAssets()
    {
        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>();
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i] != null && filters[i].sharedMesh == null)
            {
                filters[i].sharedMesh = FallbackRing();
            }
        }

        for (int i = 0; i < ringRenderers.Length; i++)
        {
            if (ringRenderers[i] != null && ringRenderers[i].sharedMaterial == null)
            {
                ringRenderers[i].sharedMaterial = FallbackMaterial();
            }
        }
    }

    // Same torus the baker builds, so a restored gate is the same size as a baked one.
    private static Mesh FallbackRing()
    {
        if (fallbackRing == null)
        {
            fallbackRing = TerrainMapBuilder.BuildTorus(38f, 2.6f, 40, 8);
            fallbackRing.name = "GateRing";
        }
        return fallbackRing;
    }

    // Emission has to be on for the keyword to be compiled in, because the pulse in Apply only
    // drives _EmissionColor through a property block and a block cannot toggle a keyword.
    private static Material FallbackMaterial()
    {
        if (fallbackMaterial == null)
        {
            fallbackMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            fallbackMaterial.name = "MapGate";
            fallbackMaterial.SetColor("_BaseColor", new Color(0.05f, 0.85f, 0.95f, 1f));
            fallbackMaterial.SetFloat("_Smoothness", 0.4f);
            fallbackMaterial.EnableKeyword("_EMISSION");
            fallbackMaterial.SetColor("_EmissionColor", new Color(0.1f, 1.6f, 1.9f, 1f));
            fallbackMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        return fallbackMaterial;
    }

    public void SetHighlighted(bool highlighted)
    {
        isHighlighted = highlighted;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        float pulse = 0f;
        if (isHighlighted)
        {
            pulse = (Mathf.Sin((Time.time + phase) * pulseSpeed) + 1f) * 0.5f;
        }
        Apply(pulse);
    }

    private void Apply(float pulse)
    {
        // The guard used to check ringRenderers and then dereference propertyBlock, which is a
        // different field. Whenever that block was still null the throw happened here, before the
        // SetPropertyBlock loop below ever ran, so the rings kept the raw shared material and the
        // gates were effectively invisible. Both are now established before they are used.
        if (ringRenderers == null)
        {
            ringRenderers = GetComponentsInChildren<MeshRenderer>();
        }
        if (ringRenderers.Length == 0)
        {
            return;
        }
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        Color tint = idleColor;
        float emission = idleEmission;
        if (isHighlighted)
        {
            tint = activeColor;
            emission = Mathf.Lerp(activeEmission * 0.55f, activeEmission, pulse);
        }

        propertyBlock.SetColor("_BaseColor", tint);
        propertyBlock.SetColor("_EmissionColor", tint * emission);

        for (int i = 0; i < ringRenderers.Length; i++)
        {
            if (ringRenderers[i] == null)
            {
                continue;
            }
            ringRenderers[i].SetPropertyBlock(propertyBlock);
        }
    }
}
