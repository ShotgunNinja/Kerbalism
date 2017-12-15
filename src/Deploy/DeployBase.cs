using System;
using System.Collections.Generic;
using System.Reflection;

namespace KERBALISM
{
  public abstract class DeployBase : PartModule, IResourceConsumer
  {
    public List<PartResourceDefinition> GetConsumedResources() => new List<PartResourceDefinition>() { PartResourceLibrary.Instance.GetDefinition("ElectricCharge") };

    [KSPField(isPersistant = true)] public double ecCost = 0;         // ecCost to keep the part active
    [KSPField] public double ecDeploy = 0;                            // ecCost to do a deploy(animation)
    [KSPField(isPersistant = true)] public bool wasDeploySystem;      //  This identify if DeploySystem has disabled the function

    [KSPField(guiName = "EC Usage", guiUnits = "/sec", guiActive = true, guiFormat = "F2")]
    public double actualECCost = 0;                                   // Show EcConsume on part display
    public bool hasEC;                                                // Check if vessel has EC to consume, otherwise will disable animations and functions of the part.

    public bool isConsuming;

    public resource_info resourceInfo;

    public string CurrentModule;                                      // Useing it to FixDeploySystem

    public override void OnStart(StartState state)
    {
      base.OnStart(state);
      Fields["actualECCost"].guiActive = Features.Deploy;
      CurrentModule = this.GetType().Name;                            // Needs reviews to see if it is working.
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
        if (!isConsuming) actualECCost = 0;
      }
    }

    public virtual void FixedUpdate()
    {
      if (Lib.IsEditor()) return;

      if (Features.Deploy)
      {
        part.ModulesOnUpdate();   // NEED TO FIX: I don't want to update the modules on FixedUpdate, but I need update it because, it is possible that IsDoingAction has changed the module

        if (isConsuming)
        {
          if (resourceInfo != null) resourceInfo.Consume(actualECCost * Kerbalism.elapsed_s);
        }
      }
    }

    // Define when it is consuming EC
    public abstract bool GetisConsuming { get; }

    // Used to enable parts that was disable by DeploySystem
    // After enable, remove module.
    public virtual void FixDeploySystem()
    {
      if (CurrentModule != null)
      {
        PartModule pModule = Lib.FindModule(part, CurrentModule);
        if (pModule != null) part.RemoveModule(pModule);
      }
    }
  }
}
