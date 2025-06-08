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

    private bool waitingOnTeleport = false;
    private bool teleportedIntoSpace = false;
    private bool lastPlayerPlanetState = false;

    private void Awake()
    {
        state = GetComponent<ErnestoState>();
        rigidbody = ErnestoChase.Instance.ernestoBody;
        GameObject targetsParent = ErnestoChase.LoadPrefab("Assets/ErnestoChase/SpaceTargetsParent.prefab");
        staticTransformParent = Instantiate(targetsParent).transform;
    }

    public void Initialize()
    {
        OWRigidbody body = GetCurrentPlanetBody();
        if (body != null)
        {
            currentPlanet = body.gameObject;
            transform.parent = body.transform;
        }
        else
        {
            transform.parent = rigidbody.transform;
        }

        lastPlayerPlanetState = body != null;
    }

    public bool UpdatePlayerPlanetState(bool noSpaceTeleportTarget)
    {
        bool onPlanet = IsOnPlanet();
        if (lastPlayerPlanetState != onPlanet)
        {
            if (!waitingOnTeleport)
            {
                lastPlayerPlanetState = onPlanet;
            }

            if (!onPlanet && !waitingOnTeleport && !teleportedIntoSpace)
            {
                teleportPlanets.Clear();
                state.FollowedPlayerToPlanet = false;
                staticTransformParent.GetComponent<OWRigidbody>().SetVelocity(Vector3.zero);
                rigidbody.SetPosition(transform.position);
                rigidbody.SetVelocity(gameObject.GetAttachedOWRigidbody().GetVelocity());
                transform.parent = rigidbody.transform;
                OnUpdateTravelMode?.Invoke(true);
            }
            else if (onPlanet)
            {
                currentPlanet = GetCurrentPlanetBody().gameObject;
                if (noSpaceTeleportTarget)
                {
                    rigidbody.SetVelocity(Vector3.zero);
                    transform.parent = currentPlanet.transform;
                }
                OnUpdateTravelMode?.Invoke(false);
            }

            return true;
        }

        return false;
    }

    public void OnPlayerWarped()
    {
        ErnestoChase.WriteDebugMessage("Receive warp event");
        waitingOnTeleport = true;
        OnPlayerWarpStarted.Invoke(lastPlayerPlanetState);

        ErnestoChase.Instance.ModHelper.Events.Unity.FireInNUpdates(() =>
        {
            waitingOnTeleport = false;
            OWRigidbody planet = GetCurrentPlanetBody();
            if (planet == null)
            {
                teleportedIntoSpace = true;
                teleportPlanets.Add(null);
                teleportVelocities.Enqueue(Locator.GetPlayerBody().GetVelocity());
            }
            else
            {
                ErnestoChase.WriteDebugMessage("Add " + planet.gameObject);
                teleportPlanets.Add(planet.gameObject);
            }

            OnPlayerWarpComplete.Invoke(planet == null);
        }, 10);
    }

    public void OnTeleportRequired(bool fromSpace)
    {
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
            transform.parent = teleportPlanets[0].transform;
            currentPlanet = teleportPlanets[0];
            teleportPlanets.RemoveAt(0);
        }
        else if (fromSpace && !toSpace)
        {
            rigidbody.SetVelocity(Vector3.zero);
            transform.parent = teleportPlanets[0].transform;
            currentPlanet = teleportPlanets[0];
            teleportPlanets.RemoveAt(0);
            state.FollowedPlayerToPlanet = true;
        }
        else if (!fromSpace && toSpace)
        {
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
        return waitingOnTeleport;
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

        if (PlayerState.InBrambleDimension())
        {
            return staticTransformParent.GetComponent<OWRigidbody>();
        }

        AlignmentForceDetector detector = Locator.GetPlayerForceDetector();
        if (detector._trackedLayers.Count > 0)
        {
            foreach (int num in detector._trackedLayers.Keys)
            {
                if (detector._trackedLayers[num].volumes.Count == 0) continue;

                foreach (PriorityVolume priorityVolume in detector._trackedLayers[num].volumes)
                {
                    ForceVolume volume = priorityVolume is ForceVolume forceVolume ? forceVolume : null;
                    if (volume && (volume.GetAffectsAlignment(Locator.GetPlayerBody()) || volume is ZeroGVolume))
                    {
                        OWRigidbody[] parentBodies = volume.GetComponentsInParent<OWRigidbody>();
                        foreach (OWRigidbody parentBody in parentBodies)
                        {
                            if (parentBody.IsKinematic())
                            {
                                body = parentBody;
                                currentGravity = volume;
                                break;
                            }
                        }
                    }
                }
            }
        }

        return body;
    }

    public void OnCaughtPlayer()
    {
        Destroy(staticTransformParent.gameObject);
    }
}
