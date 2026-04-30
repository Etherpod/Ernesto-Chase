using System.Collections.Generic;
using UnityEngine;

namespace ErnestoChase.Spectating;

public class PlayerSectorTrackerRemote : MonoBehaviour
{
	private SectorDetector _sectorDetector;
	private List<Sector> _cachedSectors = [];

	private void Awake()
	{
		_sectorDetector = GetComponent<SectorDetector>();
	}

	public void SetSectorDetector(SectorDetector detector)
	{
		if (_sectorDetector)
		{
			ClearSectors();
		}
		
		_sectorDetector = detector;
		UpdateSectors();
	}

	public void SaveSectors(List<Sector> sectors)
	{
		_cachedSectors.Clear();
		_cachedSectors.AddRange(sectors);
		UpdateSectors();
	}

	public void UpdateSectors()
	{
		bool disableRingWorld = false;
		bool disableDreamWorld = false;

		for (int i = _sectorDetector._sectorList.Count - 1; i >= 0; i--)
		{
			if (i > _sectorDetector._sectorList.Count - 1) continue;
			
			if (!_cachedSectors.Contains(_sectorDetector._sectorList[i]))
			{
				if (enabled)
				{
					if (!disableRingWorld && 
						_sectorDetector._sectorList[i].GetComponentInParent<RingWorldController>())
					{
						disableRingWorld = true;
					}
					if (!disableDreamWorld && 
						_sectorDetector._sectorList[i].GetComponentInParent<DreamWorldController>())
					{
						disableDreamWorld = true;
					}
				}
                
				RemoveSector(_sectorDetector._sectorList[i]);
			}
		}

		bool loadRingWorld = false;
		bool loadDreamWorld = false;
        
		foreach (var sector in _cachedSectors)
		{
			if (enabled)
			{
				if (!loadRingWorld && sector.GetComponentInParent<RingWorldController>())
				{
					loadRingWorld = true;
				}
				if (!loadDreamWorld && sector.GetComponentInParent<DreamWorldController>())
				{
					loadDreamWorld = true;
				}
			}
            
			if (!_sectorDetector._sectorList.Contains(sector))
			{
				AddSector(sector);
			}
		}

		if (loadRingWorld)
		{
			ErnestoChase.SpectateManager.LoadRingWorld();
		}
		else if (disableRingWorld)
		{
			ErnestoChase.SpectateManager.UnloadRingWorld();
		}
        
		if (loadDreamWorld)
		{
			ErnestoChase.SpectateManager.LoadDreamWorld();
		}
		else if (disableDreamWorld)
		{
			ErnestoChase.SpectateManager.UnloadDreamWorld();
		}
	}

	public void ClearSectors()
	{
		bool disableRingWorld = false;
		bool disableDreamWorld = false;
        
		for (int i = _sectorDetector._sectorList.Count - 1; i >= 0; i--)
		{
			if (i > _sectorDetector._sectorList.Count - 1) continue;
			
			if (enabled)
			{
				if (!disableRingWorld && 
					_sectorDetector._sectorList[i].GetComponentInParent<RingWorldController>())
				{
					disableRingWorld = true;
				}
				else if (!disableDreamWorld && 
					_sectorDetector._sectorList[i].GetComponentInParent<DreamWorldController>())
				{
					disableDreamWorld = true;
				}
			}
            
			RemoveSector(_sectorDetector._sectorList[i]);
		}

		if (disableRingWorld)
		{
			ErnestoChase.SpectateManager.UnloadRingWorld();
		}
		else if (disableDreamWorld)
		{
			ErnestoChase.SpectateManager.UnloadDreamWorld();
		}
	}

	private void AddSector(Sector sector)
	{
		sector._playerInTriggerVolume = true;
		if (sector._overriddenByQuantumMoon && Locator.GetQuantumMoon() != null && Locator.GetQuantumMoon().IsPlayerInside())
		{
			MonoBehaviour.print("Prevent player from entering " + base.gameObject.name + " while inside Quantum Moon");
			return;
		}
		
		if (sector._excludedOccupants.Contains(_sectorDetector) && !sector._dynamicOccupants.Contains(_sectorDetector))
		{
			Debug.LogError("ERROR: " + _sectorDetector.gameObject.name + " was excluded despite not being a dynamic occupant.");
		}
		if (sector._occupantsTracked.Contains(_sectorDetector))
		{
			sector._excludedOccupants.Add(_sectorDetector);
			return;
		}
		sector.AddOccupant(_sectorDetector);

		if (!sector._owTriggerVolume.IsTrackingObject(_sectorDetector.gameObject))
		{
			sector._owTriggerVolume.AddObjectToVolume(_sectorDetector.gameObject);
		}
	}

	private void RemoveSector(Sector sector)
	{
		sector._playerInTriggerVolume = false;
		sector.RemoveOccupant(_sectorDetector);
		if (sector._excludedOccupants.Contains(_sectorDetector))
		{
			sector._excludedOccupants.Remove(_sectorDetector);
		}
		
		if (sector._owTriggerVolume.IsTrackingObject(_sectorDetector.gameObject))
		{
			sector._owTriggerVolume.RemoveObjectFromVolume(_sectorDetector.gameObject);
		}
	}
}