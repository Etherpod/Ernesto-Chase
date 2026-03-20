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
    }

    private void Update()
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
    }

    private void FixedUpdate()
    {
        if (OWTime.IsPaused()) return;

        UpdateMovement();
        UpdateRotation();
        UpdateSize();
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
        rigidbody.AddLocalAngularAcceleration(rotation * rotationMultiplier);
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

    public void SetActive(bool active)
    {
        scaleRoot.gameObject.SetActive(active);
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

        ErnestoChase.WriteDebugMessage("attach");
        attachPoint.AttachPlayer();

        GlobalMessenger.FireEvent("PlayerRepositioned");

        Locator.GetPlayerBody().GetComponent<PlayerResources>()._invincible = true;
        Locator.GetDeathManager()._invincible = true;
    }

    public void DetachPlayer()
    {
        ErnestoChase.WriteDebugMessage("enabled: " + attachPoint.enabled);
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
