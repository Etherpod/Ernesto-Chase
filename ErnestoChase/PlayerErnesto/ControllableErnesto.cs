using UnityEngine;

namespace ErnestoChase.PlayerErnesto;

public class ControllableErnesto : MonoBehaviour
{
    [SerializeField]
    private Transform scaleRoot;
    [SerializeField]
    private PlayerAttachPoint attachPoint;
    [SerializeField]
    private OWCamera owCamera;
    [SerializeField]
    private AlignWithWarpTarget alignWithTarget;
    [SerializeField]
    private GameObject lockOnCanvas;

    private OWRigidbody rigidbody;
    private SectorDetector sectorDetector;
    private RulesetDetector rulesetDetector;
    private AlignmentForceDetector forceDetector;

    private bool shrinked = false;
    private bool changingSize = false;
    private float sizeStartTime;
    private float lastSize;
    private readonly float sizeChangeLength = 1.5f;

    private float baseMass;
    private float baseFOV;
    
    private readonly float movementMultiplier = 30f;
    private readonly float rotationMultiplier = 1f;
    private readonly float forceMultiplier = 0.8f;
    private Vector3 localAcceleration = Vector3.zero;
    private Vector3 localAngularAcceleration = Vector3.zero;

    private readonly float warpChargeLength = 2.5f;
    private float warpChargeStartTime;
    private bool isChargingWarp;

    private readonly float warpSpeed = 1000f;
    private readonly float warpCollisionBuffer = 100f;
    private bool warping;

    private void Awake()
    {
        rigidbody = GetComponent<OWRigidbody>();
        sectorDetector = GetComponentInChildren<SectorDetector>();
        rulesetDetector = GetComponentInChildren<RulesetDetector>();
        forceDetector = GetComponentInChildren<AlignmentForceDetector>();
        owCamera.GetComponent<PlanetaryFogImageEffect>().fogShader = Shader.Find("Hidden/PlanetaryFogImageEffect");
        owCamera.GetComponent<FlashbackScreenGrabImageEffect>()._downsampleShader = Shader.Find("Hidden/DownsampleImageEffect");
        owCamera.gameObject.SetActive(true);

        baseMass = rigidbody.GetMass();
        baseFOV = owCamera.fieldOfView;
        forceDetector._fieldMultiplier = forceMultiplier;
        rigidbody.FreezeRotation();
        alignWithTarget.enabled = false;
    }

    private void Update()
    {
        localAcceleration = Vector3.zero;
        localAngularAcceleration = Vector3.zero;

        if (!warping && !changingSize && OWInput.IsNewlyPressed(InputLibrary.matchVelocity) &&
            CanWarpToTarget(Locator.GetReferenceFrame()))
        {
            warpChargeStartTime = Time.time;
            alignWithTarget.enabled = true;
            isChargingWarp = true;
        }

        if (isChargingWarp)
        {
            if (OWInput.IsNewlyReleased(InputLibrary.matchVelocity) ||
                !CanWarpToTarget(Locator.GetReferenceFrame()))
            {
                isChargingWarp = false;
                owCamera.fieldOfView = shrinked ? baseFOV + 10 : baseFOV;
                alignWithTarget.enabled = false;
            }
            
            var lerp = Mathf.InverseLerp(warpChargeStartTime, 
                warpChargeStartTime + warpChargeLength, Time.time);
            var originalFOV = shrinked ? baseFOV + 10 : baseFOV;
            var newFOV = Mathf.Lerp(originalFOV, originalFOV + 20, Mathf.Pow(lerp, 2));
            owCamera.fieldOfView = newFOV;

            if (lerp == 1f)
            {
                isChargingWarp = false;
                warping = true;
            }

            return;
        }
        
        if (warping)
        {
            if (OWInput.IsNewlyPressed(InputLibrary.matchVelocity))
            {
                warping = false;
                owCamera.fieldOfView = shrinked ? baseFOV + 10 : baseFOV;
                alignWithTarget.enabled = false;
                
                rigidbody.SetVelocity(Locator.GetReferenceFrame() != null
                    ? Locator.GetReferenceFrame().GetVelocity()
                    : Vector3.zero);
            }

            return;
        }
        
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
        
        ReadTranslationalInput();
        ReadRotationalInput();
    }

    private void FixedUpdate()
    {
        if (OWTime.IsPaused()) return;

        if (!warping)
        {
            UpdateMovement();

            if (!isChargingWarp)
            {
                UpdateRotation();
                UpdateSize();
            }
        }
        else
        {
            UpdateWarp();
        }
    }

