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
    public bool isPlaying;

    public override void Start()
    {
      thisModule = "LadderDeploy";
      ladder = part.FindModuleImplementing<RetractableLadder>();
      if (ladder != null)
      {
        ladder.isEnabled = false;
      }
    }

    public override void Update()
    {
      base.Update();

      // Update GUI
      Events["RetractLadder"].guiActive = Events["RetractLadder"].guiActiveUnfocused = (targetState != "Retracted" && hasEC && isPlaying);
      Events["ExtendLadder"].guiActive = Events["ExtendLadder"].guiActiveUnfocused = (targetState == "Retracted" && hasEC && isPlaying);
    }

    public override bool IsActive
    {
      get
      {
        if (ladder != null)
        {
          if (!Features.Deploy)
          {
            ladder.isEnabled = true;
            return false;
          }

          if (targetState == "") targetState = ladder.StateName;

          if (targetState == ladder.StateName)
          {
            isPlaying = false;
            actualECCost = 0;
          }
          else actualECCost = ecDeploy;
        }
        return isPlaying;
      }
    }
  }
}
