using UnityEngine;
using ErnestoChase.ErnestoAI;
using ErnestoChase.PlayerErnesto;

namespace ErnestoChase.Spectating;

public abstract class SpectatorCamera : MonoBehaviour
{
    [SerializeField]
    protected SectorDetector _sectorDetector;
    [SerializeField]
    protected OWCamera _owCamera;
    
    protected AudioListener _audioListener;
    
    public virtual OWCamera Camera => _owCamera;
    public virtual SectorDetector Detector => _sectorDetector;
    public virtual AudioListener AudioListener => _audioListener;

    protected virtual void Start()
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
