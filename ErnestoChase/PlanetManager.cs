using System.Collections.Generic;
using UnityEngine;

namespace ErnestoChase;

public class PlanetManager : MonoBehaviour
{
    public delegate void TravelModeEvent(bool isSpace);
    public event TravelModeEvent OnUpdateTravelMode;
    public delegate void TeleportStartedEvent(bool fromSpace, bool toSpace);
    public event TeleportStartedEvent OnTeleportStarted;
    public delegate void PlayerWarpEvent(bool isSpace);
    public event PlayerWarpEvent OnPlayerWarpStarted;
    public event PlayerWarpEvent OnPlayerWarpComplete;

    private ErnestoState state;

    private GameObject currentPlanet;
    private ForceVolume currentGravity;

    private List<GameObject> teleportPlanets = new();
    private Queue<Vector3> teleportVelocities = new();
    private Vector3 startVelocity;

    private Transform staticTransformParent;
    private OWRigidbody rigidbody;

    private bool playerRecentlyWarped = false;
    private bool teleportedIntoSpace = false;
    private bool lastPlayerPlanetState = false;
    private bool teleportedBeforeRelease = false;

    private void Awake()
    {
        state = GetComponent<ErnestoState>();
        var body = ErnestoChase.LoadPrefab("Assets/ErnestoChase/ErnestoBody.prefab");
        rigidbody = Instantiate(body, Vector3.zero, Quaternion.identity).GetComponent<OWRigidbody>();
        GameObject targetsParent = ErnestoChase.LoadPrefab("Assets/ErnestoChase/SpaceTargetsParent.prefab");
        staticTransformParent = Instantiate(targetsParent).transform;
    }

    public void Initialize()
    {
        OWRigidbody body = GetCurrentPlanetBody();
        if (body != null)
        {
            currentPlanet = body.gameObject;
            if (state.AIEnabled)
            {
                transform.parent = body.transform;
            }
        }
        else if (state.AIEnabled)
        {
            transform.parent = rigidbody.transform;
        }

        lastPlayerPlanetState = body != null;
    }

    public bool UpdatePlayerPlanetState(bool forceUpdate = false)
    {
        bool onPlanet = IsOnPlanet();
        bool lastState = lastPlayerPlanetState;
        if (lastPlayerPlanetState != onPlanet || forceUpdate)
        {
            if (!playerRecentlyWarped)
            {
                lastPlayerPlanetState = onPlanet;
            }

            if (!onPlanet && !playerRecentlyWarped && !teleportedIntoSpace)
            {
                ErnestoChase.WriteDebugMessage("clear teleport planets");
                teleportPlanets.Clear();
                state.FollowedPlayerToPlanet = false;

                if (state.ErnestoReleased)
                {
                    staticTransformParent.GetComponent<OWRigidbody>().SetVelocity(Vector3.zero);
                    rigidbody.SetPosition(transform.position);
                    rigidbody.SetVelocity(gameObject.GetAttachedOWRigidbody().GetVelocity());
                    transform.parent = rigidbody.transform;
                }
                
                OnUpdateTravelMode?.Invoke(true);
            }
            else if (onPlanet)
            {
                if (teleportedBeforeRelease && (teleportPlanets.Count == 0 || 
                    teleportPlanets[0] != this.GetAttachedOWRigidbody().gameObject))
                {
                    ErnestoChase.WriteDebugMessage("burn teleport trigger");
                    state.FollowedPlayerToPlanet = false;
                }
                
                teleportedBeforeRelease = false;
                
                currentPlanet = GetCurrentPlanetBody().gameObject;

                if (state.ErnestoReleased && teleportPlanets.Count == 0)
                {
                    ErnestoChase.WriteDebugMessage("enter atmo");
                    rigidbody.SetVelocity(Vector3.zero);
                    transform.parent = currentPlanet.transform;
                    ErnestoChase.WriteDebugMessage("parented to " + transform.parent.name);
                }
                else if (!lastState && !playerRecentlyWarped && teleportPlanets.Count > 0 &&
                    teleportPlanets[teleportPlanets.Count - 1] == null)
                {
                    ErnestoChase.WriteDebugMessage("Set space planet to current planet");
                    teleportPlanets[teleportPlanets.Count - 1] = currentPlanet;
                }
                
                OnUpdateTravelMode?.Invoke(false);
            }

            return true;
        }

        return false;
    }

    public void OnPlayerWarped()
    {
        ErnestoChase.WriteDebugMessage("\nReceive warp event");
        playerRecentlyWarped = true;
        OnPlayerWarpStarted?.Invoke(!lastPlayerPlanetState);

        if (!state.ErnestoReleased && GetCurrentPlanetBody() == gameObject.GetAttachedOWRigidbody())
        {
            teleportedBeforeRelease = true;
        }

        ErnestoChase.Instance.ModHelper.Events.Unity.FireInNUpdates(() =>
        {
            playerRecentlyWarped = false;
            OWRigidbody planet = GetCurrentPlanetBody();
            if (planet == null)
            {
                ErnestoChase.WriteDebugMessage($"- Teleported to space ({teleportPlanets.Count})");
                teleportedIntoSpace = true;
                teleportPlanets.Add(null);
                teleportVelocities.Enqueue(Locator.GetPlayerBody().GetVelocity());
            }
            else
            {
                if (teleportPlanets.Count > 0 && teleportPlanets[teleportPlanets.Count - 1] == null)
                {
                    ErnestoChase.WriteDebugMessage("Set space to planet");
                    teleportPlanets[teleportPlanets.Count - 1] = planet.gameObject;
                }
                else
                {
                    teleportPlanets.Add(planet.gameObject);
                }
                ErnestoChase.WriteDebugMessage($"- Teleported to planet " + planet.gameObject + $" ({teleportPlanets.Count})");
            }

            OnPlayerWarpComplete?.Invoke(planet == null);
        }, 10);
    }

