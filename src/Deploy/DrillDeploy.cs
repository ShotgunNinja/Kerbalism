namespace KERBALISM
{
  public class DrillDeploy : DeployBase
  {
    ModuleAnimationGroup mining;
    Harvester harvester;
    public override void OnStart(StartState state)
    {
      if (state == StartState.Editor && state == StartState.None && state == StartState.PreLaunch) return;
      thisModule = "DrillDeploy";
      mining = part.FindModuleImplementing<ModuleAnimationGroup>();
      foreach (PartModule pModule in part.FindModulesImplementing<Harvester>())
      {
        if (pModule.isEnabled) harvester = pModule as Harvester;
      }
    }

    public override void FixedUpdate()
    {
      if (mining != null)
      {
        mining.Events["RetractModule"].guiActive = mining.Events["RetractModule"].guiActiveUnfocused = mining.isDeployed && hasEC;
        mining.Events["DeployModule"].guiActive = mining.Events["DeployModule"].guiActiveUnfocused = !mining.isDeployed && hasEC;

        part.ModulesOnUpdate();
        if (IsActive)
        {
          // get resource cache
          if (harvester != null)
          {
            if (harvester.running)
            {
              // Just show the value on screen, but not consume
              return;
            }
          }

          vessel_resources resources = ResourceCache.Get(part.vessel);
          resources.Consume(part.vessel, "ElectricCharge", actualECCost * Kerbalism.elapsed_s);
        }
        else actualECCost = 0;
      }
    }

    public override bool IsActive
    {
      get
      {
        if (!Features.Deploy)
        {
          if (mining != null)
          {
            mining.Events["RetractModule"].guiActive = mining.Events["RetractModule"].guiActiveUnfocused = mining.isDeployed;
            mining.Events["DeployModule"].guiActive = mining.Events["DeployModule"].guiActiveUnfocused = !mining.isDeployed;
          }
        }
        if (mining != null)
        {
          mining.isEnabled = true;

          if (mining.ActiveAnimation.isPlaying && !harvester.running && hasEC)
          {
            actualECCost = ecDeploy;
            return true;
          }
          else if (harvester.running)
          {
            actualECCost = (float)harvester.ec_rate;
            return true;
          }
        }
        actualECCost = 0;
        return false;
      }
    }
  }
}
