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

      if (lights1 != null) pModule = lights1;
      else if (lights2 != null) pModule = lights1;

      base.OnStart(state);
    }

    public override bool GetisConsuming
    {
      get
      {
        if (hasEC)
        {
          isActionGroupchanged = false;
          if (lights1 != null)
          {
            lights1.Events["Toggle"].active = true;
            ToggleActions(lights1, true);
            if (lights1.animSpeed > 0)
            {
              actualECCost = ecCost;
              return true;
            }
          }
          else if (lights2 != null)
          {
            lights2.Events["ToggleEvent"].active = true;
            ToggleActions(lights2, true);
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
            lights1.Events["Toggle"].active = false;
            ToggleActions(lights1, false);
          }
          else if (lights2 != null)
          {
            if (lights2.animState) lights2.ToggleEvent();
            lights2.Events["ToggleEvent"].active = false;
            ToggleActions(lights2, false);
          }
          isActionGroupchanged = true;
        }
        return false;
      }
    }
  }
}