    public void OnTeleportRequired(bool fromSpace)
    {
        ErnestoChase.WriteDebugMessage("Teleport planets length: " + teleportPlanets.Count);
        ErnestoChase.WriteDebugMessage("\nNext teleport target " + teleportPlanets[0] + "\n");
        if (!fromSpace && teleportPlanets[0] == null)
        {
            teleportedIntoSpace = false;
        }

        OnTeleportStarted?.Invoke(fromSpace, teleportPlanets[0] == null);
    }

    public void OnEnterBlackHole(bool fromSpace, bool toSpace)
    {
        ErnestoChase.WriteDebugMessage("Teleport logic");

        if (!fromSpace && !toSpace)
        {
            ErnestoChase.WriteDebugMessage("ground to ground");
            transform.parent = teleportPlanets[0].transform;
            currentPlanet = teleportPlanets[0];
            teleportPlanets.RemoveAt(0);
        }
        else if (fromSpace && !toSpace)
        {
            ErnestoChase.WriteDebugMessage("space to ground");
            rigidbody.SetVelocity(Vector3.zero);
            transform.parent = teleportPlanets[0].transform;
            currentPlanet = teleportPlanets[0];
            teleportPlanets.RemoveAt(0);
            state.FollowedPlayerToPlanet = true;
        }
        else if (!fromSpace && toSpace)
        {
            ErnestoChase.WriteDebugMessage("ground to space");
            teleportPlanets.RemoveAt(0);
            state.FollowedPlayerToPlanet = false;
            staticTransformParent.GetComponent<OWRigidbody>().SetVelocity(Vector3.zero);
            OWRigidbody planet = GetCurrentPlanetBody();
            if (planet != null)
            {
                currentPlanet = planet.gameObject;
                rigidbody.SetVelocity(Vector3.zero);
                transform.parent = currentPlanet.transform;
            }
            else
            {
                currentPlanet = null;
                rigidbody.SetPosition(transform.position);
                rigidbody.SetVelocity(teleportVelocities.Peek());
                teleportVelocities.Dequeue();
                startVelocity = rigidbody.GetVelocity();
                transform.parent = rigidbody.transform;
                ErnestoChase.Instance.ModHelper.Events.Unity.FireOnNextUpdate(() =>
                {
                    OnUpdateTravelMode?.Invoke(true);
                });
            }
        }
        else if (fromSpace && toSpace)
        {
            ErnestoChase.WriteDebugMessage("space to space");
            teleportPlanets.RemoveAt(0);
            OWRigidbody planet = GetCurrentPlanetBody();
            if (planet != null && teleportPlanets.Count == 0)
            {
                currentPlanet = planet.gameObject;
            }
        }
    }

    public bool IsOnPlanet()
    {
        return GetCurrentPlanetBody() != null;
    }

    public bool HasRecentlyTeleported()
    {
        return playerRecentlyWarped;
    }

    public GameObject GetCurrentPlanet()
    {
        return currentPlanet;
    }

    public ForceVolume GetCurrentGravity()
    {
        return currentGravity;
    }

    public Transform GetTargetParent()
    {
        if (teleportPlanets.Count > 0 && teleportPlanets[teleportPlanets.Count - 1] != null)
        {
            return teleportPlanets[teleportPlanets.Count - 1].transform;
        }
        else if (currentPlanet != null)
        {
            return currentPlanet.transform;
        }

        return null;
    }

    public Transform GetPlayerParent()
    {
        if (IsOnPlanet())
        {
            if (teleportPlanets.Count > 0)
            {
                GameObject lastPlanet = teleportPlanets[teleportPlanets.Count - 1];
                if (lastPlanet != null)
                {
                    return lastPlanet.transform;
                }
            }
            else if (currentPlanet != null)
            {
                return currentPlanet.transform;
            }
        }

        return staticTransformParent;
    }

    public Transform GetStaticParent()
    {
        return staticTransformParent;
    }

    public OWRigidbody GetErnestoBody()
    {
        return rigidbody;
    }

    public OWRigidbody GetCurrentPlanetBody()
    {
        OWRigidbody body = null;
        ForceVolume gravVol = null;
        ForceVolume zeroGVol = null;

        AlignmentForceDetector detector = Locator.GetPlayerForceDetector();
        if (detector._trackedLayers.Count > 0)
        {
            foreach (int num in detector._trackedLayers.Keys)
            {
                if (detector._trackedLayers[num].volumes.Count == 0) continue;

                foreach (PriorityVolume priorityVolume in detector._trackedLayers[num].volumes)
                {
                    if (priorityVolume is ForceVolume volume && volume.GetAttachedOWRigidbody().IsKinematic())
                    {
                        if (gravVol == null && volume.GetAffectsAlignment(Locator.GetPlayerBody()))
                        {
                            gravVol = volume;
                        }
                        else if (zeroGVol == null && volume is ZeroGVolume)
                        {
                            zeroGVol = volume;
                        }
                    }
                }

                ForceVolume theVolume = gravVol ?? zeroGVol;
                if (theVolume != null)
                {
                    var parentBody = theVolume.GetAttachedOWRigidbody();
                    if (parentBody.IsKinematic())
                    {
                        body = parentBody;
                        currentGravity = theVolume;
                    }
                }
            }
        }

        if (transform.parent != rigidbody.transform)
        {
            rigidbody.transform.position = transform.position;
        }

        if (PlayerState.InBrambleDimension())
        {
            return staticTransformParent.GetComponent<OWRigidbody>();
        }
        
        return body;
    }
}
