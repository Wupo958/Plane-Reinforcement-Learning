using UnityEngine;

public class ShowcaseVisuals : MonoBehaviour
{
    [SerializeField] private bool enableShowcase = true;

    [SerializeField] private GameObject trafficRoot;
    [SerializeField] private GameObject smokeRoot;
    [SerializeField] private GameObject cloudRoot;

    private static ShowcaseVisuals instance;

    public bool ShowcaseEnabled
    {
        get { return enableShowcase; }
    }

    public static bool IsActive()
    {
        // A map rebuild destroys the instance along with the world root. A cached "already
        // searched" flag would then report the showcase as off for the rest of the run, so the
        // lookup is retried whenever there is no live instance.
        if (instance == null)
        {
            instance = FindFirstObjectByType<ShowcaseVisuals>();
        }
        if (instance == null)
        {
            return false;
        }
        return instance.enableShowcase;
    }

    // Used by the runtime rebuild, which attaches a fresh copy to the newly generated showcase
    // root and has to carry the previous setting across.
    public void Configure(GameObject traffic, GameObject smoke, bool showcaseEnabled)
    {
        trafficRoot = traffic;
        smokeRoot = smoke;
        enableShowcase = showcaseEnabled;
        ApplyState();
    }

    private void Awake()
    {
        instance = this;
        ApplyState();
    }

    private void ApplyState()
    {
        if (enableShowcase)
        {
            return;
        }

        if (trafficRoot != null)
        {
            trafficRoot.SetActive(false);
        }
        if (smokeRoot != null)
        {
            smokeRoot.SetActive(false);
        }
    }
}
