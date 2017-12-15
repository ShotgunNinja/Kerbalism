using System;
using System.Collections.Generic;
using System.Reflection;

namespace KERBALISM
{
  public abstract class DeployBase : PartModule, IResourceConsumer
  {
    public List<PartResourceDefinition> GetConsumedResources() => new List<PartResourceDefinition>() { PartResourceLibrary.Instance.GetDefinition("ElectricCharge") };

    [KSPField] public double ecCost = 0;                              // ecCost to keep the part active
    [KSPField] public double ecDeploy = 0;                            // ecCost to do a deploy(animation)
    [KSPField(isPersistant = true)] public bool wasDeploySystem;      //  This identify if DeploySystem has disabled the function

    [KSPField(guiName = "EC Usage", guiUnits = "/sec", guiActive = true, guiFormat = "F2")]
    public double actualECCost = 0;                                   // Show EcConsume on part display
    public bool hasEC;                                                // Check if vessel has EC to consume, otherwise will disable animations and functions of the part.

    public resource_info resourceInfo;
    public string CurrentModule;                                      // Used it to FixDeploySystem
    public bool isConsuming;

    public override void OnStart(StartState state)
    {
      base.OnStart(state);
      Fields["actualECCost"].guiActive = Features.Deploy;
      CurrentModule = this.GetType().Name;
      FixDeploySystem();
    }

    public virtual void Update()
    {
      if (Lib.IsFlight() && Features.Deploy)
      {
        // get ec resource handler
        resourceInfo = ResourceCache.Info(vessel, "ElectricCharge");
        hasEC = resourceInfo.amount > double.Epsilon;

        isConsuming = GetisConsuming;
      }
    }

    public virtual void FixedUpdate()
    {
      if (Lib.IsFlight() && Features.Deploy)
      {
        part.ModulesOnUpdate();   // NEED TO FIX: I don't want to update the modules on FixedUpdate, but I need update it because, it is possible that IsDoingAction has changed the module

        if (isConsuming)
        {
          resourceInfo.Consume(actualECCost * Kerbalism.elapsed_s);
        }
        else actualECCost = 0;
      }
    }

    // Define when it is consuming EC
    public abstract bool GetisConsuming { get; }

    // Used to enable parts that was disable by DeploySystem
    public virtual void FixDeploySystem()
    {
      PartModule pModule = Lib.FindModule(part, CurrentModule);
      if (pModule != null) part.RemoveModule(pModule);
    }
  }
}
