using ModuleWheels;

namespace KERBALISM
{
  public class LandingGearDeploy : DeployBase
  {
    ModuleWheelDeployment gear;

    public override void OnStart(StartState state)
    {
      if (state == StartState.Editor && state == StartState.None && state == StartState.PreLaunch) return;
      gear = part.FindModuleImplementing<ModuleWheelDeployment>();

      if (gear != null)
      {
        pModule = gear;
        base.OnStart(state);
      }
    }

    public override bool GetisConsuming
    {
      get
      {
        // Just making sure that we have the target module
        if (gear == null) return false;

        if (hasEC)
        {
          gear.Events["EventToggle"].active = true;
          ToggleActions(gear, true);
          isActionGroupchanged = false;

          if (gear.stateString == "Deploying..." || gear.stateString == "Retracting...")
          {
            actualECCost = ecDeploy;
            return true;
          }
        }
        else
        {
          gear.Events["EventToggle"].active = false;
          ToggleActions(gear, false);
          isActionGroupchanged = true;
        }
        return false;
      }
    }
  }
}
