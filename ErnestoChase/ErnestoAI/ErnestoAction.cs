using UnityEditor.PackageManager;
using UnityEngine;

namespace ErnestoChase.ErnestoAI;

public abstract class ErnestoAction
{
	protected ErnestoState _state;
	protected ErnestoController _controller;
	protected ErnestoEffects _effects;
	protected bool _running;
	protected float _enterTime;
	
	public enum Name
	{
		Idle,
		Follow,
		FollowStupid,
		Berserker,
		SpaceLinear,
		SpaceTimed,
		SpaceCumulative,
		Teleport,
		Shortcut,
		None
	}

	public static ErnestoAction CreateAction(Name name)
	{
		ErnestoAction action;
		switch (name)
		{
			case Name.Idle:
				action = new IdleAction();
				break;
			case Name.Follow:
				action = new FollowAction();
				break;
			case Name.FollowStupid:
				action = new ScoutFollowAction();
				break;
			case Name.Berserker:
				action = new BerserkerAction();
				break;
			case Name.SpaceLinear:
				action = new SpaceLinearAction();
				break;
			case Name.SpaceTimed:
				action = new SpaceTimedAction();
				break;
			case Name.SpaceCumulative:
				action = new SpaceCumulativeAction();
				break;
			case Name.Teleport:
				action = new TeleportAction();
				break;
			case Name.Shortcut:
				action = new ShortcutAction();
				break;
			default:
				action = null;
				break;
		}

		if (action == null)
		{
			ErnestoChase.WriteDebugMessage("Failed to create action from name " + name);
			return null;
		}

		if (action.GetName() != name)
		{
			ErnestoChase.WriteErrorMessage($"New action name {action.GetName()} does not match supplied name {name}");
		}

		return action;
	}

	public virtual void Initialize(ErnestoState state, ErnestoController controller, 
		ErnestoEffects effects)
	{
		_state = state;
		_controller = controller;
		_effects = effects;
	}
	
	public void EnterAction()
	{
		_running = true;
		_enterTime = Time.time;
		OnEnterAction();
	}
	
	public void ExitAction()
	{
		_running = false;
		OnExitAction();
	}

	public abstract Name GetName();
	
	public abstract float CalculateUtility();
	
	public abstract bool Update_Action();
	
	public virtual bool IsInterruptible()
	{
		return true;
	}

	public virtual void FixedUpdate_Action() { }

	public virtual void OnArriveAtTarget() { }
	
	protected virtual void OnEnterAction() { }
	
	protected virtual void OnExitAction() { }
}