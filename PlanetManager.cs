using System.Collections.Generic;
using UnityEngine;

namespace ErnestoChase;

public class PlanetManager : MonoBehaviour
{
    public delegate void TravelModeEvent(bool isSpace);
    public event TravelModeEvent OnUpdateTravelMode;
    public delegate void TeleportStartedEvent(bool fromSpace, bool toSpace);
    public event TeleportStartedEvent OnTeleportStarted;

    private ErnestoState state;

    private GameObject currentPlanet;
    private ForceVolume currentGravity;

    private List<GameObject> teleportPlanets = new();
    private Queue<Vector3> teleportVelocities = new();

    private Transform staticTransformParent;
    private OWRigidbody rigidbody;

    private bool waitingOnTeleport = false;
    private bool teleportedIntoSpace = false;
    private bool lastPlayerPlanetState = false;
    private bool reachedPlanetEnterPos = true;

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
                reachedPlanetEnterPos = false;
                staticTransformParent.GetComponent<OWRigidbody>().SetVelocity(Vector3.zero);
                rigidbody.SetPosition(transform.position);
                rigidbody.SetVelocity(gameObject.GetAttachedOWRigidbody().GetVelocity());
                transform.parent = rigidbody.transform;
                OnUpdateTravelMode?.Invoke(true);
            }
            else if (onPlanet)
            {
                if (noSpaceTeleportTarget)
                {
                    rigidbody.SetVelocity(Vector3.zero);
                    currentPlanet = GetCurrentPlanetBody().gameObject;
                    transform.parent = currentPlanet.transform;
                }
                OnUpdateTravelMode?.Invoke(false);
            }

            return true;
        }

        return false;
    }

    public void OnTeleportRequired()
    {
        if (teleportPlanets[0] == null)
        {
            teleportedIntoSpace = false;
            OnTeleportStarted?.Invoke(false, true);
        }
    }

    public void OnEnterBlackHole(bool fromSpace, bool toSpace)
    {
        teleportPlanets.RemoveAt(0);
    }

    public bool IsOnPlanet()
    {
        return GetCurrentPlanetBody() != null;
    }

    public bool RecentlyTeleported()
    {
        return waitingOnTeleport;
    }

    public bool HasEnteredPlanet()
    {
        return reachedPlanetEnterPos;
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
        else
        {
            return currentPlanet.transform;
        }
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
            else
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

    private OWRigidbody GetCurrentPlanetBody()
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
