// ===================================================================================================================
// Utility functions to interact with RemoteTech
// ===================================================================================================================


using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


namespace KERBALISM
{

  public static class RemoteTech
  {
    // store initialized flag, for lazy initialization
    static bool initialized = false;

    // cache the result of the check here
    static bool enabled = false;

    // check if RemoteTech is loaded
    public static bool isEnabled()
    {
      // search for compatible SCANsat assembly
      if (!initialized)
      {
        foreach (var a in AssemblyLoader.loadedAssemblies)
        {
          if (a.name == "RemoteTech")
          {
            enabled = true;
            break;
          }
        }
        initialized = true;
      }
      return enabled;
    }

    public static bool isRTAntenna(PartModule module) {
      return module.part.Modules.Contains("ModuleRTAntenna");
    }
  }

} // KERBALISM