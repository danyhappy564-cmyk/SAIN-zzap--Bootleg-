using EFT;
using SAIN.Components;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Memory;

public class LocationTracker : BotBase
{
    public Collider BotZoneCollider
    {
        get { return BotZone?.Collider; }
    }

    public AIPlaceInfo BotZone
    {
        get { return BotOwner.AIData.PlaceInfo; }
    }

    public bool IsIndoors { get; private set; }

    /// <summary>
    /// zzap: indoors by either measure - SAIN's own IsIndoors only knows the AI "building" areas (AIData.EnvironmentId), and
    /// Factory has none, so every bot there counted as outdoors (10/2 sim: all 101 failed grenade arcs were tried as
    /// outdoor lobs into the factory ceiling). Also true when the game's own environment says indoor (under a roof).
    /// Used by the zzap features; SAIN's original code keeps IsIndoors.
    /// </summary>
    public bool UnderRoof { get; private set; }

    public LocationTracker(BotComponent sain)
        : base(sain) { }

    public override void ManualUpdate()
    {
        if (_checkIndoorsTime < Time.time)
        {
            _checkIndoorsTime = Time.time + 0.2f;
            IsIndoors = Player.AIData.EnvironmentId != 0;
            UnderRoof = IsIndoors || Player.Environment == EnvironmentType.Indoor;
        }
        base.ManualUpdate();
    }

    private float _checkIndoorsTime;
}
