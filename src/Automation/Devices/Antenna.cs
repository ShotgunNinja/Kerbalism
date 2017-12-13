using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {

public sealed class AntennaDevice : Device
{
  public AntennaDevice(Antenna antenna)
  {
    this.antenna = antenna;
    animator = antenna.part.FindModuleImplementing<ModuleAnimationGroup>();
  }

  public AntennaDevice(ModuleDataTransmitter transmitter)
  {
    this.transmitter = transmitter;
    stockAnim = this.transmitter.part.FindModuleImplementing<ModuleDeployableAntenna>();
  }

  public override string name()
  {
    return "antenna";
  }

  public override uint part()
  {
    if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet) return transmitter.part.flightID;
    else return antenna.part.flightID;
  }

  public override string info()
  {
    if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
    {
      return stockAnim == null
        ? "fixed"
        : stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED
        ? "<color=cyan>deployed</color>"
        : "<color=red>retracted</color>";
    }
    else
        return animator == null
      ? "fixed"
      : antenna.extended
      ? "<color=cyan>deployed</color>"
      : "<color=red>retracted</color>";
  }

  public override void ctrl(bool value)
  {
    if (Features.Deploy)
    {
        if (Features.Signal)
        {
          if (ResourceCache.Info(antenna.part.vessel, "ElectricCharge").amount <= double.Epsilon)
          {
            return;
          }
        }
        else
        {
          if (ResourceCache.Info(stockAnim.part.vessel, "ElectricCharge").amount <= double.Epsilon)
          {
            return;
          }
        }
    }
    if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
    {
      if (stockAnim.deployState != ModuleDeployablePart.DeployState.EXTENDED && value) stockAnim.Extend();
      else if (stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED && !value) stockAnim.Retract();
    }
    if (animator != null)
    {
      if (!antenna.extended && value) animator.DeployModule();
      else if (antenna.extended && !value) animator.RetractModule();
    }
  }

  public override void toggle()
  {
    if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
    {
      if (stockAnim.deployState != ModuleDeployablePart.DeployState.EXTENDED) ctrl(true);
      else if (stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED) ctrl(false);
    }
    else if (animator != null)
    {
      ctrl(!antenna.extended);
    }
  }

  Antenna antenna;
  ModuleAnimationGroup animator;
  ModuleDataTransmitter transmitter;
  ModuleDeployableAntenna stockAnim;
}

public sealed class ProtoAntennaDevice : Device
{
  public ProtoAntennaDevice(ProtoPartModuleSnapshot antenna, uint part_id, Vessel v)
  {
    if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
    {
      this.antenna = FlightGlobals.FindProtoPartByID(part_id).FindModule("ModuleDataTransmitter");
      this.animator = FlightGlobals.FindProtoPartByID(part_id).FindModule("ModuleDeployableAntenna");
    }
    else
    { 
      this.antenna = antenna;
      this.animator = FlightGlobals.FindProtoPartByID(part_id).FindModule("ModuleAnimationGroup");
    }
    this.vessel = v;
    this.part_id = part_id;
  }

  public override string name()
  {
    return "antenna";
  }

  public override uint part()
  {
    return part_id;
  }

  public override string info()
  {
    if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
    {
      return animator == null
       ? "fixed"
       : Lib.Proto.GetString(animator, "deployState") == "EXTENDED"
       ? "<color=cyan>deployed</color>"
       : "<color=red>retracted</color>";
    }
    else
    { 
      return animator == null
        ? "fixed"
        : Lib.Proto.GetBool(antenna, "extended")
        ? "<color=cyan>deployed</color>"
        : "<color=red>retracted</color>";
    }
  }

  public override void ctrl(bool value)
  {
    if (Features.Deploy)
    {
      if (ResourceCache.Info(vessel, "ElectricCharge").amount <= double.Epsilon)
      {
        return;
      }
    }

    if (animator != null)
    {
      if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
      {
        string status = value ? "EXTENDED" : "RETRACTED";
        Lib.Proto.Set(antenna, "canComm", value);
        Lib.Proto.Set(animator, "deployState", status);
      }
      else
      { 
        Lib.Proto.Set(antenna, "extended", value);
        Lib.Proto.Set(animator, "isDeployed", value);
      }
    }
  }

  public override void toggle()
  {
    if (animator != null)
    {
      if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet) ctrl(Lib.Proto.GetString(animator, "deployState") == "RETRACTED");
      else ctrl(!Lib.Proto.GetBool(antenna, "extended"));
    }
  }

  ProtoPartModuleSnapshot antenna;
  ProtoPartModuleSnapshot animator;

  Vessel vessel;
  uint part_id;
}


} // KERBALISM





