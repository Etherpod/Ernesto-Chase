using UnityEngine;
using static TargetDataQueue;

namespace ErnestoChase;

public class ControllableErnesto : MonoBehaviour
{
    [SerializeField]
    private PlayerAttachPoint attachPoint;
    [SerializeField]
    private OWCamera owCamera;

    private OWRigidbody rigidbody;
    private SectorDetector sectorDetector;
    private TargetDataQueue storedTargets;
    private int frameDelay;
    private readonly int storedTargetsFrameDelay;

    private readonly float angularDrag = 0.92f;
    private readonly float movementMultiplier = 25f;
    private readonly float rotationMultiplier = 2f;

    private void Awake()
    {
        rigidbody = GetComponent<OWRigidbody>();
        sectorDetector = GetComponentInChildren<SectorDetector>();
        owCamera.GetComponent<PlanetaryFogImageEffect>().fogShader = Shader.Find("Hidden/PlanetaryFogImageEffect");

        rigidbody.GetRigidbody().angularDrag = angularDrag;
    }

    private void Start()
    {
        storedTargets = new();
    }

    private void FixedUpdate()
    {
        if (OWTime.IsPaused()) return;

        UpdateMovement();
        UpdateRotation();

        /*if (frameDelay > 0)
        {
            frameDelay--;
        }
        else
        {
            frameDelay = storedTargetsFrameDelay;
            TargetData data = GenerateTargetData();
            storedTargets.AddTarget(data);
            ErnestoChase.WriteDebugMessage("Host: " + data.time);

            if (ErnestoChase.InMultiplayer)
            {
                foreach (var id in ErnestoChase.Players)
                {
                    QSBCompat.SendTargetData(id, 0, data);
                }
            }
        }*/
    }

    private void UpdateMovement()
    {
        Vector3 acceleration = Vector3.zero;

        if (OWInput.IsPressed(InputLibrary.up))
        {
            acceleration += Vector3.forward;
        }
        if (OWInput.IsPressed(InputLibrary.down))
        {
            acceleration += Vector3.back;
        }
        if (OWInput.IsPressed(InputLibrary.left))
        {
            acceleration += Vector3.left;
        }
        if (OWInput.IsPressed(InputLibrary.right))
        {
            acceleration += Vector3.right;
        }
        if (OWInput.IsPressed(InputLibrary.thrustUp))
        {
            acceleration += Vector3.up;
        }
        if (OWInput.IsPressed(InputLibrary.thrustDown))
        {
            acceleration += Vector3.down;
        }

        rigidbody.AddLocalAcceleration(acceleration * movementMultiplier);
    }

    private void UpdateRotation()
    {
        Vector3 rotation = Vector3.zero;

        if (OWInput.IsPressed(InputLibrary.rollMode))
        {
            rotation.z -= OWInput.GetValue(InputLibrary.yaw);
        }
        else
        {
            rotation.y += OWInput.GetValue(InputLibrary.yaw);
        }

        rotation.x -= OWInput.GetValue(InputLibrary.pitch);

        rigidbody.AddLocalAngularAcceleration(rotation * rotationMultiplier);
    }

    /*private TargetData GenerateTargetData()
    {
        var staticRef = Locator.GetCenterOfTheUniverse().GetStaticReferenceFrame().transform;
        Transform reference;
        if (sectorDetector.GetLastEnteredSector() != null)
        {
            reference = sectorDetector.GetLastEnteredSector().transform;
        }
        else
        {
            reference = staticRef;
        }
        
        var localPos = reference.InverseTransformPoint(transform.position);
        var worldPos = staticRef.InverseTransformDirection(transform.position);
        return new TargetData(reference.name, localPos, worldPos, Time.fixedTime);
    }*/

    public void AttachPlayer()
    {
        Locator.GetPlayerCamera().enabled = false;
        owCamera.enabled = true;
        GlobalMessenger<OWCamera>.FireEvent("SwitchActiveCamera", owCamera);

        if (PlayerState.IsWearingSuit())
        {
            Locator.GetPlayerSuit().RemoveSuit();
        }

        foreach (var renderer in Locator.GetPlayerBody().GetComponentsInChildren<Renderer>())
        {
            renderer.forceRenderingOff = true;
        }

        attachPoint.AttachPlayer();

        GlobalMessenger.FireEvent("PlayerRepositioned");

        Locator.GetPlayerBody().GetComponent<PlayerResources>()._invincible = true;
        Locator.GetDeathManager()._invincible = true;
    }
}
