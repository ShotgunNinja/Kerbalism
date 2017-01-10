﻿using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Emitter : PartModule, ISpecifics
{
  // config
  [KSPField(isPersistant = true)] public double radiation;  // radiation in rad/s
  [KSPField(isPersistant = true)] public double ec_rate;    // EC consumption rate per-second (optional)
  [KSPField] public bool toggle;                            // true if the effect can be toggled on/off
  [KSPField] public string active;                          // name of animation to play when enabling/disabling

  // persistent
  [KSPField(isPersistant = true)] public bool running;

  // rmb status
  [KSPField(guiActive = true, guiActiveEditor = true, guiName = "_")] public string Status;  // rate of radiation emitted/shielded

  // animations
  Animator active_anim;

  // pseudo-ctor
  public override void OnStart(StartState state)
  {
    // update RMB ui
    Fields["Status"].guiName = radiation >= 0.0 ? "Radiation" : "Active shielding";
    Events["Toggle"].active = toggle;
    Actions["Action"].active = toggle;

    // deal with non-toggable emitters
    if (!toggle) running = true;

    // create animator
    active_anim = new Animator(part, active);

    // set animation initial state
    active_anim.still(running ? 0.0 : 1.0);
  }


  public void Update()
  {
    // update ui
    Status = running ? Lib.HumanReadableRadiation(Math.Abs(radiation)) : "none";
    Events["Toggle"].guiName = Lib.StatusToggle("Active shield", running ? "active" : "disabled");
  }



  public void FixedUpdate()
  {
    // do nothing in the editor
    if (Lib.IsEditor()) return;

    // if enabled, and there is ec consumption
    if (running && ec_rate > double.Epsilon)
    {
      // get resource cache
      resource_info ec = ResourceCache.Info(vessel, "ElectricCharge");

      // consume EC
      ec.Consume(ec_rate * Kerbalism.elapsed_s);
    }
  }


  public static void BackgroundUpdate(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, Emitter emitter, resource_info ec, double elapsed_s)
  {
    // if enabled, and EC is required
    if (Lib.Proto.GetBool(m, "running") && emitter.ec_rate > double.Epsilon)
    {
      // consume EC
      ec.Consume(emitter.ec_rate * elapsed_s);
    }
  }


  [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
  public void Toggle()
  {
    // switch status
    running = !running;

    // play animation
    active_anim.play(running, false);
  }


  // action groups
  [KSPAction("Toggle Active Shield")] public void Action(KSPActionParam param) { Toggle(); }



  // part tooltip
  public override string GetInfo()
  {
    string desc = radiation > double.Epsilon
      ? "Emit ionizing radiation"
      : "Reduce incoming radiation";

    return Specs().info(desc);
  }


  // specifics support
  public Specifics Specs()
  {
    Specifics specs = new Specifics();
    specs.add(radiation >= 0.0 ? "Radiation emitted" : "Active shielding", Lib.HumanReadableRadiation(Math.Abs(radiation)));
    if (ec_rate > double.Epsilon) specs.add("EC/s", Lib.HumanReadableRate(ec_rate));
    return specs;
  }


  // return total radiation emitted in a vessel
  public static double Total(Vessel v)
  {
    // get resource cache
    resource_info ec = ResourceCache.Info(v, "ElectricCharge");

    double tot = 0.0;
    if (v.loaded)
    {
      foreach(var emitter in Lib.FindModules<Emitter>(v))
      {
        if (ec.amount > double.Epsilon || emitter.ec_rate <= double.Epsilon)
        {
          tot += emitter.running ? emitter.radiation : 0.0;
        }
      }
    }
    else
    {
      foreach(ProtoPartModuleSnapshot m in Lib.FindModules(v.protoVessel, "Emitter"))
      {
        if (ec.amount > double.Epsilon || Lib.Proto.GetDouble(m, "ec_rate") <= double.Epsilon)
        {
          tot += Lib.Proto.GetBool(m, "running") ? Lib.Proto.GetDouble(m, "radiation") : 0.0;
        }
      }
    }
    return tot;
  }
}


} // KERBALISM

