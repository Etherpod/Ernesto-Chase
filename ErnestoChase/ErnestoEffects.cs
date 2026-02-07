using Mono.Cecil.Cil;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OWML.Common;
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
    private float baseLightIntensity;
    private Texture bulbTex;
    private float baseMeshScale;

    private readonly Dictionary<uint, (AudioClip clip, float lastTime)[]> musicFiles = [];
    private Coroutine audioTransition;
    private float baseLoopingAudioVolume;
    private float baseMusicVolume;

    private bool cachedFromSpace;
    private bool cachedToSpace;
    private bool isFrozen;
    
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
        baseLightIntensity = anglerLight.intensity;
        bulbTex = ernestoRenderer.material.GetTexture("_EmissionMap");
        baseLoopingAudioVolume = loopingAudio.GetMaxVolume();
        baseMusicVolume = musicAudio.GetMaxVolume();

        state.OnDataChanged += OnDataChanged;

        AssetBundleUtilities.ReplaceShaders(blackHolePrefab.gameObject);
        AssetBundleUtilities.ReplaceShaders(whiteHolePrefab.gameObject);
        blackHolePrefab._warpedObjectGeometry = ernestoMesh;
        whiteHolePrefab._warpedObjectGeometry = ernestoMesh;
        blackHole = Instantiate(blackHolePrefab);
        whiteHole = Instantiate(whiteHolePrefab);

        ernestoMesh.transform.localScale = Vector3.zero;
        anglerLight.range = baseLightRange * (ernestoMesh.transform.localScale.magnitude / baseMeshScale);
    }

    private void Start()
    {
        if (state.DisableLight)
        {
            anglerLight.intensity = 0f;
            ernestoRenderer.material.SetTexture("_EmissionMap", noBulbTex);
        }
        
        StartCoroutine(InitAudioFiles(state.DataStates.Keys.ToArray()));
    }

    private void OnDataChanged(uint lastData)
    {
        if (state.DisableLight)
        {
            anglerLight.intensity = 0f;
            ernestoRenderer.material.SetTexture("_EmissionMap", noBulbTex);
        }
        else
        {
            anglerLight.intensity = baseLightIntensity;
            ernestoRenderer.material.SetTexture("_EmissionMap", bulbTex);
        }

        if (!state.QuantumMode)
        {
            isFrozen = false;
        }

        if (state.DataStates[lastData].ErnestoMusic)
        {
            for (int i = 0; i < musicFiles[lastData].Length; i++)
            {
                if (musicFiles[lastData][i].clip == musicAudio.clip)
                {
                    musicFiles[lastData][i].lastTime = musicAudio.time;
                    break;
                }
            }
        }
        
        if (state.ErnestoMusic && state.RemoteID == 0)
        {
            SelectRandomMusicClip(state.ActiveStateID);
        }
        
        if (state.ErnestoReleased && !state.StealthMode && !isFrozen)
        {
            loopingAudio.FadeIn(1f);
            musicAudio.FadeIn(0.1f);
        }
        else
        {
            loopingAudio.Stop();
            musicAudio.Pause();
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
            filterLerp = Mathf.MoveTowards(filterLerp, muffle ? 1f : 0f, Time.deltaTime / 4f);
        }
        
        foreach (var filter in lowPassFilters)
        {
            filter.cutoffFrequency = Mathf.Lerp(22000, 3000, Mathf.Lerp(Mathf.Sqrt(filterLerp), 1f, distMult));
        }
    }

    private IEnumerator InitAudioFiles(uint[] ids)
    {
        string filePath = Path.Combine(ErnestoChase.Instance.ModHelper.Manifest.ModFolderPath, "ErnestoMusic");
        string[] fileTypes = [".mp3", ".ogg", ".wav"];
        
        foreach (var id in ids)
        {
            List<string> files = [];
            
            foreach (var type in fileTypes)
            {
                if (id > 0)
                {
                    var foundFiles = Directory.GetFiles(filePath, $"{id}_*{type}", SearchOption.AllDirectories);
                    if (foundFiles.Length > 0)
                    {
                        files.AddRange(foundFiles);
                        continue;
                    }
                }
            
                files.AddRange(Directory.GetFiles(filePath, $"*{type}", SearchOption.AllDirectories));
            }

            musicFiles[id] = new(AudioClip, float)[files.Count];
            
            for (int i = 0; i < files.Count; i++)
            {
                var request = UnityWebRequestMultimedia.GetAudioClip("file:///" + files[i], UnityEngine.AudioType.UNKNOWN);
                yield return request.SendWebRequest();

                if (request.isDone && !request.isNetworkError)
                {
                    musicFiles[id][i] = (DownloadHandlerAudioClip.GetContent(request), 0f);
                }
            }
        }
        
        if (state.ErnestoMusic && state.RemoteID == 0)
        {
            SelectRandomMusicClip(state.ActiveStateID);
        }

        yield return null;
    }

    private void SelectRandomMusicClip(uint stateID)
    {
        var rand = new System.Random();
        int index = rand.Next(0, musicFiles[stateID].Length);
        var file = musicFiles[stateID][index];
        
        musicAudio.clip = file.clip;
        musicAudio.time = file.lastTime;
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
            if (!state.QuantumMode && !state.StealthMode)
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
                audioTransition = StartCoroutine(SpaceAudioTransition());
            }
        }
        else
        {
            if (audioTransition != null)
            {
                StopCoroutine(audioTransition);
            }
            audioTransition = StartCoroutine(AtmosphereAudioTransition());
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
        isFrozen = visible;
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

        // This is run elsewhere for fake warping
        if (!state.UsingStoredTargets)
        {
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
    }

    private IEnumerator SpaceAudioTransition()
    {
        loopingAudio.FadeOut(0.5f);
        musicAudio.FadeOut(0.5f, OWAudioSource.FadeOutCompleteAction.PAUSE);

        yield return new WaitForSeconds(0.5f);

        loopingAudio.SetMaxVolume(1f);
        loopingAudio.spatialBlend = 0f;
        loopingAudio.SetTrack(OWAudioMixer.TrackName.Environment_Unfiltered);

        musicAudio.SetMaxVolume(1f);
        musicAudio.spatialBlend = 0f;
        musicAudio.SetTrack(OWAudioMixer.TrackName.Environment_Unfiltered);

        if (!state.StealthMode && !isFrozen)
        {
            loopingAudio.FadeIn(state.SpaceTimer, true);
            musicAudio.FadeIn(state.SpaceTimer, true);
        }

        yield return new WaitForSeconds(state.SpaceTimer > 12f 
            ? state.SpaceTimer - 7f : state.SpaceTimer * 0.8f);
        
        if (state.StealthMode)
        {
            OnProximityRoar(true);
        }
        else
        {
            loopingAudio.PlayOneShot(AudioType.DBAnglerfishDetectTarget, 1f);
        }
        
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

        musicAudio.SetMaxVolume(baseMusicVolume);
        musicAudio.spatialBlend = 1f;
        musicAudio.SetTrack(OWAudioMixer.TrackName.Environment);

        if (!state.StealthMode && !isFrozen)
        {
            loopingAudio.FadeIn(1f);
            musicAudio.FadeIn(1f);
        }

        audioTransition = null;
    }

    public void OnCaughtPlayer()
    {
        loopingAudio.FadeOut(2f);
    }

    public void OnFakeWarpEntry()
    {
        OnTeleportStarted(false, false);
    }
    
    public void OnFakeWarpExit()
    {
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

    public void SetAudioPitchMultiplier(float mult)
    {
        loopingAudio.pitch = mult;
        oneShotAudio.pitch = mult;
        musicAudio.pitch = mult;
    }

    private void OnDestroy()
    {
        state.OnDataChanged += OnDataChanged;
    }
}