    private void ReadTranslationalInput()
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

        localAcceleration = acceleration.normalized;
    }
    
    private void ReadRotationalInput()
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
        localAngularAcceleration = rotation;
    }

    private void UpdateMovement()
    {
        float multiplier = Mathf.Min(movementMultiplier * (scaleRoot.localScale.x / 2 + 0.5f), rulesetDetector.GetThrustLimit());
        rigidbody.AddLocalAcceleration(localAcceleration * multiplier);
    }

    private void UpdateRotation()
    {
        rigidbody.AddLocalAngularAcceleration(localAngularAcceleration * rotationMultiplier);
    }

    private void UpdateSize()
    {
        if (changingSize)
        {
            float timeLerp = Mathf.InverseLerp(sizeStartTime, sizeStartTime + sizeChangeLength, Time.fixedTime);
            float scale = Mathf.SmoothStep(lastSize, shrinked ? 0.1f : 1f, timeLerp);

            scaleRoot.localScale = Vector3.one * scale;
            owCamera.fieldOfView = Mathf.Lerp(baseFOV + 10f, baseFOV, scale);

            if (timeLerp == 1)
            {
                changingSize = false;
            }
        }
    }

    private void UpdateWarp()
    {
        var rf = Locator.GetReferenceFrame();
        if (!CanWarpToTarget(rf))
        {
            warping = false;
            owCamera.fieldOfView = shrinked ? baseFOV + 10 : baseFOV;
            alignWithTarget.enabled = false;
            rigidbody.SetVelocity(Locator.GetReferenceFrame() != null
                ? Locator.GetReferenceFrame().GetVelocity()
                : Vector3.zero);
            return;
        }

        var toTarget = Locator.GetReferenceFrame().GetPosition() - rigidbody.GetWorldCenterOfMass();
        if (Physics.Raycast(rigidbody.GetWorldCenterOfMass(), toTarget.normalized,
            out var hit, warpCollisionBuffer, OWLayerMask.physicalMask))
        {
            warping = false;
            owCamera.fieldOfView = shrinked ? baseFOV + 10 : baseFOV;
            alignWithTarget.enabled = false;

            if (hit.collider.GetAttachedOWRigidbody() != null)
            {
                rigidbody.SetVelocity(hit.collider.GetAttachedOWRigidbody().GetVelocity());
            }
            else
            {
                rigidbody.SetVelocity(Locator.GetReferenceFrame() != null
                    ? Locator.GetReferenceFrame().GetVelocity()
                    : Vector3.zero);
            }
            return;
        }

        rigidbody.SetVelocity(rf.GetVelocity() + toTarget.normalized * warpSpeed);
    }

    private bool CanWarpToTarget(ReferenceFrame rf)
    {
        if (rf == null || !rf.GetAllowAutopilot()) return false;
        
        var toTarget = rf.GetPosition() - rigidbody.GetWorldCenterOfMass();
        if (toTarget.magnitude < rf.GetAutopilotArrivalDistance())
        {
            return false;
        }

        return true;
    }

    public void SetActive(bool active)
    {
        scaleRoot.gameObject.SetActive(active);
        lockOnCanvas.SetActive(active);
    }

    public void AttachPlayer()
    {
        Locator.GetPlayerCamera().enabled = false;
        owCamera.enabled = true;
        GlobalMessenger<OWCamera>.FireEvent("SwitchActiveCamera", owCamera);

        if (PlayerState.IsWearingSuit())
        {
            Locator.GetPlayerSuit().RemoveSuit(true);
        }
        Locator.GetToolModeSwapper().UnequipTool();
        
        attachPoint.AttachPlayer();
        GlobalMessenger.FireEvent("PlayerRepositioned");

        Locator.GetPlayerBody().GetComponent<PlayerResources>()._invincible = true;
        Locator.GetDeathManager()._invincible = true;
    }

    public void DetachPlayer()
    {
        attachPoint.DetachPlayer();
        
        GlobalMessenger.FireEvent("PlayerRepositioned");
        
        owCamera.enabled = false;
        Locator.GetPlayerCamera().enabled = true;
        GlobalMessenger<OWCamera>.FireEvent("SwitchActiveCamera", Locator.GetPlayerCamera());

        Locator.GetPlayerSuit().SuitUp(instantSuitUp: true);

        Locator.GetPlayerBody().GetComponent<PlayerResources>()._invincible = false;
        Locator.GetDeathManager()._invincible = false;
    }
}
