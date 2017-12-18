namespace KERBALISM
{
  public class DrillDeploy : DeployBase
  {
    ModuleAnimationGroup mining;
    Harvester harvester;
    public override void OnStart(StartState state)
    {
      if (state == StartState.Editor && state == StartState.None && state == StartState.PreLaunch) return;
      mining = part.FindModuleImplementing<ModuleAnimationGroup>();
      foreach (PartModule pModule in part.FindModulesImplementing<Harvester>())
      {
        if (pModule.isEnabled) harvester = pModule as Harvester;
      }

      if (mining != null) pModule = mining;
      base.OnStart(state);
    }

    public override void Update()
    {
      if (Lib.IsFlight() && Features.Deploy)
      {
        base.Update();
        if (harvester.running)
        {
          actualECCost = (float)harvester.ec_rate;
          isConsuming = false;
        }
      }
    }

    public override bool GetisConsuming
    {
      get
      {
        // Just to make sure that has the module target
        if (mining != null)
        {
          // Update GUI
          mining.Events["RetractModule"].guiActive = mining.Events["RetractModule"].guiActiveUnfocused = mining.isDeployed && hasEC;
          mining.Events["DeployModule"].guiActive = mining.Events["DeployModule"].guiActiveUnfocused = !mining.isDeployed && hasEC;

          ToggleActions(mining, hasEC);

          if (mining.ActiveAnimation.isPlaying && hasEC)
          {
            actualECCost = ecDeploy;
            return true;
          }
        }
        return false;
      }
    }
  }
}
