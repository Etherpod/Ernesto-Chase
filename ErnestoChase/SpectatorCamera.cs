using UnityEngine;

namespace ErnestoChase;

public class SpectatorCamera : MonoBehaviour
{
    [SerializeField]
    private SectorDetector sectorDetector;
    [SerializeField]
    private OWCamera owCamera;

    private bool isErnestoCam;
    private uint playerID;
    private ErnestoManager ernestoManager;

    public bool IsErnestoCam { get => isErnestoCam; }
    public OWCamera Camera { get => owCamera; }
    public SectorDetector Detector { get => sectorDetector; }

    private void Awake()
    {
        if (GetComponent<ErnestoManager>())
        {
            ernestoManager = GetComponent<ErnestoManager>();
            isErnestoCam = true;
            ErnestoChase.Instance.ernestoSpectatorCams.Add(this);
        }

        owCamera.GetComponent<PlanetaryFogImageEffect>().fogShader = Shader.Find("Hidden/PlanetaryFogImageEffect");
        owCamera.GetComponent<FlashbackScreenGrabImageEffect>()._downsampleShader = Shader.Find("Hidden/DownsampleImageEffect");
        owCamera.enabled = false;
        owCamera.gameObject.SetActive(true);
    }

    public void SetPlayerID(uint id)
    {
        isErnestoCam = false;
        playerID = id;
        sectorDetector = ErnestoChase.QSBInteraction.GetRemoteFluidDetector(playerID).GetAddComponent<SectorDetector>();
        sectorDetector.SetOccupantType(DynamicOccupant.Player);
    }

    public bool CanSpectate()
    {
        if (isErnestoCam)
        {
            return ernestoManager.CanSpectate();
        }
        else
        {
            return !ErnestoChase.QSBAPI.GetPlayerDead(playerID);
        }
    }
}
