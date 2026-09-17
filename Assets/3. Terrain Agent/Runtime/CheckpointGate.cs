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
        propertyBlock = new MaterialPropertyBlock();
        phase = Random.Range(0f, 10f);
        Apply(0f);
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
        if (ringRenderers == null)
        {
            return;
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
            ringRenderers[i].SetPropertyBlock(propertyBlock);
        }
    }
}
