using UnityEngine;

public class ShowcaseVisuals : MonoBehaviour
{
    [SerializeField] private bool enableShowcase = true;

    [SerializeField] private GameObject trafficRoot;
    [SerializeField] private GameObject smokeRoot;
    [SerializeField] private GameObject cloudRoot;

    private static ShowcaseVisuals instance;
    private static bool searched;

    public static bool IsActive()
    {
        if (!searched)
        {
            instance = FindFirstObjectByType<ShowcaseVisuals>();
            searched = true;
        }
        if (instance == null)
        {
            return false;
        }
        return instance.enableShowcase;
    }

    private void Awake()
    {
        instance = this;
        searched = true;

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
