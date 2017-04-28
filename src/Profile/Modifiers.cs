using System;
using System.Collections.Generic;
using System.Text;


namespace KERBALISM {


public static class Modifiers
{
  public static double evaluate(Vessel v, vessel_info vi, vessel_resources resources, List<string> modifiers)
  {
    double k = 1.0;
    foreach(string mod in modifiers)
    {
      switch(mod)
      {
        case "breathable":
          k *= vi.breathable ? 0.0 : 1.0;
          break;

        case "pos_flux":
          k *= vi.net_flux > 0.0 ? vi.net_flux : 0.0;
          break;

        case "neg_flux":
          k *= vi.net_flux < 0.0 ? Math.Abs(vi.net_flux) : 0.0;
          break;

        case "pos_temp":
          k *= vi.net_flux > 0.0 ? vi.hab_temp : 0.0;
          break;

        case "neg_temp":
          k *= vi.net_flux < 0.0 ? vi.hab_temp : 0.0;
          break;

        case "radiation":
          k *= vi.radiation;
          break;

        case "shielding":
          k *= 1.0 - vi.shielding;
          break;

        case "volume":
          k *= vi.volume;
          break;

        case "surface":
          k *= vi.surface;
          break;

        case "living_space":
          k /= vi.living_space;
          break;

        case "comfort":
          k /= vi.comforts.factor;
          break;

        case "mod_comfort_space_limiter":
          k *= vi.comforts.factor * (1.0 - vi.comforts.factor + vi.living_space);
          break;

        case "pressure":
          k *= vi.pressure > Settings.PressureThreshold ? 1.0 : Settings.PressureFactor;
          break;

        case "poisoning":
          k *= vi.poisoning > Settings.PoisoningThreshold ? 1.0 : Settings.PoisoningFactor;
          break;

        case "per_capita":
          k /= (double)Math.Max(vi.crew_count, 1);
          break;

        case "crew_count":
          k *= (double)vi.crew_count;
          break;

        case "inverse":
          k = k > double.Epsilon ? 1.0 / k : 0.0;
          break;

        default:
          k *= resources.Info(v, mod).amount;
          break;
      }
    }
    return k;
  }


  public static double evaluate(environment_analyzer env, vessel_analyzer va, resource_simulator sim, List<string> modifiers)
  {
    double k = 1.0;
    foreach(string mod in modifiers)
    {
      switch(mod)
      {
        case "breathable":
          k *= env.breathable ? 0.0 : 1.0;
          break;

        case "pos_flux":
          k *= va.net_flux > 0.0 ? va.net_flux : 0.0;
          break;

        case "neg_flux":
          k *= va.net_flux < 0.0 ? Math.Abs(va.net_flux) : 0.0;
          break;

        case "pos_temp":
          k *= va.net_flux > 0.0 ? va.hab_temp : 0.0;
          break;

        case "neg_temp":
          k *= va.net_flux < 0.0 ? va.hab_temp : 0.0;
          break;

        case "radiation":
          k *= Math.Max(Radiation.Nominal, (env.landed ? env.surface_rad : env.magnetopause_rad) + va.emitted);
          break;

        case "shielding":
          k *= 1.0 - va.shielding;
          break;

        case "volume":
          k *= va.volume;
          break;

        case "surface":
          k *= va.surface;
          break;

        case "living_space":
          k /= va.living_space;
          break;

        case "comfort":
          k /= va.comforts.factor;
          break;

        case "mod_comfort_space_limiter":
          k *= va.comforts.factor * (1.0 - va.comforts.factor + va.living_space);
          break;

        case "pressure":
          k *= va.pressurized ? 1.0 : Settings.PressureFactor;
          break;

        case "poisoning":
          k *= !va.scrubbed ? 1.0 : Settings.PoisoningFactor;
          break;

        case "per_capita":
          k /= (double)Math.Max(va.crew_count, 1);
          break;

        case "crew_count":
          k *= (double)va.crew_count;
          break;

        case "inverse":
          k = k > double.Epsilon ? 1.0 / k : 0.0;
          break;

        default:
          k *= sim.resource(mod).amount;
          break;
      }
    }
    return k;
  }
}


} // KERBALISM