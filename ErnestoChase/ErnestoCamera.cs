using UnityEngine;

namespace ErnestoChase;

public class ErnestoCamera : MonoBehaviour
{
    public const int SNAPSHOT_RESOLUTION = 512;
    private static RenderTexture s_sharedSnapshotTexure;

    private OWCamera _camera;
    private RenderTexture _snapshotTexture;
    private NoiseImageEffect _noiseEffect;

    private int _frameDelay = 10;

    public static RenderTexture GetSharedSnapshotTexture()
    {
        if (s_sharedSnapshotTexure == null)
        {
            s_sharedSnapshotTexure = new RenderTexture(512, 512, 16);
            s_sharedSnapshotTexure.name = "ErnestoCameraSnapshot";
            s_sharedSnapshotTexure.hideFlags = HideFlags.HideAndDontSave;
            s_sharedSnapshotTexure.Create();
        }
        return s_sharedSnapshotTexure;
    }

    private void Awake()
    {
        _camera = this.GetComponent<OWCamera>();
        _camera.enabled = false;
        _noiseEffect = GetComponent<NoiseImageEffect>();
        _snapshotTexture = GetSharedSnapshotTexture();

        GetComponent<PlanetaryFogImageEffect>().fogShader = Shader.Find("Hidden/PlanetaryFogImageEffect");
        _noiseEffect._noiseShader = Shader.Find("Hidden/NoiseImageEffect");
    }

    private void OnDestroy()
    {
        _snapshotTexture = null;
    }

    public float GetInterferenceLevel()
    {
        return 0;
        return Mathf.InverseLerp(500f * 500f, 3000f * 3000f, (Locator.GetPlayerTransform().position - transform.position).sqrMagnitude);
    }

    public RenderTexture TakeSnapshot()
    {
        if (_noiseEffect != null)
        {
            _noiseEffect.strength = GetInterferenceLevel();
        }
        _camera.targetTexture = _snapshotTexture;
        _camera.Render();
        return _snapshotTexture;
    }
}