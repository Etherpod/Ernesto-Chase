using UnityEngine;

namespace ErnestoChase.Spectating;

public class PlayerSectorTracker : MonoBehaviour
{
	private SectorDetector _sectorDetector;
	private bool _syncSectorsNextFrame;

	private void Awake()
	{
		_sectorDetector = this.GetRequiredComponent<SectorDetector>();
		_sectorDetector.OnEnterSector += UpdateSectors;
		_sectorDetector.OnExitSector += UpdateSectors;
	}

	private void Update()
	{
		if (_syncSectorsNextFrame)
		{
			foreach (var id in ErnestoChase.Players)
			{
				QSBCompat.SendPlayerSectors(id, _sectorDetector);
			}

			_syncSectorsNextFrame = false;
			enabled = false;
		}
	}

	private void UpdateSectors(Sector sector)
	{
		if (!ErnestoChase.InMultiplayer || _syncSectorsNextFrame) return;

		_syncSectorsNextFrame = true;
		enabled = true;
	}

	private void OnDestroy()
	{
		_sectorDetector.OnEnterSector -= UpdateSectors;
		_sectorDetector.OnExitSector -= UpdateSectors;
	}
}