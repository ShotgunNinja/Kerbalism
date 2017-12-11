using System.Collections.Generic;

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

    // I check if vessel has EC to consume, otherwise will disable animations and functions of the part.
    public bool hasEC;
    // To do less botton flash, only show action if has more ec that deploy needs.
    public double amountECleft;

    public string thisModule;

    public bool FixGame(string module)
    {
      if (!Features.Deploy)
      {
        PartModule thisModule = Lib.FindModule(part, module);
        if (thisModule != null)
        {
          bool isActive = IsActive;
          part.RemoveModule(thisModule);
        }
        return true;
      }
      return false;
    }

    public virtual void Update()
    {
      if (FixGame(thisModule)) return;
      amountECleft = ResourceCache.Info(part.vessel, "ElectricCharge").amount;
      hasEC = amountECleft > double.Epsilon;
    }

    public virtual void FixedUpdate()
    {
      part.ModulesOnUpdate();
      if (IsActive)
      {
        // get resource cache
        vessel_resources resources = ResourceCache.Get(part.vessel);
        resources.Consume(part.vessel, "ElectricCharge", actualECCost * Kerbalism.elapsed_s);
      }
      else actualECCost = 0;
    }

    public abstract bool IsActive { get; }

    public abstract void Start();
  }
}
