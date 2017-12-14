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
    // Show value on part Display
    [KSPField(guiName = "Transmitting", guiUnits = "", guiActive = true, guiFormat = "")]
    bool isTransmitting;
    [KSPField(guiName = "Moving", guiUnits = "", guiActive = true, guiFormat = "")]
    bool isMoving;

    // Show value on part Display
    [KSPField(guiName = "EC Usage", guiUnits = "/sec", guiActive = true, guiFormat = "F2")]
    public double antennaCost;

    public override void OnStart(StartState state)
    {
      if (state == StartState.Editor && state == StartState.None && state == StartState.PreLaunch) return;

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
      if (antenna != null && transmitter != null)
      {
        antenna.isEnabled = Features.Signal;
        antenna.moduleIsEnabled= Features.Signal;
        transmitter.isEnabled = !Features.Signal;
      }

      if (customAnim != null && stockAnim != null)
      {
        antenna.extended = customAnim.isDeployed;
        customAnim.isEnabled = Features.Signal;
        customAnim.moduleIsEnabled = Features.Signal;

        stockAnim.isEnabled = !Features.Signal;
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

          if (!isTransmitting) antennaCost = antenna.cost;
          else antennaCost = 0;

          if (IsDoingAction)
          {
            part.ModulesOnUpdate();
            vessel_resources resources = ResourceCache.Get(part.vessel);
            if (isMoving || !isTransmitting)
            {
              resources.Consume(part.vessel, "ElectricCharge", actualECCost * Kerbalism.elapsed_s);
            }
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
            stockAnim.isEnabled = false;
          }

          if (hasEC)
          {
            if (customAnim != null)
            {
              antenna.extended = customAnim.isDeployed;

              // Add cost to Extending/Retracting
              if (customAnim.DeployAnimation.isPlaying)
              {
                // Don't show Event when is doing extending/retracting event
                stockAnim.Events["RetractModule"].guiActive = false;
                stockAnim.Events["RetractModule"].guiActiveUncommand = false;
                stockAnim.Events["RetractModule"].guiActiveUnfocused = false;

                stockAnim.Events["DeployModule"].guiActive = false;
                stockAnim.Events["DeployModule"].guiActiveUncommand = false;
                stockAnim.Events["DeployModule"].guiActiveUnfocused = false;

                actualECCost = ecDeploy;
                isMoving = true;
                return true;
              }
              else if (customAnim.isDeployed)
              {
                // Don't show Event when is doing extending/retracting event
                stockAnim.Events["RetractModule"].guiActive = true;
                stockAnim.Events["RetractModule"].guiActiveUncommand = true;
                stockAnim.Events["RetractModule"].guiActiveUnfocused = true;

                stockAnim.Events["DeployModule"].guiActive = false;
                stockAnim.Events["DeployModule"].guiActiveUncommand = false;
                stockAnim.Events["DeployModule"].guiActiveUnfocused = false;

                actualECCost = ecCost;
                isMoving = false;
                return true;
              }
              else
              {
                // if hit here, this means that antenna has animation but is not extended nor playing.
                // no ecCost

                // Don't show Event when is doing extending/retracting event
                stockAnim.Events["RetractModule"].guiActive = false;
                stockAnim.Events["RetractModule"].guiActiveUncommand = false;
                stockAnim.Events["RetractModule"].guiActiveUnfocused = false;

                stockAnim.Events["DeployModule"].guiActive = true;
                stockAnim.Events["DeployModule"].guiActiveUncommand = true;
                stockAnim.Events["DeployModule"].guiActiveUnfocused = true;


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
              stockAnim.Events["RetractModule"].guiActive = false;
              stockAnim.Events["RetractModule"].guiActiveUncommand = false;
              stockAnim.Events["RetractModule"].guiActiveUnfocused = false;

              stockAnim.Events["DeployModule"].guiActive = false;
              stockAnim.Events["DeployModule"].guiActiveUncommand = false;
              stockAnim.Events["DeployModule"].guiActiveUnfocused = false;
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
            customAnim.isEnabled = false;
            customAnim.isDeployed = stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED;
            antenna.extended = stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED;
          }
          if (hasEC)
          {
            if (stockAnim != null)
            {
              // Allow extending/retracting when has ec

              // Add cost to Extending/Retracting
              if (stockAnim.deployState == ModuleDeployablePart.DeployState.RETRACTING || stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDING)
              {
                // Don't show Event when is doing extending/retracting event
                stockAnim.Events["Retract"].guiActive = false;
                stockAnim.Events["Retract"].guiActiveUncommand = false;
                stockAnim.Events["Retract"].guiActiveUnfocused = false;

                stockAnim.Events["Extend"].guiActive = false;
                stockAnim.Events["Extend"].guiActiveUncommand = false;
                stockAnim.Events["Extend"].guiActiveUnfocused = false;

                actualECCost = ecDeploy;
                isMoving = true;
                return true;
              }
              else if (stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED)
              {
                // Recover antennaPower only if antenna is Extended
                transmitter.antennaPower = antenna.dist;

                stockAnim.Events["Retract"].guiActive = true;
                stockAnim.Events["Retract"].guiActiveUncommand = true;
                stockAnim.Events["Retract"].guiActiveUnfocused = true;

                stockAnim.Events["Extend"].guiActive = false;
                stockAnim.Events["Extend"].guiActiveUncommand = false;
                stockAnim.Events["Extend"].guiActiveUnfocused = false;

                actualECCost = ecCost;
                isMoving = false;
                return true;
              }
              else
              {
                stockAnim.Events["Retract"].guiActive = false;
                stockAnim.Events["Retract"].guiActiveUncommand = false;
                stockAnim.Events["Retract"].guiActiveUnfocused = false;

                stockAnim.Events["Extend"].guiActive = true;
                stockAnim.Events["Extend"].guiActiveUncommand = true;
                stockAnim.Events["Extend"].guiActiveUnfocused = true;

                // if hit here, this means that antenna has animation but is not extended nor playing.
                // no ecCost
                return false;
              }
            }
            else
            {
              // Recover antennaPower for fixed antenna
              transmitter.antennaPower = antenna.dist;
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
              stockAnim.Events["Retract"].guiActive = false;
              stockAnim.Events["Retract"].guiActiveUncommand = false;
              stockAnim.Events["Retract"].guiActiveUnfocused = false;

              stockAnim.Events["Extend"].guiActive = false;
              stockAnim.Events["Extend"].guiActiveUncommand = false;
              stockAnim.Events["Extend"].guiActiveUnfocused = false;
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
