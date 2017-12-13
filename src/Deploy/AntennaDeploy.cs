namespace KERBALISM
{
  // This class will support two signal system (Kerbalism Signal & CommNet)
  public class AntennaDeploy : DeployBase
  {
    Antenna antenna;
    ModuleAnimationGroup customAnim;

    ModuleDataTransmitter transmitter;
    ModuleDeployableAntenna stockAnim;

    // When is consuming EC
    bool isTransmitting;
    bool isMoving;

    public override void OnStart(StartState state)
    {
      if (state == StartState.Editor && state == StartState.None && state == StartState.PreLaunch) return;
      thisModule = "AntennaDeploy";
      // Kerbalism modules
      antenna = part.FindModuleImplementing<Antenna>();
      customAnim = part.FindModuleImplementing<ModuleAnimationGroup>();
      // KSP modules
      transmitter = part.FindModuleImplementing<ModuleDataTransmitter>();
      stockAnim = part.FindModuleImplementing<ModuleDeployableAntenna>();

      // I'm using the antennaModule to save distance & extend, this works for 2 purpose. 
      //  First: When someone disable CommNet, Kerbalism antenna will work fine until extend is saved
      //  Second:	CommNet verify if transmitter.canComm == (isdeploy || moduleisActive)
      //    when the transmitter dosn't has deploy(is fixed), the only way to disable the connection is Setting distance to 0 when no EC, forcing CommNet lost connection.
      if (transmitter != null) transmitter.isEnabled = !Features.Signal;
      if (stockAnim != null) stockAnim.isEnabled = !Features.Signal;

      if (antenna != null) antenna.isEnabled = Features.Signal;
      if (customAnim != null)
      {
        antenna.extended = customAnim.isDeployed;
        customAnim.isEnabled = Features.Signal;
      }
      else
      {
        antenna.extended = true;
      }
    }

    public override void FixedUpdate()
    {
      if (Features.Deploy)
      {
        if (Features.Signal)
        {
          // get vessel info from the cache to verify if it is transmitting or relaying
          vessel_info vi = Cache.VesselInfo(part.vessel);
          if (Cache.HasVesselInfo(part.vessel, out vi)) isTransmitting = (vi.transmitting.Length > 0 || vi.relaying.Length > 0);
        }
        else
        {
          isTransmitting = false;
        }
        if (IsActive)
        {
          part.ModulesOnUpdate();
          vessel_resources resources = ResourceCache.Get(part.vessel);
          if (!isTransmitting || isMoving) resources.Consume(part.vessel, "ElectricCharge", actualECCost * Kerbalism.elapsed_s);
          else
          {
            // Just show the value on screen for module, but not consume
            actualECCost = antenna.cost;
          }
        }
        else actualECCost = 0;
      }
    }

    public override bool IsActive
    {
      get
      {
        if (Features.Signal)
        {
          // Just to make sure that has the modules target
          if (antenna == null) return false;

          if (hasEC)
          {
            if (customAnim != null)
            {
              // Enable module when has ec
              customAnim.isEnabled = true;
              antenna.extended = customAnim.isDeployed;

              // Update values of stock Modules
              stockAnim.deployState = (customAnim.isDeployed ? ModuleDeployablePart.DeployState.EXTENDED : ModuleDeployablePart.DeployState.RETRACTED);

              // Add cost to Extending/Retracting
              if (customAnim.DeployAnimation.isPlaying)
              {
                actualECCost = ecDeploy;
                isMoving = true;
                return true;
              }
              else if (customAnim.isDeployed)
              {
                actualECCost = ecCost;
                isMoving = false;
                return true;
              }
              else
              {
                // if hit here, this means that antenna has animation but is not extended nor playing.
                return false;
              }
            }
            else
            {
              // this means that antenna is fixed
              // Make antenna not valid to AntennaInfo
              antenna.extended = true;
              actualECCost = ecCost;
              isMoving = false;
              return true;
            }
          }
          else
          {
            // Make antenna not valid to AntennaInfo
            antenna.extended = false;

            if (customAnim != null)
            {
              customAnim.isEnabled = false;
              // Update values of stock Modules
              stockAnim.deployState = (customAnim.isDeployed ? ModuleDeployablePart.DeployState.EXTENDED : ModuleDeployablePart.DeployState.RETRACTED);
            }
            return false;
          }
        }
        else
        {
          if (antenna == null && transmitter == null) return false;

          antenna.dist = (antenna.dist != transmitter.antennaPower && transmitter.antennaPower > 0 ? transmitter.antennaPower : antenna.dist);

          if (hasEC)
          {
            transmitter.antennaPower = antenna.dist;

            if (stockAnim != null)
            {
              stockAnim.isEnabled = true;
              // Update values of Kerbalism Modules
              customAnim.isDeployed = stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED;
              antenna.extended = stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED;

              // Add cost to Extending/Retracting
              if (stockAnim.deployState == ModuleDeployablePart.DeployState.RETRACTING || stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDING)
              {
                actualECCost = ecDeploy;
                isMoving = true;
                return true;
              }
              else if (stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED)
              {
                actualECCost = ecCost;
                isMoving = false;

                // Save the antenna status for Enable\Disable Signal support.
                antenna.extended = true;
                if (customAnim != null) customAnim.isDeployed = true;
                return true;
              }
              else
              {
                // if hit here, this means that antenna has animation but is not extended nor playing.
                return false;
              }
            }
            // If deployableAntenna == null, Transmitter is fixed
            else
            {
              actualECCost = ecCost;
              isMoving = false;
              // I don't need update value when don't have animation, Kerbalism assume antenna.extended=true
              return true;
            }
          }
          else
          {
            if (stockAnim != null)
            {
              stockAnim.isEnabled = false;

              // Update values of Kerbalism Modules
              customAnim.isDeployed = stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED;
              antenna.extended = stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED;
            }
            // Change the range to 0, causing CommNet to lose the signal
            transmitter.antennaPower = 0;
            return false;
          }
        }
      }
    }
  }
}
