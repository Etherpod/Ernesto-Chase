using UnityEngine;
using ErnestoChase.ErnestoAI;
using ErnestoChase.PlayerErnesto;

namespace ErnestoChase.Spectating;

public abstract class SpectatorCamera : MonoBehaviour
{
    [SerializeField]
    protected SectorDetector _sectorDetector;
    [SerializeField]
    private OWCamera _owCamera;
    
    protected AudioListener _audioListener;
    
    public OWCamera Camera => _owCamera;
    public SectorDetector Detector => _sectorDetector;
    public AudioListener AudioListener => _audioListener;

    protected virtual void Awake()
    {
        _audioListener = _owCamera.GetComponent<AudioListener>();
        _owCamera.GetComponent<PlanetaryFogImageEffect>().fogShader = Shader.Find("Hidden/PlanetaryFogImageEffect");
        _owCamera.GetComponent<FlashbackScreenGrabImageEffect>()._downsampleShader = Shader.Find("Hidden/DownsampleImageEffect");
        _owCamera.GetComponent<HeightmapAmbientLightRenderer>()._lightShader = Shader.Find("Hidden/HeightmapAmbientLight");
        _owCamera.enabled = false;
        _owCamera.gameObject.SetActive(true);
    }

    public abstract bool CanSpectate();
}
