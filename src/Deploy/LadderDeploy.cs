namespace KERBALISM
{
  public class LadderDeploy : DeployBase
  {
    RetractableLadder ladder;

    // I have to replaced the Events because, I cannot access the Animation values
    [KSPEvent(guiActive = false, guiName = "#autoLOC_6001411", active = true, guiActiveUnfocused = true, unfocusedRange = 4f)]
    public void ExtendLadder()
    {
      ladder.Extend();
      targetState = "Extended";
      isPlaying = true;
    }

    [KSPEvent(guiActive = false, guiName = "#autoLOC_6001412", active = true, guiActiveUnfocused = true, unfocusedRange = 4f)]
    private void RetractLadder()
    {
      ladder.Retract();
      targetState = "Retracted";
      isPlaying = true;
    }

    // Controllers to know when the animation is playing
    public string targetState = "";
    bool isPlaying;

    [KSPField(guiName = "Status", guiUnits = "", guiActive = false, guiFormat = "")]
    string moving = "Moving";

    public override void OnStart(StartState state)
    {
      if (state == StartState.Editor && state == StartState.None && state == StartState.PreLaunch) return;

      ladder = part.FindModuleImplementing<RetractableLadder>();
      if (ladder != null)
      {
        ladder.Events["Retract"].guiActive = ladder.Events["Retract"].guiActiveUnfocused = false;
        ladder.Events["Extend"].guiActive = ladder.Events["Extend"].guiActiveUnfocused = false;
        
        pModule = ladder;
        base.OnStart(state);
      }
    }

    public override bool GetisConsuming
    {
      get
      {
        // Just making sure that we have the target module
        if (ladder == null) return false;

        // Update GUI
        Events["RetractLadder"].guiActive = Events["RetractLadder"].guiActiveUnfocused = (targetState != "Retracted" && hasEC && !isPlaying);
        Events["ExtendLadder"].guiActive = Events["ExtendLadder"].guiActiveUnfocused = (targetState == "Retracted" && hasEC && !isPlaying);
        Fields["moving"].guiActive = isPlaying;

        ToggleActions(ladder, hasEC);

        if (targetState == "") targetState = ladder.StateName;

        if (targetState == ladder.StateName)
        {
          isPlaying = false;
          return false;
        }

        actualECCost = ecDeploy;
        return isPlaying;
      }
    }
  }
}
