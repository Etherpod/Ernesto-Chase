using Mono.Cecil.Cil;
using System.Collections;
using UnityEngine;

namespace ErnestoChase;

public class ErnestoEffects : MonoBehaviour
{
    public delegate void WhiteHoleEvent();
    public event WhiteHoleEvent OnExitWhiteHole;
    public delegate void BlackHoleEvent(bool fromSpace, bool toSpace);
    public event BlackHoleEvent OnEnterBlackHole;

    [SerializeField]
    private OWAudioSource loopingAudio;
    [SerializeField]
    private OWAudioSource oneShotAudio;
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private GameObject ernestoMesh;
    [SerializeField]
    private Light anglerLight;
    [SerializeField]
    private SingularityWarpEffect blackHolePrefab;
    [SerializeField]
    private SingularityWarpEffect whiteHolePrefab;

    private ErnestoState state;
    private SkinnedMeshRenderer ernestoRenderer;
    private SingularityWarpEffect blackHole;
    private SingularityWarpEffect whiteHole;
    private float baseLightRange;
    private float baseMeshScale;

    private Coroutine audioTransition;
    private float baseLoopingAudioVolume;

    private bool cachedFromSpace;
    private bool cachedToSpace;

    private void Awake()
    {
        state = GetComponent<ErnestoState>();
        animator = GetComponentInChildren<Animator>();
        loopingAudio = GetComponentInChildren<OWAudioSource>();
        ernestoRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        baseMeshScale = ernestoMesh.transform.localScale.magnitude;
        baseLightRange = anglerLight.range;
        baseLoopingAudioVolume = loopingAudio.GetMaxVolume();

        AssetBundleUtilities.ReplaceShaders(blackHolePrefab.gameObject);
        AssetBundleUtilities.ReplaceShaders(whiteHolePrefab.gameObject);
        blackHolePrefab._warpedObjectGeometry = ernestoMesh;
        whiteHolePrefab._warpedObjectGeometry = ernestoMesh;
        blackHole = Instantiate(blackHolePrefab);
        whiteHole = Instantiate(whiteHolePrefab);

        ernestoMesh.transform.localScale = Vector3.zero;
        anglerLight.range = baseLightRange * (ernestoMesh.transform.localScale.magnitude / baseMeshScale);
        if (ErnestoChase.Instance.StealthMode)
        {
            anglerLight.intensity = 0f;
        }
    }

    private void Update()
    {
        anglerLight.range = baseLightRange * (ernestoMesh.transform.localScale.magnitude / baseMeshScale);
    }

    public void CreateWhiteHole()
    {
        whiteHole.transform.parent = transform.parent;
        whiteHole.transform.localPosition = transform.localPosition;
        whiteHole.WarpObjectIn(2f);
        whiteHole.singularityController.OnCreation += OnWhiteHoleCreated;
    }

    private void OnWhiteHoleCreated()
    {
        whiteHole.singularityController.OnCreation -= OnWhiteHoleCreated;
        if (!state.ErnestoReleased)
        {
            animator.SetTrigger("Impulse");
            oneShotAudio.PlayOneShot(AudioType.DBAnglerfishDetectTarget, 0.8f);
            loopingAudio.AssignAudioLibraryClip(AudioType.DBAnglerfishChasing_LP);
            if (!ErnestoChase.Instance.StealthMode)
            {
                loopingAudio.FadeIn(1f);
            }
        }
        OnExitWhiteHole?.Invoke();
    }

    public void SetTravelMode(bool isSpace)
    {
        if (isSpace)
        {
            if (ErnestoChase.Instance.SpaceAccelerationType == "Timed")
            {
                if (audioTransition != null)
                {
                    StopCoroutine(audioTransition);
                }
                audioTransition = ErnestoChase.Instance.StealthMode ? null : StartCoroutine(SpaceAudioTransition());
            }
        }
        else
        {
            if (audioTransition != null)
            {
                StopCoroutine(audioTransition);
            }
            audioTransition = ErnestoChase.Instance.StealthMode ? null : StartCoroutine(AtmosphereAudioTransition());
        }
    }

    public void OnTeleportStarted(bool fromSpace, bool toSpace)
    {
        blackHole.transform.parent = transform.parent;
        blackHole.transform.localPosition = transform.localPosition;
        blackHole.WarpObjectOut(2f);
        cachedFromSpace = fromSpace;
        cachedToSpace = toSpace;
        blackHole.singularityController.OnCollapse += HandleBlackHoleCollapse;
        loopingAudio.FadeOut(1f);
    }

    public void OnProximityRoar(bool inRange)
    {
        if (inRange)
        {
            oneShotAudio.PlayOneShot(AudioType.DBAnglerfishDetectTarget, 0.8f);
            loopingAudio.FadeIn(1f);
        }
        else
        {
            loopingAudio.FadeOut(3f);
        }
    }

    private void HandleBlackHoleCollapse()
    {
        OnBlackHoleCollapse(cachedFromSpace, cachedToSpace);
    }

    private void OnBlackHoleCollapse(bool fromSpace, bool toSpace)
    {
        blackHole.singularityController.OnCollapse -= HandleBlackHoleCollapse;
        OnEnterBlackHole?.Invoke(fromSpace, toSpace);

        whiteHole.transform.parent = transform.parent;
        whiteHole.transform.localPosition = transform.localPosition;
        whiteHole.WarpObjectIn(2f);
        whiteHole.singularityController.OnCreation += OnWhiteHoleCreated;
        if (!ErnestoChase.Instance.StealthMode)
        {
            loopingAudio.FadeIn(1f);
        }
    }

    private IEnumerator SpaceAudioTransition()
    {
        loopingAudio.FadeOut(0.5f);
        yield return new WaitForSeconds(0.5f);
        loopingAudio.SetMaxVolume(1f);
        loopingAudio.spatialBlend = 0f;
        loopingAudio.SetTrack(OWAudioMixer.TrackName.Environment_Unfiltered);
        loopingAudio.FadeIn(ErnestoChase.Instance.SpaceTimer, true);
        yield return new WaitForSeconds(ErnestoChase.Instance.SpaceTimer > 12f 
            ? ErnestoChase.Instance.SpaceTimer - 7f : ErnestoChase.Instance.SpaceTimer * 0.8f);
        loopingAudio.PlayOneShot(AudioType.DBAnglerfishDetectTarget, 1f);
        audioTransition = null;
    }

    private IEnumerator AtmosphereAudioTransition()
    {
        loopingAudio.FadeOut(0.5f);
        yield return new WaitForSeconds(0.5f);
        loopingAudio.SetMaxVolume(baseLoopingAudioVolume);
        loopingAudio.spatialBlend = 1f;
        loopingAudio.SetTrack(OWAudioMixer.TrackName.Environment);
        loopingAudio.FadeIn(1f);
        audioTransition = null;
    }

    public void OnCaughtPlayer()
    {
        loopingAudio.FadeOut(2f);
        animator.SetTrigger("Stop");
    }
}
