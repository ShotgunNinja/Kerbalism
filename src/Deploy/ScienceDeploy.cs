﻿namespace KERBALISM
{
  public class ScienceDeploy : DeployBase
  {
    // List of target
    // Part name: 
    //    OrbitalScanner
    //    SurveyScanner
    //    InfraredTelescope
    //    GooExperiment
    //    science_module (science.module)
    public override bool GetisConsuming
    {
      get
      {
        return false;
      }
    }
  }
}
