﻿using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class HarvesterDevice : Device
{
  public HarvesterDevice(Harvester harvester)
  {
    this.harvester = harvester;
    this.animator = harvester.part.FindModuleImplementing<ModuleAnimationGroup>();
  }

  public override string name()
  {
    return Lib.BuildString(harvester.resource, " harvester").ToLower();
  }

  public override uint part()
  {
    return harvester.part.flightID;
  }

  public override string info()
  {
    return animator != null && !harvester.deployed
      ? "not deployed"
      : harvester.starved
      ? "no resource"
      : harvester.running
      ? "<color=cyan>running</color>"
      : "<color=red>stopped</color>";
  }

  public override void ctrl(bool value)
  {
    if (harvester.deployed)
    {
      harvester.running = value;
    }
  }

  public override void toggle()
  {
    ctrl(!harvester.running);
  }

  Harvester harvester;
  ModuleAnimationGroup animator;
}


public sealed class ProtoHarvesterDevice : Device
{
  public ProtoHarvesterDevice(ProtoPartModuleSnapshot harvester, Harvester prefab, uint part_id)
  {
    this.harvester = harvester;
    this.animator = FlightGlobals.FindProtoPartByID(part_id).FindModule("ModuleAnimationGroup");
    this.prefab = prefab;
    this.part_id = part_id;
  }

  public override string name()
  {
    return Lib.BuildString(prefab.resource, " harvester").ToLower();
  }

  public override uint part()
  {
    return part_id;
  }

  public override string info()
  {
    return animator != null && !Lib.Proto.GetBool(harvester, "deployed")
      ? "not deployed"
      : Lib.Proto.GetBool(harvester, "starved")
      ? "no resource"
      : Lib.Proto.GetBool(harvester, "running")
      ? "<color=cyan>running</color>"
      : "<color=red>stopped</color>";
  }

  public override void ctrl(bool value)
  {
    if (Lib.Proto.GetBool(harvester, "deployed"))
    {
      Lib.Proto.Set(harvester, "running", value);
    }
  }

  public override void toggle()
  {
    ctrl(!Lib.Proto.GetBool(harvester, "running"));
  }

  ProtoPartModuleSnapshot harvester;
  ProtoPartModuleSnapshot animator;
  Harvester prefab;
  uint part_id;
}


} // KERBALISM





