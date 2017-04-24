using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


  public class UIData
  {
    public UIData()
    {
      popout_window_left = 280;
      popout_window_top = 100;
      mapviewed = false;
    }

    public UIData(ConfigNode node)
    {
      popout_window_left = Lib.ConfigValue(node, "popout_window_left", 280);
      popout_window_top = Lib.ConfigValue(node, "popout_window_top", 100);
      mapviewed = Lib.ConfigValue(node, "mapviewed", false);
    }

    public void save(ConfigNode node)
    {
      node.AddValue("popout_window_left", popout_window_left);
      node.AddValue("popout_window_top", popout_window_top);
      node.AddValue("mapviewed", mapviewed);
    }

    public float popout_window_left;       // popout window position left
    public float popout_window_top;        // popout window position top
    public bool  mapviewed;                // has the user entered map-view/tracking-station
  }


} // KERBALISM
