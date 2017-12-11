namespace KERBALISM
{
  // This class will support two signal system (Kerbalism Signal & CommNet)
  public class AntennaDeploy : DeployBase
  {
    Antenna antenna;
    ModuleDataTransmitter transmitter;

    ModuleAnimationGroup kerbalismAnimation;
    ModuleDeployableAntenna stockAnimation;

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
        stockAnimation = part.FindModuleImplementing<ModuleDeployableAntenna>();

        // Future use:
        // I'm using the antennaModule to save distance & extend, this works for 2 purpose. 
        //  First: When someone disable CommNet, Kerbalism antenna will work fine until extend is saved
        //  Second:	CommNet verify if transmitter.canComm == (isdeploy || moduleisActive)
        //    when the transmitter dosn't has deploy(is fixed), the only way to disable the connection is Setting distance to 0 when no EC, forcing CommNet lost connection.
        antenna = part.FindModuleImplementing<Antenna>();
        kerbalismAnimation = part.FindModuleImplementing<ModuleAnimationGroup>();
        if (antenna != null) antenna.isEnabled = false;
        if (kerbalismAnimation != null) kerbalismAnimation.isEnabled = false;
      }
    }

    public override void FixedUpdate()
    {
      // get vessel info from the cache to verify if it is transmitting or relaying
      vessel_info vi = Cache.VesselInfo(part.vessel);
      if(Cache.HasVesselInfo(part.vessel, out vi)) isTransmitting = (vi.transmitting.Length > 0 || vi.relaying.Length > 0);

      if (IsActive)
      {
        part.ModulesOnUpdate();
        vessel_resources resources = ResourceCache.Get(part.vessel);
        if (!isTransmitting || isMoving) resources.Consume(part.vessel, "ElectricCharge", actualECCost * Kerbalism.elapsed_s);
        // Just show the value on screen for antennaModule
        else
        {
          actualECCost = antenna.cost;
        }
      }
      else actualECCost = 0;
    }

    public override bool IsActive
    {
      get
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
            // if hit here, this means that antenna has animation but is not extended nor playing.
            return false;
          }
          else
          {
            if (kerbalismAnimation != null) kerbalismAnimation.isEnabled = false;
            antenna.isEnabled = false;
            return false;
          }
        }
        else
        {
          // Fix the modules if the Feature deploy has been disable.
          if (!Features.Deploy)
          {
            if (transmitter != null) transmitter.isEnabled = true;
            if (stockAnimation != null) stockAnimation.isEnabled = true;

            if (antenna != null) antenna.isEnabled = false;
            if (kerbalismAnimation != null) kerbalismAnimation.isEnabled = false;

            return false;
          }

          if (antenna == null && transmitter == null) return false;

          antenna.dist = (antenna.dist != transmitter.antennaPower && transmitter.antennaPower > 0 ? transmitter.antennaPower : antenna.dist);

          if (hasEC)
          {
            transmitter.antennaPower = antenna.dist;

            // If deployableAntenna == null, Transmitter is fixed
            if (stockAnimation != null)
            {
              stockAnimation.isEnabled = true;

              // Add cost to Extending/Retracting
              if (stockAnimation.deployState == ModuleDeployablePart.DeployState.RETRACTING || stockAnimation.deployState == ModuleDeployablePart.DeployState.EXTENDING)
              {
                actualECCost = ecDeploy;
                isMoving = true;
                return true;
              }
              else if (stockAnimation.deployState == ModuleDeployablePart.DeployState.EXTENDED)
              {
                actualECCost = ecCost;
                isMoving = false;

                // Save the antenna status for Enable\Disable Signal support.
                antenna.extended = true;
                if (kerbalismAnimation != null) kerbalismAnimation.isDeployed = true;
                return true;
              }
            }
            else
            {
              actualECCost = ecCost;
              isMoving = false;
              // Save the antenna status for Enable\Disable Signal support.
              antenna.extended = true;
              if (kerbalismAnimation != null) kerbalismAnimation.isDeployed = true;
              return true;
            }
            // if hit here, this means that antenna has animation but is not extended nor playing.
            return false;
          }
          else
          {
            if (stockAnimation != null)
            {
              stockAnimation.isEnabled = false;

              // Save the antenna status for Enable\Disable Signal support.
              if (kerbalismAnimation != null) kerbalismAnimation.isDeployed = stockAnimation.deployState == ModuleDeployablePart.DeployState.EXTENDED;
              antenna.extended = stockAnimation.deployState == ModuleDeployablePart.DeployState.EXTENDED;
            }
            else
            {
              // Save the antenna status for Enable\Disable Signal support.
              if (kerbalismAnimation != null) kerbalismAnimation.isDeployed = true;
              antenna.extended = true;
            }
            transmitter.antennaPower = 0;
            return false;
          }
        }
      }
    }
  }
}
