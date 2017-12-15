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
    if (!Features.Deploy) has_ec = true;
    else has_ec = ResourceCache.Info(antenna.part.vessel, "ElectricCharge").amount > double.Epsilon;
  }

  public AntennaDevice(ModuleDataTransmitter transmitter)
  {
    this.transmitter = transmitter;
    stockAnim = this.transmitter.part.FindModuleImplementing<ModuleDeployableAntenna>();
    if(!Features.Deploy)has_ec = true;
    has_ec = ResourceCache.Info(transmitter.part.vessel, "ElectricCharge").amount > double.Epsilon;
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
      return stockAnim == null
        ? "fixed"
        : stockAnim.deployState == ModuleDeployablePart.DeployState.EXTENDED
        ? has_ec
        ? "<color=cyan>deployed</color>"
        : "<color=orange>inactive</color>"
        : "<color=red>retracted</color>";
    else
      return animator == null
        ? "fixed"
        : antenna.extended
        ? has_ec
        ? "<color=cyan>deployed</color>"
        : "<color=orange>inactive</color>"
        : "<color=red>retracted</color>";
  }

  public override void ctrl(bool value)
  {
    if (Features.Deploy)
    {
      if (!has_ec) return;
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
  bool has_ec;
}

public sealed class ProtoAntennaDevice : Device
{
  public ProtoAntennaDevice(ProtoPartModuleSnapshot antenna, uint part_id, Vessel v)
  {
    if (!Features.Deploy) has_ec = true;
    else has_ec = ResourceCache.Info(v, "ElectricCharge").amount > double.Epsilon;

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
      return !has_ec
       ? "<color=orange>inactive</color>"
       : animator == null
       ? "fixed"
       : Lib.Proto.GetString(animator, "deployState") == "EXTENDED"
       ? "<color=cyan>deployed</color>"
       : "<color=red>retracted</color>";
    else
      return !has_ec
       ? "<color=orange>inactive</color>"
       : animator == null
       ? "fixed"
       : Lib.Proto.GetBool(antenna, "extended")
       ? "<color=cyan>deployed</color>"
       : "<color=red>retracted</color>";
  }

  public override void ctrl(bool value)
  {
    if (Features.Deploy)
    {
      if (!has_ec) return;
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
  bool has_ec;

  Vessel vessel;
  uint part_id;
}


} // KERBALISM





