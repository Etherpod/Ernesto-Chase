using UnityEngine;

namespace ErnestoChase;

public class SpectatorCamera : MonoBehaviour
{
    [SerializeField]
    private PlayerAttachPoint attachPoint;
    [SerializeField]
    private OWCamera owCamera;

    private bool isErnestoCam;
    private uint playerID;
    private ErnestoManager ernestoManager;

    private void Awake()
    {
        if (GetComponent<ErnestoManager>())
        {
            ernestoManager = GetComponent<ErnestoManager>();
            isErnestoCam = true;
        }

        owCamera.GetComponent<PlanetaryFogImageEffect>().fogShader = Shader.Find("Hidden/PlanetaryFogImageEffect");
        owCamera.GetComponent<FlashbackScreenGrabImageEffect>()._downsampleShader = Shader.Find("Hidden/DownsampleImageEffect");
        owCamera.enabled = false;
        owCamera.gameObject.SetActive(true);

        ErnestoChase.Instance.spectatorCameras.Add(this);
    }

    public bool IsErnestoCam()
    {
        return isErnestoCam;
    }

    public OWCamera GetOWCamera()
    {
        return owCamera;
    }

    public void SetPlayerID(uint id)
    {
        isErnestoCam = false;
        playerID = id;
    }

    public bool CanSpectate()
    {
        return isErnestoCam ? 
            !ernestoManager.GetComponent<ErnestoState>().CaughtPlayer 
            : !ErnestoChase.QSBAPI.GetPlayerDead(playerID);
    }
    
    public void AttachPlayer()
    {
        ErnestoChase.WriteDebugMessage("attach");

        if (PlayerState.IsWearingSuit())
        {
            Locator.GetPlayerSuit().RemoveSuit();
        }

        Locator.GetToolModeSwapper().UnequipTool();

        foreach (var renderer in Locator.GetPlayerBody().GetComponentsInChildren<Renderer>())
        {
            renderer.forceRenderingOff = true;
        }

        ErnestoChase.WriteDebugMessage("Attach body: " + gameObject.GetAttachedOWRigidbody());
        attachPoint.AttachPlayer();

        GlobalMessenger.FireEvent("PlayerRepositioned");

        Locator.GetPlayerBody().GetComponent<PlayerResources>()._invincible = true;
        Locator.GetDeathManager()._invincible = true;
    }
}
