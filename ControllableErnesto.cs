using UnityEngine;
using static ErnestoChase.TargetDataQueue;

namespace ErnestoChase;

public class ControllableErnesto : MonoBehaviour
{
    [SerializeField]
    private Transform scaleRoot;
    [SerializeField]
    private PlayerAttachPoint attachPoint;
    [SerializeField]
    private OWCamera owCamera;

    private OWRigidbody rigidbody;
    private SectorDetector sectorDetector;
    private RulesetDetector rulesetDetector;
    private AlignmentForceDetector forceDetector;
    private TargetDataQueue storedTargets;
    private int frameDelay;
    private readonly int storedTargetsFrameDelay;

    private bool shrinked = false;
    private bool changingSize = false;
    private float sizeStartTime;
    private float lastSize;
    private readonly float sizeChangeLength = 2f;

    private float baseMass;
    private float baseFOV;

    private float adjustedManualDrag;
    private Vector3 manualAngularVelocity;

    private readonly float angularDrag = 0.95f;
    private readonly float movementMultiplier = 30f;
    private readonly float rotationMultiplier = 2f;
    private readonly float forceMultiplier = 0.8f;

    private void Awake()
    {
        rigidbody = GetComponent<OWRigidbody>();
        sectorDetector = GetComponentInChildren<SectorDetector>();
        rulesetDetector = GetComponentInChildren<RulesetDetector>();
        forceDetector = GetComponentInChildren<AlignmentForceDetector>();
        owCamera.GetComponent<PlanetaryFogImageEffect>().fogShader = Shader.Find("Hidden/PlanetaryFogImageEffect");

        baseMass = rigidbody.GetMass();
        baseFOV = owCamera.fieldOfView;
        forceDetector._fieldMultiplier = forceMultiplier;
        manualAngularVelocity = Vector3.zero;
        rigidbody.FreezeRotation();
    }

    private void Start()
    {
        adjustedManualDrag = Mathf.Pow(angularDrag, OWTime.GetFixedTimestep() / 0.01666666f);
        storedTargets = new();
    }

    private void OnDisable()
    {
        manualAngularVelocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (OWTime.IsPaused()) return;

        UpdateMovement();
        UpdateRotation();
        UpdateSize();

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
        
        float multiplier = Mathf.Min(movementMultiplier * (scaleRoot.localScale.x / 2 + 0.5f), rulesetDetector.GetThrustLimit());
        rigidbody.AddLocalAcceleration(acceleration.normalized * multiplier);
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

        manualAngularVelocity += transform.TransformDirection(rotation * rotationMultiplier * Time.fixedDeltaTime);
        manualAngularVelocity *= adjustedManualDrag;
        Quaternion quaternion = Quaternion.AngleAxis(manualAngularVelocity.magnitude * 180f / 3.1415927f * Time.fixedDeltaTime, manualAngularVelocity.normalized);
        rigidbody.AddRotation(quaternion);
    }

    private void UpdateSize()
    {
        bool downPressed = OWInput.IsNewlyPressed(InputLibrary.toolOptionDown);
        bool upPressed = OWInput.IsNewlyPressed(InputLibrary.toolOptionUp);

        if (!changingSize && (downPressed || upPressed))
        {
            shrinked = downPressed;
            lastSize = scaleRoot.localScale.x;
            sizeStartTime = Time.fixedTime;
            changingSize = true;

            if (ErnestoChase.InMultiplayer)
            {
                foreach (var id in ErnestoChase.Players)
                {
                    QSBCompat.SendErnestoSizeChange(id, 0, shrinked);
                }
            }
        }

        if (changingSize)
        {
            float timeLerp = Mathf.InverseLerp(sizeStartTime, sizeStartTime + sizeChangeLength, Time.fixedTime);
            float scale;

            if (shrinked)
            {
                scale = Mathf.SmoothStep(lastSize, 0.1f, timeLerp);
            }
            else
            {
                scale = Mathf.SmoothStep(lastSize, 1f, timeLerp);
            }

            scaleRoot.localScale = Vector3.one * scale;
            owCamera.fieldOfView = Mathf.Lerp(baseFOV + 10f, baseFOV, scale);

            if (timeLerp == 1)
            {
                changingSize = false;
            }
        }
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

        Locator.GetToolModeSwapper().UnequipTool();

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
