using System;
using System.Collections.Generic;
using System.Reflection;

namespace KERBALISM
{
  public abstract class DeployBase : PartModule, IResourceConsumer
  {
    public List<PartResourceDefinition> GetConsumedResources() => new List<PartResourceDefinition>() { PartResourceLibrary.Instance.GetDefinition("ElectricCharge") };

    // Control Update
    public double lastUpdated;

    // ecCost to keep the part active
    [KSPField]
    public double ecCost = 0;

    // ecCost to do a deploy(animation)
    [KSPField]
    public double ecDeploy = 0;

    // Show value on part Display
    [KSPField(guiName = "EC Usage", guiUnits = "/sec", guiActive = true, guiFormat = "F2")]
    public double actualECCost = 0;

    // Check if vessel has EC to consume, otherwise will disable animations and functions of the part.
    public bool hasEC;

    // When it is consuming EC
    public bool isActive;

    public virtual void Update()
    {
      if (Lib.IsFlight())
      {
        hasEC = ResourceCache.Info(part.vessel, "ElectricCharge").amount > double.Epsilon;
        isActive = IsDoingAction;
      }
    }

    public virtual void FixedUpdate()
    {
      if (Lib.IsFlight())
      {
        part.ModulesOnUpdate();
        if (IsDoingAction)
        {
          // get resource cache
          vessel_resources resources = ResourceCache.Get(part.vessel);
          resources.Consume(part.vessel, "ElectricCharge", actualECCost * Kerbalism.elapsed_s);
        }
        else actualECCost = 0;
      }
    }

    public abstract bool IsDoingAction { get; }
  }
}
