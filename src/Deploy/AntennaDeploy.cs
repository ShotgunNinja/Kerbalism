namespace KERBALISM
{
  // This class will support two signal system (Kerbalism Signal & CommNet)
  public class AntennaDeploy : DeployBase
  {
    Antenna antenna;
    ModuleAnimationGroup customAnim;

    ModuleDataTransmitter transmitter;
    ModuleDeployableAntenna stockAnim;

    [KSPField(isPersistant = true)] double rightDistValue;     // Needs to support CommNet

    // Extra condition to IsConsuming
    bool isTransmitting;

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
      if (Lib.IsFlight() && Features.Signal)
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

        base.Update();
        if (isTransmitting)
        {
          actualECCost = antenna.cost;
          // Kerbalism already has logic to consume EC when it is transmitting
          isConsuming = false;
        }
      }
    }

    public override bool GetisConsuming
    {
      get
      {
        if (Features.Signal)
        {
          // Just to make sure that has the module target
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
                return true;
              }
              else if (customAnim.isDeployed)
              {
                customAnim.Events["RetractModule"].guiActive = customAnim.Events["RetractModule"].guiActiveUncommand = customAnim.Events["RetractModule"].guiActiveUnfocused = true;
                customAnim.Events["DeployModule"].guiActive = customAnim.Events["DeployModule"].guiActiveUncommand = customAnim.Events["DeployModule"].guiActiveUnfocused = false;

                // Makes antenna valid to AntennaInfo
                antenna.extended = true;
                actualECCost = ecCost;
                return true;
              }
              else
              {
                customAnim.Events["RetractModule"].guiActive = customAnim.Events["RetractModule"].guiActiveUncommand = customAnim.Events["RetractModule"].guiActiveUnfocused = false;
                customAnim.Events["DeployModule"].guiActive = customAnim.Events["DeployModule"].guiActiveUncommand = customAnim.Events["DeployModule"].guiActiveUnfocused = true;
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
                return true;
              }
              else if (stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED)
              {
                stockAnim.Events["Retract"].guiActive = stockAnim.Events["Retract"].guiActiveUncommand = stockAnim.Events["Retract"].guiActiveUnfocused = true;
                stockAnim.Events["Extend"].guiActive = stockAnim.Events["Extend"].guiActiveUncommand = stockAnim.Events["Extend"].guiActiveUnfocused = false;

                // Recover antennaPower only if antenna is Extended
                transmitter.antennaPower = rightDistValue;

                actualECCost = ecCost;
                return true;
              }
              else
              {
                // antenna is retract
                stockAnim.Events["Retract"].guiActive = stockAnim.Events["Retract"].guiActiveUncommand = stockAnim.Events["Retract"].guiActiveUnfocused = false;
                stockAnim.Events["Extend"].guiActive = stockAnim.Events["Extend"].guiActiveUncommand = stockAnim.Events["Extend"].guiActiveUnfocused = true;
                return false;
              }
            }
            else
            {
              wasDeploySystem = true;
              // Recover antennaPower for fixed antenna
              transmitter.antennaPower = antenna.dist;
              actualECCost = ecCost;
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
            customAnim.Events["RetractModule"].guiActive = customAnim.Events["RetractModule"].guiActiveUncommand = customAnim.Events["RetractModule"].guiActiveUnfocused = customAnim.isDeployed;
            customAnim.Events["DeployModule"].guiActive = customAnim.Events["DeployModule"].guiActiveUncommand = customAnim.Events["DeployModule"].guiActiveUnfocused = !customAnim.isDeployed;
            // Makes antenna valid to AntennaInfo
            antenna.extended = customAnim.isDeployed;
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
            bool isExtended = stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED;
            stockAnim.Events["Retract"].guiActive = stockAnim.Events["Retract"].guiActiveUncommand = stockAnim.Events["Retract"].guiActiveUnfocused = isExtended;
            stockAnim.Events["Extend"].guiActive = stockAnim.Events["Extend"].guiActiveUncommand = stockAnim.Events["Extend"].guiActiveUnfocused = !isExtended;
          }
        }
        // Leaving with the default game logic
        wasDeploySystem = false;
      }
    }

    public static void BackgroundUpdate(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot antenna, vessel_info vi, resource_info ec, double elapsed_s)
    {
      if (Features.Deploy)
      {
        bool has_ec = ec.amount > double.Epsilon;

        ProtoPartModuleSnapshot deployModule = p.FindModule("AntennaDeploy");

        // if it is transmitting, leave with Kerbalism
        if (Features.Science && (vi.transmitting.Length > 0 || vi.relaying.Length > 0)) return;

        if (has_ec)
        {
          if (Features.Signal)
          {
            if (!Settings.ExtendedAntenna || Lib.Proto.GetBool(antenna, "extended"))
            {
              ec.Consume(Lib.Proto.GetDouble(deployModule, "ecCost") * elapsed_s);
            }
          }
          else if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
          {
            if (Lib.Proto.GetBool(antenna, "canComm"))
            {
              ec.Consume(Lib.Proto.GetDouble(deployModule, "ecCost") * elapsed_s);
            }
          }
        }
        else
        {
          if (Features.Signal)
          {
            Lib.Proto.Set(antenna, "extended", false);
          }
          else if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
          {
            Lib.Proto.Set(antenna, "canComm",false);
          }
        }
      }
    }
  }
}
