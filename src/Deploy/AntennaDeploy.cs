namespace KERBALISM
{
  // This class will support two signal system (Kerbalism Signal & CommNet)
  public class AntennaDeploy : DeployBase
  {
    Antenna antenna;
    ModuleDataTransmitter transmitter;

    ModuleAnimationGroup kerbalismAnimation;
    ModuleDeployableAntenna StockAnimation;

    // When is consuming EC
    bool isTransmitting;
    bool isMoving;

    public override void Start()
    {
      thisModule = "AntennaDeploy";
      if (Features.Signal)
      {
        antenna = part.FindModuleImplementing<Antenna>();
        kerbalismAnimation = part.FindModuleImplementing<ModuleAnimationGroup>();
      }
      else
      {
        transmitter = part.FindModuleImplementing<ModuleDataTransmitter>();
        StockAnimation = part.FindModuleImplementing<ModuleDeployableAntenna>();

        // I using the antennaModule & KerbalismDeploy to save distance & extend, this works for 2 purpose. 
        //  First: When someone disable CommNet, Kerbalism antenna will work fine until extend is saved
        //  Second:	CommNet verify if transmitter.canComm == (isdeploy || moduleisActive)
        //    when the transmitter dosn't has deploy(is fixed), the only way to disable the connection is Setting distance to 0 when no EC, forcing CommNet lost connection.
        antenna = part.FindModuleImplementing<Antenna>();
        kerbalismAnimation = part.FindModuleImplementing<ModuleAnimationGroup>();
      }
    }

    public override void FixedUpdate()
    {
      if (FixGame(thisModule)) return;

      hasEC = ResourceCache.Info(part.vessel, "ElectricCharge").amount > double.Epsilon;

      vessel_info vi;

      // get vessel info from the cache to verify if it is transmitting or relaying
      if (Cache.HasVesselInfo(part.vessel, out vi)) isTransmitting = (vi.transmitting.Length > 0 || vi.relaying.Length > 0);

      if (GetIsActive())
      {
        part.ModulesOnUpdate();
        vessel_resources resources = ResourceCache.Get(part.vessel);
        if (!isTransmitting || isMoving) resources.Consume(part.vessel, "ElectricCharge", actualECCost * Kerbalism.elapsed_s);
        // Just show the value on screen for antennaModule
        else actualECCost = antenna.cost;
      }
      else actualECCost = 0;
    }

    public override bool GetIsActive()
    {
      if (Features.Signal)
      {
        // Just to make sure that has the modules target
        if (antenna == null) return false;

        // Fix the modules if the Feature deploy has been disable.
        if (!Features.Deploy)
        {
          if (antenna != null) antenna.isEnabled = true;
          if (transmitter != null) transmitter.isEnabled = true;
          if (kerbalismAnimation != null) kerbalismAnimation.isEnabled = true;
          if (StockAnimation != null) StockAnimation.isEnabled = true;
          return false;
        }

        if (hasEC)
        {
          // Enable module when has ec
          if (kerbalismAnimation != null) kerbalismAnimation.isEnabled = true;
          antenna.isEnabled = true;

          // If kerbalismAnimation == null, antenna is fixed
          if (kerbalismAnimation != null)
          {
            // Add cost to Extending/Retracting
            if (kerbalismAnimation.DeployAnimation.isPlaying)
            {
              actualECCost = ecDeploy;
              isMoving = true;
              return true;
            }
            else if (kerbalismAnimation.isDeployed && !kerbalismAnimation.DeployAnimation.isPlaying)
            {
              actualECCost = ecCost;
              isMoving = false;
              return true;
            }
          }
          else
          {
            // this means that antenna is fixed
            actualECCost = ecCost;
            isMoving = false;
            return true;
          }
          // if hit here, this means that antenna has animation but is not extended.
          return false;
        }
        else
        {
          if (kerbalismAnimation != null) kerbalismAnimation.isEnabled = false;
          antenna.isEnabled = false;
          return false;
        }
      }
      return false;
    }
  }
}
