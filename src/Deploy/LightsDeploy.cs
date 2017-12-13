namespace KERBALISM
{
  public class LightsDeploy : DeployBase
  {
    ModuleAnimateGeneric lights1;
    ModuleColorChanger lights2;

		public override void OnStart(StartState state)
		{
			if (state == StartState.Editor && state == StartState.None && state == StartState.PreLaunch) return;
      lights1 = part.FindModuleImplementing<ModuleAnimateGeneric>();
      lights2 = part.FindModuleImplementing<ModuleColorChanger>();
    }

    public override bool IsDoingAction
    {
      get
      {
        if (!Features.Deploy)
        {
          if (lights1 != null)
          {
            lights1.Events["Toggle"].guiActive = lights1.Events["Toggle"].guiActiveUnfocused = true;
          }
          else if (lights2 != null)
          {
            lights2.Events["ToggleEvent"].guiActive = lights2.Events["ToggleEvent"].guiActiveUnfocused = true;
          }
          return false;
        }
        if (hasEC)
        {
          if (lights1 != null)
          {
            lights1.Events["Toggle"].guiActive = lights1.Events["Toggle"].guiActiveUnfocused = true;
            if (lights1.animSpeed > 0)
            {
              actualECCost = ecCost;
              return true;
            }
          }
          else if (lights2 != null)
          {
            lights2.Events["ToggleEvent"].guiActive = lights2.Events["ToggleEvent"].guiActiveUnfocused = true;
            if (lights2.animState)
            {
              actualECCost = ecCost;
              return true;
            }
          }
        }
        else
        {
          if (lights1 != null)
          {
            if (lights1.animSpeed > 0) lights1.Toggle();
            lights1.Events["Toggle"].guiActive = lights1.Events["Toggle"].guiActiveUnfocused = false;
          }
          else if (lights2 != null)
          {
            if (lights2.animState) lights2.ToggleEvent();
            lights2.Events["ToggleEvent"].guiActive = lights2.Events["ToggleEvent"].guiActiveUnfocused = false;
          }
        }

        actualECCost = 0;
        return false;
      }
    }
  }
}
