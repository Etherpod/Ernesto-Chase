using Mono.Cecil.Cil;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

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
    private OWAudioSource musicAudio;
    [SerializeField] 
    private AudioLowPassFilter[] lowPassFilters;
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private GameObject ernestoMesh;
    [SerializeField]
    private Light anglerLight;
    [SerializeField]
    private Texture2D noBulbTex;
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
    private float baseMusicVolume;

    private bool cachedFromSpace;
    private bool cachedToSpace;
    
    private float filterLerp = 1f;

    private bool isFinalWarp = false;

    private void Awake()
    {
        state = GetComponent<ErnestoState>();
        animator = GetComponentInChildren<Animator>();
        loopingAudio = GetComponentInChildren<OWAudioSource>();
        ernestoRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        baseMeshScale = ernestoMesh.transform.localScale.magnitude;
        baseLightRange = anglerLight.range;
        baseLoopingAudioVolume = loopingAudio.GetMaxVolume();
        baseMusicVolume = musicAudio.GetMaxVolume();

        AssetBundleUtilities.ReplaceShaders(blackHolePrefab.gameObject);
        AssetBundleUtilities.ReplaceShaders(whiteHolePrefab.gameObject);
        blackHolePrefab._warpedObjectGeometry = ernestoMesh;
        whiteHolePrefab._warpedObjectGeometry = ernestoMesh;
        blackHole = Instantiate(blackHolePrefab);
        whiteHole = Instantiate(whiteHolePrefab);

        ernestoMesh.transform.localScale = Vector3.zero;
        anglerLight.range = baseLightRange * (ernestoMesh.transform.localScale.magnitude / baseMeshScale);

        if (state.DisableLight)
        {
            anglerLight.intensity = 0f;
            ernestoRenderer.material.SetTexture("_EmissionMap", noBulbTex);
            //enabled = false;
        }
        if (state.ErnestoMusic)
        {
            StartCoroutine(ReadAudioFiles());
        }
    }

    private void Update()
    {
        if (!state.DisableLight)
        {
            anglerLight.range = baseLightRange * (ernestoMesh.transform.localScale.magnitude / baseMeshScale);
        }
        
        UpdateMuffle();
    }

    private void UpdateMuffle(bool instant = false)
    {
        var toPlayer = Locator.GetPlayerTransform().position - transform.position;
        float distMult = Mathf.InverseLerp(50f * 50f, 500f * 500f, toPlayer.sqrMagnitude);
        bool muffle = false;
        if (distMult < 1f && Locator.GetAudioMixer()._playerInReverbVolume)
        {
            muffle = Physics.Raycast(transform.position, toPlayer, toPlayer.magnitude - 1f, 
                OWLayerMask.physicalMask);
        }

        if (instant)
        {
            filterLerp = muffle ? 1f : 0f;
        }
        else
        {
            filterLerp = Mathf.MoveTowards(filterLerp, muffle ? 1f : 0f, Time.deltaTime / 2f);
        }

        ErnestoChase.WriteDebugMessage(filterLerp);
        
        foreach (var filter in lowPassFilters)
        {
            filter.cutoffFrequency = Mathf.Lerp(22000, 3000, Mathf.Lerp(Mathf.Sqrt(filterLerp), 1f, distMult));
        }
    }

    private IEnumerator ReadAudioFiles()
    {
        AudioClip clip = null;

        List<string> files = [];
        files.AddRange(Directory.GetFiles(Path.Combine(ErnestoChase.Instance.ModHelper.Manifest.ModFolderPath, "ErnestoMusic"),
            "*.mp3", SearchOption.AllDirectories));
        files.AddRange(Directory.GetFiles(Path.Combine(ErnestoChase.Instance.ModHelper.Manifest.ModFolderPath, "ErnestoMusic"),
            "*.ogg", SearchOption.AllDirectories));
        files.AddRange(Directory.GetFiles(Path.Combine(ErnestoChase.Instance.ModHelper.Manifest.ModFolderPath, "ErnestoMusic"),
            "*.wav", SearchOption.AllDirectories));

        if (files.Count > 0)
        {
            int index = Random.Range(0, files.Count);

            var request = UnityWebRequestMultimedia.GetAudioClip("file:///" + files[index], UnityEngine.AudioType.UNKNOWN);
            yield return request.SendWebRequest();

            if (request.isDone && !request.isNetworkError)
            {
                clip = DownloadHandlerAudioClip.GetContent(request);
                ErnestoChase.WriteDebugMessage(clip);
            }
        }

        if (clip != null)
        {
            musicAudio.clip = clip;
        }
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
            if (!state.StealthMode)
            {
                loopingAudio.FadeIn(1f);
                musicAudio.Play();
            }
            UpdateMuffle(true);
        }
        OnExitWhiteHole?.Invoke();
    }

    public void SetTravelMode(bool isSpace)
    {
        if (isSpace)
        {
            if (state.SpaceAccelerationType == "Timed")
            {
                if (audioTransition != null)
                {
                    StopCoroutine(audioTransition);
                }
                audioTransition = state.StealthMode ? null : StartCoroutine(SpaceAudioTransition());
            }
        }
        else
        {
            if (audioTransition != null)
            {
                StopCoroutine(audioTransition);
            }
            audioTransition = state.StealthMode ? null : StartCoroutine(AtmosphereAudioTransition());
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
        musicAudio.FadeOut(1f, OWAudioSource.FadeOutCompleteAction.PAUSE);
    }

    public void OnTakeShortcut()
    {
        oneShotAudio.PlayOneShot(AudioType.DBAnglerfishDetectDisturbance, 0.8f);
    }

    public void OnUpdateVisibility(bool visible)
    {
        if (!visible)
        {
            if (loopingAudio.GetLocalVolume() == 0f)
            {
                loopingAudio.SetLocalVolume(1f);
                musicAudio.SetLocalVolume(1f);
                musicAudio.Play();
            }
            if (!animator.enabled)
            {
                animator.enabled = true;
            }
        }
        else
        {
            if (loopingAudio.GetLocalVolume() > 0f)
            {
                loopingAudio.SetLocalVolume(0f);
                musicAudio.SetLocalVolume(0f);
                musicAudio.Pause();
            }
            if (animator.enabled)
            {
                animator.enabled = false;
            }
        }
    }

    public void OnProximityRoar(bool inRange)
    {
        if (inRange)
        {
            oneShotAudio.PlayOneShot(AudioType.DBAnglerfishDetectTarget, 0.8f);
            loopingAudio.FadeIn(1f);
            musicAudio.FadeIn(0.1f);
        }
        else
        {
            loopingAudio.FadeOut(3f);
            musicAudio.FadeOut(3f, OWAudioSource.FadeOutCompleteAction.PAUSE);
        }
    }

    public void OnFinalWarp()
    {
        isFinalWarp = true;
        OnTeleportStarted(false, false);
    }

    private void HandleBlackHoleCollapse()
    {
        OnBlackHoleCollapse(cachedFromSpace, cachedToSpace);
    }

    private void OnBlackHoleCollapse(bool fromSpace, bool toSpace)
    {
        blackHole.singularityController.OnCollapse -= HandleBlackHoleCollapse;

        if (isFinalWarp)
        {
            gameObject.SetActive(false);
            return;
        }

        OnEnterBlackHole?.Invoke(fromSpace, toSpace);

        whiteHole.transform.parent = transform.parent;
        whiteHole.transform.localPosition = transform.localPosition;
        whiteHole.WarpObjectIn(2f);
        whiteHole.singularityController.OnCreation += OnWhiteHoleCreated;
        if (!state.StealthMode)
        {
            loopingAudio.FadeIn(1f);
            musicAudio.FadeIn(1f);
        }
        
        UpdateMuffle(true);
    }

    private IEnumerator SpaceAudioTransition()
    {
        loopingAudio.FadeOut(0.5f);
        musicAudio.FadeOut(0.5f, OWAudioSource.FadeOutCompleteAction.PAUSE);

        yield return new WaitForSeconds(0.5f);

        loopingAudio.SetMaxVolume(1f);
        loopingAudio.spatialBlend = 0f;
        loopingAudio.SetTrack(OWAudioMixer.TrackName.Environment_Unfiltered);
        loopingAudio.FadeIn(state.SpaceTimer, true);

        musicAudio.SetMaxVolume(1f);
        musicAudio.spatialBlend = 0f;
        musicAudio.SetTrack(OWAudioMixer.TrackName.Environment_Unfiltered);
        musicAudio.FadeIn(state.SpaceTimer, true);

        yield return new WaitForSeconds(state.SpaceTimer > 12f 
            ? state.SpaceTimer - 7f : state.SpaceTimer * 0.8f);

        loopingAudio.PlayOneShot(AudioType.DBAnglerfishDetectTarget, 1f);
        audioTransition = null;
    }

    private IEnumerator AtmosphereAudioTransition()
    {
        loopingAudio.FadeOut(0.5f);
        musicAudio.FadeOut(0.5f, OWAudioSource.FadeOutCompleteAction.PAUSE);

        yield return new WaitForSeconds(0.5f);

        loopingAudio.SetMaxVolume(baseLoopingAudioVolume);
        loopingAudio.spatialBlend = 1f;
        loopingAudio.SetTrack(OWAudioMixer.TrackName.Environment);
        loopingAudio.FadeIn(1f);

        musicAudio.SetMaxVolume(baseMusicVolume);
        musicAudio.spatialBlend = 1f;
        musicAudio.SetTrack(OWAudioMixer.TrackName.Environment);
        musicAudio.FadeIn(1f);

        audioTransition = null;
    }

    public void OnCaughtPlayer()
    {
        loopingAudio.FadeOut(2f);
        animator.SetTrigger("Stop");
    }
}
