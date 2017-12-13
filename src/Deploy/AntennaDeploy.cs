using System;

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
      // Do nothing on Editor/PreLaunch
      if (state == StartState.Editor && state == StartState.PreLaunch) return;
      if (Lib.IsFlight())
      {
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
    }

    public override void FixedUpdate()
    {
      if (Lib.IsFlight())
      {
        if (Features.Deploy)
        {
          // get vessel info from the cache to verify if it is transmitting or relaying
          vessel_info vi = Cache.VesselInfo(part.vessel);
          if (Cache.HasVesselInfo(part.vessel, out vi)) isTransmitting = (vi.transmitting.Length > 0 || vi.relaying.Length > 0);

          if (isActive)
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
    }

    public override bool IsDoingAction
    {
      get
      {
        if (Features.Signal)
        {
          // Just to make sure that has the modules target
          if (antenna == null) return false;

          if (customAnim != null)
          {
            // Update values of stock Modules
            // This way you are able to Disable\Enable SignalSystem without break the game.
            stockAnim.deployState = (customAnim.isDeployed ? ModuleDeployablePart.DeployState.EXTENDED : ModuleDeployablePart.DeployState.RETRACTED);
          }

          if (hasEC)
          {
            if (customAnim != null)
            {
              // Allow extending/retracting when has ec
              customAnim.Events["DeployModule"].active = true;
              customAnim.Events["RetractModule"].active = true;

              antenna.extended = customAnim.isDeployed;

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
                // no ecCost
                return false;
              }
            }
            else
            {
              // this means that antenna is fixed
              // Make antenna valid to AntennaInfo
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
              // Don't allow extending/retracting when has no ec
              customAnim.Events["DeployModule"].active = false;
              customAnim.Events["RetractModule"].active = false;
            }
            return false;
          }
        }
        else
        {
          if (antenna == null && transmitter == null) return false;

          // Save antennaPower in Antenna Module
          antenna.dist = (antenna.dist != transmitter.antennaPower && transmitter.antennaPower > 0 ? transmitter.antennaPower : antenna.dist);

          if (stockAnim != null)
          {
            // Update values of Kerbalism Modules
            // This way you are able to Disable\Enable SignalSystem without break the game.
            customAnim.isDeployed = stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED;
            antenna.extended = stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED;
          }
          if (hasEC)
          {
            // Recover antennaPower
            transmitter.antennaPower = antenna.dist;

            if (stockAnim != null)
            {
              // Allow extending/retracting when has ec
              stockAnim.Events["Extend"].active = true;
              stockAnim.Events["Retract"].active = true;

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
                return true;
              }
              else
              {
                // if hit here, this means that antenna has animation but is not extended nor playing.
                // no ecCost
                return false;
              }
            }
            else
            {
              actualECCost = ecCost;
              isMoving = false;
              return true;
            }
          }
          else
          {
            if (stockAnim != null)
            {
              // Don't allow extending/retracting when has no ec
              stockAnim.Events["Extend"].active = false;
              stockAnim.Events["Retract"].active = false;
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
