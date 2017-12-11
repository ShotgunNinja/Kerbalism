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
      if (hasEC)
      {
        if (!isPlaying && amountECleft > ecDeploy)
        {
          this.Events["RetractLadder"].guiActive = targetState != "Retracted";
          this.Events["RetractLadder"].guiActiveUnfocused = targetState != "Retracted";

          this.Events["ExtendLadder"].guiActive = targetState == "Retracted";
          this.Events["ExtendLadder"].guiActiveUnfocused = targetState == "Retracted";
          return;
        }
      }
      this.Events["RetractLadder"].guiActive = false;
      this.Events["RetractLadder"].guiActiveUnfocused = false;
      this.Events["ExtendLadder"].guiActive = false;
      this.Events["ExtendLadder"].guiActiveUnfocused = false;
    }

    public override bool GetIsActive()
    {
      if (!Features.Deploy)
      {
        if (ladder != null) ladder.isEnabled = true;
        return false;
      }

      if (targetState == "") targetState = ladder.StateName;

      if (targetState == ladder.StateName)
      {
        isPlaying = false;
        actualECCost = 0;
      }
      else actualECCost = ecDeploy;
      return isPlaying;
    }
  }
}
