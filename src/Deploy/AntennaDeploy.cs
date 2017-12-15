namespace KERBALISM
{
  // This class will support two signal system (Kerbalism Signal & CommNet)
  public class AntennaDeploy : DeployBase
  {
    Antenna antenna;
    ModuleAnimationGroup customAnim;

    ModuleDataTransmitter transmitter;
    ModuleDeployableAntenna stockAnim;

    [KSPField(isPersistant = true)] double rightDistValue;     // Need to support CommNet

    // When is consuming EC
    bool isTransmitting;
    bool isMoving;

    public override void OnStart(StartState state)
    {
      base.OnStart(state);
      if (Features.Deploy)
      {
        if (state == StartState.Editor && state == StartState.None && state == StartState.PreLaunch) return;

        // Kerbalism modules
        antenna = part.FindModuleImplementing<Antenna>();
        customAnim = part.FindModuleImplementing<ModuleAnimationGroup>();
        // KSP modules
        // I'm using this.dist to save transmitter.antennaPower.
        //  CommNet - transmitter.canComm() = (isdeploy || moduleisActive)
        //    When the transmitter has no deploy(is fixed), isdeploy= True, 
        //    Then the only way to disable the connection for this transmitter type is setting distance to 0 when no EC, forcing CommNet lost connection.
        //    When need enable back, take the information from this.dist
        transmitter = part.FindModuleImplementing<ModuleDataTransmitter>();
        stockAnim = part.FindModuleImplementing<ModuleDeployableAntenna>();
      }
    }

    public override void Update()
    {
      if (Lib.IsFlight())
      {
        if (Features.Signal)
        {
          // Check if it is transmitting
          if (!Features.Science) isTransmitting = antenna.stream.transmitting();
          else
          {
            // get info from the cache
            vessel_info vi = Cache.VesselInfo(vessel);
            // consume ec if data is transmitted or relayed
            isTransmitting = (vi.transmitting.Length > 0 || vi.relaying.Length > 0);
          }
        }
        base.Update();
        if (isTransmitting) actualECCost = antenna.cost;
        else if(!isConsuming) actualECCost = 0;
      }
    }

    public override void FixedUpdate()
    {
      if (Lib.IsFlight() && Features.Deploy)
      {
        if (isConsuming)
        {
          if (isMoving || !isTransmitting)
          {
            base.FixedUpdate();
          }
        }
      }
    }

    public override bool GetisConsuming
    {
      get
      {
        if (Features.Signal)
        {
          // Just to make sure that has the modules target
          if (antenna == null) return false;

          if (hasEC)
          {
            wasDeploySystem = false;
            if (customAnim != null)
            {
              // Add cost to Extending/Retracting
              if (customAnim.DeployAnimation.isPlaying)
              {
                actualECCost = ecDeploy;
                isMoving = true;
                return true;
              }
              else if (customAnim.isDeployed)
              {
                customAnim.Events["RetractModule"].guiActive = customAnim.Events["RetractModule"].guiActiveUncommand = customAnim.Events["RetractModule"].guiActiveUnfocused = true;
                customAnim.Events["DeployModule"].guiActive = customAnim.Events["DeployModule"].guiActiveUncommand = customAnim.Events["DeployModule"].guiActiveUnfocused = false;

                // Makes antenna valid to AntennaInfo
                antenna.extended = true;
                actualECCost = ecCost;
                isMoving = false;
                return true;
              }
              else
              {
                customAnim.Events["RetractModule"].guiActive = customAnim.Events["RetractModule"].guiActiveUncommand = customAnim.Events["RetractModule"].guiActiveUnfocused = false;
                customAnim.Events["DeployModule"].guiActive = customAnim.Events["DeployModule"].guiActiveUncommand = customAnim.Events["DeployModule"].guiActiveUnfocused = true;
                isMoving = false;
                return false;
              }
            }
            else
            {
              // this means that antenna is fixed
              // Makes antenna valid to AntennaInfo
              antenna.extended = true;
              actualECCost = ecCost;
              return true;
            }
          }
          else
          {
            wasDeploySystem = true;
            if (customAnim != null)
            {
              // Don't allow extending/retracting when has no ec
              customAnim.Events["RetractModule"].guiActive = customAnim.Events["RetractModule"].guiActiveUncommand = customAnim.Events["RetractModule"].guiActiveUnfocused = false;
              customAnim.Events["DeployModule"].guiActive = customAnim.Events["DeployModule"].guiActiveUncommand = customAnim.Events["DeployModule"].guiActiveUnfocused = false;
            }
            // Makes antennaModule invalid to AntennaInfo
            antenna.extended = false;
            isMoving = false;
            return false;
          }
        }
        else if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
        {
          if (transmitter == null) return false;

          // Save antennaPower
          rightDistValue = (rightDistValue != transmitter.antennaPower && transmitter.antennaPower > 0 ? transmitter.antennaPower : rightDistValue);

          if (hasEC)
          {
            wasDeploySystem = false;
            if (stockAnim != null)
            {
              // Add cost to Extending/Retracting
              if (stockAnim.deployState == ModuleDeployablePart.DeployState.RETRACTING || stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDING)
              {
                actualECCost = ecDeploy;
                isMoving = true;
                return true;
              }
              else if (stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED)
              {
                stockAnim.Events["Retract"].guiActive = stockAnim.Events["Retract"].guiActiveUncommand = stockAnim.Events["Retract"].guiActiveUnfocused = true;
                stockAnim.Events["Extend"].guiActive = stockAnim.Events["Extend"].guiActiveUncommand = stockAnim.Events["Extend"].guiActiveUnfocused = false;

                // Recover antennaPower only if antenna is Extended
                transmitter.antennaPower = rightDistValue;

                actualECCost = ecCost;
                isMoving = false;
                return true;
              }
              else
              {
                // antenna is retract
                stockAnim.Events["Retract"].guiActive = stockAnim.Events["Retract"].guiActiveUncommand = stockAnim.Events["Retract"].guiActiveUnfocused = false;
                stockAnim.Events["Extend"].guiActive = stockAnim.Events["Extend"].guiActiveUncommand = stockAnim.Events["Extend"].guiActiveUnfocused = true;

                isMoving = false;
                return false;
              }
            }
            else
            {
              wasDeploySystem = true;
              // Recover antennaPower for fixed antenna
              transmitter.antennaPower = antenna.dist;
              actualECCost = ecCost;
              isMoving = false;
              return true;
            }
          }
          else
          {
            wasDeploySystem = true;
            if (stockAnim != null)
            {
              // Don't allow extending/retracting when has no ec
              stockAnim.Events["Retract"].guiActive = stockAnim.Events["Retract"].guiActiveUncommand = stockAnim.Events["Retract"].guiActiveUnfocused = false;
              stockAnim.Events["Extend"].guiActive = stockAnim.Events["Extend"].guiActiveUncommand = stockAnim.Events["Extend"].guiActiveUnfocused = false;
            }
            // Change the range to 0, causing CommNet to lose the signal
            transmitter.antennaPower = 0;
            return false;
          }
        }
        else return false;
      }
    }

    public override void FixDeploySystem()
    {
      if (!Features.Deploy && wasDeploySystem)
      {
        if (Features.Signal)
        {
          if (customAnim != null)
          {
            if (customAnim.isDeployed)
            {
              customAnim.Events["RetractModule"].guiActive = customAnim.Events["RetractModule"].guiActiveUncommand = customAnim.Events["RetractModule"].guiActiveUnfocused = true;
              customAnim.Events["DeployModule"].guiActive = customAnim.Events["DeployModule"].guiActiveUncommand = customAnim.Events["DeployModule"].guiActiveUnfocused = false;
              // Makes antenna valid to AntennaInfo
              antenna.extended = true;
            }
            else
            {
              // Antenna is retract.
              customAnim.Events["RetractModule"].guiActive = customAnim.Events["RetractModule"].guiActiveUncommand = customAnim.Events["RetractModule"].guiActiveUnfocused = false;
              customAnim.Events["DeployModule"].guiActive = customAnim.Events["DeployModule"].guiActiveUncommand = customAnim.Events["DeployModule"].guiActiveUnfocused = true;
            }
          }
          else
          {
            // this means that antenna is fixed
            // Makes antenna valid to AntennaInfo
            antenna.extended = true;
          }
        }
        else if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
        {
          // Recover antennaPower only if antenna is Extended
          transmitter.antennaPower = rightDistValue;

          if (stockAnim != null)
          {
            if (stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED)
            {
              stockAnim.Events["Retract"].guiActive = stockAnim.Events["Retract"].guiActiveUncommand = stockAnim.Events["Retract"].guiActiveUnfocused = true;
              stockAnim.Events["Extend"].guiActive = stockAnim.Events["Extend"].guiActiveUncommand = stockAnim.Events["Extend"].guiActiveUnfocused = false;
            }
            else
            {
              // antenna is retract
              stockAnim.Events["Retract"].guiActive = stockAnim.Events["Retract"].guiActiveUncommand = stockAnim.Events["Retract"].guiActiveUnfocused = false;
              stockAnim.Events["Extend"].guiActive = stockAnim.Events["Extend"].guiActiveUncommand = stockAnim.Events["Extend"].guiActiveUnfocused = true;
            }
          }
        }
        // Leaving this the default game logic
        wasDeploySystem = false;
      }
    }
  }
}
