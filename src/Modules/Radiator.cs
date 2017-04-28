using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KERBALISM
{
  class Radiator : PartModule, IModuleInfo
  {
    // Note on radiator_type :
    // frontal : the radiator is assumed to face the part -transform.forward vector
    // radial : the radiator is assumed to face both the part transform.right & -transform.right vectors. Effective surface = surface * 2
    // deployable : autodetected if there is a ModuleDeployableRadiator and isTracking = false. Effective surface = surface * 2
    // tracking : autodetected if there is a ModuleDeployableRadiator and isTracking = true. Effective surface = surface * 2

    // config
    [KSPField] public string input_resource = "ElectricCharge"; // resource consumed to make the pump work
    [KSPField] public float input_rate_min = 0.005f;            // input per unit of coolant produced at min temperature, per second
    [KSPField] public float input_rate_max = 0.500f;            // input per unit of coolant produced at max temperature, per second
    [KSPField] public string output_resource = "Coolant";       // name of the coolant output resource  
    [KSPField] public float temperature_min = 290.0f;           // tweakable minimal coolant temperature
    [KSPField] public float temperature_max = 340.0f;           // tweakable maximal coolant temperature
    [KSPField] public double emissivity = 1.0;                  // optional factor on emissivity, affect output rate

    // persistance/config
    [KSPField(isPersistant = true)] public string radiator_type = string.Empty;  // must be specified for non-deployable radiators, valid values : frontal, radial
    [KSPField(isPersistant = true)] public double surface = 0.0;                 // surface of a single face of the radiator, autodetected but can be defined in cfg
    [KSPField(isPersistant = true)] public Vector3 direction;                    // direction the radiator is facing, autodetected but can be defined in cfg

    // persistence
    [KSPField(isPersistant = true)] public bool running;

    // rmb status
    [KSPField(guiActive = true, guiActiveEditor = true, guiName = "_", guiFormat = "F2", guiUnits = "/s")] public double cooling_rate;
    [KSPField(guiActive = true, guiActiveEditor = true, guiName = "_", guiFormat = "F2", guiUnits = "/s")] public double input_rate;

    // rmb tweakable
    [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Coolant temperature", guiFormat = "F0")]
    public float coolant_temperature = -1.0f;       // coolant temperature in K, higher = more efficient
    
    // other data
    private ModuleActiveRadiator radiator;
    private ModuleDeployableRadiator deploy_module;

    public void Start()
    {
      // setup temperature tweakable
      if (coolant_temperature < temperature_min) coolant_temperature = temperature_min;
      UI_FloatRange floatui = new UI_FloatRange();
      floatui.minValue = temperature_min;
      floatui.maxValue = temperature_max;
      floatui.stepIncrement = 5.0f;
      Fields["coolant_temperature"].uiControlEditor = floatui;
      Fields["coolant_temperature"].uiControlFlight = floatui;

      // setup ui
      PartResourceDefinition outputdef = PartResourceLibrary.Instance.GetDefinition(output_resource);
      PartResourceDefinition inputdef = PartResourceLibrary.Instance.GetDefinition(input_resource);
      Fields["cooling_rate"].guiName = outputdef.title + " output: ";
      Fields["input_rate"].guiName = inputdef.title + " input: ";
      
      // get stock radiator modules
      radiator = part.Modules.GetModule<ModuleActiveRadiator>();
      deploy_module = part.Modules.GetModule<ModuleDeployableRadiator>();

      // disable if ModuleActiveRadiator is missing
      if (radiator != null) running = false;

      // disable if type is not frontal or radial and there is no ModuleDeployableRadiator
      if (deploy_module == null && radiator_type != "frontal" && radiator_type != "radial")
      {
        radiator = null;
        running = false;
      }

      // autodetect type
      if (deploy_module != null)
      {
        radiator_type = deploy_module.isTracking ? "tracking" : "deployable";
      }

      // autodetect direction if necessary
      if (direction.magnitude < float.Epsilon)
      {
        switch (radiator_type)
        {
          case "frontal":     direction = -part.partTransform.forward; break;
          case "radial":      direction = part.partTransform.right; break;
          case "tracking":    direction = part.FindModelComponent<Transform>(deploy_module.pivotName).up; break;
          case "deployable":  direction = part.FindModelComponent<Transform>(deploy_module.pivotName).right; break;
        }
      }

      // calculate surface if necessary
      if (surface < double.Epsilon)
      {
        if (!CalculateSurface())
        {
          running = false;
          radiator = null;
        }
      }

      // add coolant resource capacity to part
      if (!part.Resources.Contains(output_resource))
      {
        Lib.AddResource(part, output_resource, 0.0, surface * 5.0);
      }
    }

    public void Update()
    {
      // get running state from ModuleActiveRadiator state
      running = (radiator != null) ? radiator.IsCooling : false;
      // fix for ModuleActiveRadiator.IsCooling not reflecting ModuleDeployableRadiator state in the editor
      if (Lib.IsEditor() && deploy_module != null)
      {
        running = deploy_module.deployState == ModuleDeployablePart.DeployState.EXTENDED;
      }
      
      // update flow mode of resource
      // note: this has to be done constantly to prevent the user from changing it
      Lib.SetResourceFlow(part, output_resource, running);
      // when disabled remove stored coolant
      if (!running && part.Resources.Get(output_resource).amount > 0.0)
      {
        Lib.RemoveResource(part, output_resource, surface * 5.0, 0.0);
      }
    }

    public void FixedUpdate()
    {
      if (!running || !Lib.IsFlight()) { return; }

      // get vessel info from cache
      vessel_info vi = Cache.VesselInfo(vessel);

      // calculate net flux (W)
      cooling_rate = GetRadiatorFlux(
        (vessel.mainBody.position - Lib.VesselPosition(vessel)).normalized, // body_dir
        vi.sun_dir, // sun_dir
        GetFacingDirectionLoaded(), // radiator_dir
        vi.body_flux, // body_flux
        vi.albedo_flux, // albedo_flux
        vi.solar_flux, // solar_flux
        vi.temperature, // env_temperature
        vessel.mainBody.GetPressure(Math.Max(vessel.altitude, 0.0)), // env_pressure
        surface, 
        radiator_type, 
        emissivity, 
        coolant_temperature);

      // calculate input rate
      input_rate = GetInputRate(
        cooling_rate,
        coolant_temperature,
        temperature_min,
        temperature_max,
        input_rate_min,
        input_rate_max);

      resource_recipe recipe = new resource_recipe();
      // consume input at fixed rate
      recipe.Input(input_resource, input_rate * Kerbalism.elapsed_s);
      // produce coolant (1 unit/s = 1kW)
      recipe.Output(output_resource, cooling_rate * Kerbalism.elapsed_s, false);

      // get resource cache
      vessel_resources resources = ResourceCache.Get(vessel);
      resources.Transform(recipe);
    }


    public static void BackgroundUpdate(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, Radiator radiator, 
                                        vessel_info info, vessel_resources resources, double elapsed_s)
    {
      if (!Lib.Proto.GetBool(m, "running")) return;

      // get facing direction
      Vector3d facing_direction = Lib.Proto.GetVector3(m, "direction");
      // if tracking, assume perfect alignement toward the sun and calculate direction constrained to pivot axis
      if (Lib.Proto.GetString(m, "radiator_type") == "tracking")
      {
        facing_direction = Vector3d.Cross(info.sun_dir, v.transform.rotation * p.rotation * facing_direction).normalized; // UNTESTED
      }
      else
      {
        facing_direction = (v.transform.rotation * p.rotation * facing_direction).normalized;
      }

      // get radiator surface
      double surface = Lib.Proto.GetDouble(m, "surface");

      // calculate net flux (W)
      double cooling_rate = GetRadiatorFlux(
        (v.mainBody.position - Lib.VesselPosition(v)).normalized, // body_dir
        info.sun_dir, // sun_dir
        facing_direction, // radiator_dir
        info.body_flux, // body_flux
        info.albedo_flux, // albedo_flux
        info.solar_flux, // solar_flux
        info.temperature, // env_temperature
        v.mainBody.GetPressure(Math.Max(v.altitude, 0.0)), // env_pressure
        surface,
        Lib.Proto.GetString(m, "radiator_type"),
        radiator.emissivity,
        radiator.coolant_temperature);

      // calculate input rate
      double input_rate = GetInputRate(
        cooling_rate,
        Lib.Proto.GetFloat(m, "coolant_temperature"),
        radiator.temperature_min,
        radiator.temperature_max,
        radiator.input_rate_min,
        radiator.input_rate_max);

      resource_recipe recipe = new resource_recipe();
      // consume input at fixed rate
      recipe.Input(radiator.input_resource, input_rate * elapsed_s);
      // produce coolant (1 unit/s = 1kW)
      recipe.Output(radiator.output_resource, cooling_rate * elapsed_s, false);
      resources.Transform(recipe);
    }

    public static double GetRadiatorFlux
      (
      Vector3d body_dir,
      Vector3d sun_dir,
      Vector3d radiator_dir,
      double body_flux,
      double albedo_flux,
      double solar_flux,
      double env_temperature,
      double env_pressure,
      double surface,
      string type,
      double emissivity,
      double coolant_temperature
      )
    {
      // calculate cosine factors
      // - frontal : flux is accounted only for the 180° angle the radiator is facing
      // - deployable/edge : flux can affect both radiator faces
      double sun_cosine_factor = Vector3d.Dot(sun_dir, radiator_dir);
      double body_cosine_factor = Vector3d.Dot(body_dir, radiator_dir);

      switch (type)
      {
        case "frontal":
          sun_cosine_factor = Math.Max(sun_cosine_factor, 0.0);
          body_cosine_factor = Math.Max(body_cosine_factor, 0.0);
          break;
        case "radial":
        case "deployable":
        case "tracking":
          sun_cosine_factor = Math.Abs(sun_cosine_factor);
          body_cosine_factor = Math.Abs(body_cosine_factor);
          break;
      }

      // calculate sun flux (W)
      double sun_watts = sun_cosine_factor * solar_flux * surface;

      // calculate body flux (W)
      double body_watts = body_cosine_factor * (albedo_flux + body_flux) * surface;

      // calculate radiative flux (W)
      double sides = (type == "frontal") ? 1.0 : 2.0;
      double radiative_watts = -(PhysicsGlobals.StefanBoltzmanConstant * emissivity * surface * sides * Math.Pow(coolant_temperature, 4.0));

      // very approximate conductive/convective heat transfer when in atmo (W)
      // Assumption : heat transfer coeficient at 100 kPa is 50 W/m²/K = 0.5 W/m²/K/kPA
      double conductive_watts = surface * (env_temperature - coolant_temperature) * env_pressure * 0.5;

      // calculate net flux (W)
      double net_watts = sun_watts + body_watts + radiative_watts + conductive_watts;

      // return cooling flux (kW)
      return net_watts < 0.0 ? Math.Abs(net_watts) * 0.001 : 0.0;
    }

    public static float GetInputRate(double cooling_rate, float coolant_temperature, float temperature_min, float temperature_max, float input_rate_min, float input_rate_max)
    {
      float a = (input_rate_max - input_rate_min) / (temperature_max - temperature_min);
      float b = input_rate_min - (a * temperature_min);
      return ((a * coolant_temperature) + b) * (float) cooling_rate;
    }

    public Vector3d GetFacingDirectionLoaded(Vector3d sun_dir = default(Vector3d))
    {
      Vector3d dir =
        radiator_type == "tracking" ?
        part.FindModelComponent<Transform>(deploy_module.pivotName).right :
        part.transform.rotation * direction;
      // assume direction is aligned to the sun in the editor
      if (Lib.IsEditor() && radiator_type == "tracking")
      {
        dir = Vector3d.Cross(sun_dir, dir).normalized;
      }
      return dir;
    }

    public bool CalculateSurface()
    {
      float[] dim = new float[3];

      // deployable radiator : get surface from deployed dragcube
      if (deploy_module != null)
      {
        DragCube cube = part.DragCubes.Cubes.Find(p => p.Name.Contains("EXTENDED"));
        if (cube == null) { return false; }
        dim[0] = cube.Size.x;
        dim[1] = cube.Size.y;
        dim[2] = cube.Size.z;
      }
      // fixed radiator : get surface from renderer bounds
      else
      {
        Bounds bb = part.GetPartRendererBound();
        dim[0] = bb.extents.x * 2.0f;
        dim[1] = bb.extents.y * 2.0f;
        dim[2] = bb.extents.z * 2.0f;
      }
      Array.Sort(dim);
      surface = dim[1] * dim[2];

      return true;
    }

    public double GetMaxVoidOutput(double temp)
    {
      double sides = (radiator_type == "frontal") ? 1.0 : 2.0;
      return PhysicsGlobals.StefanBoltzmanConstant * emissivity * surface * sides * Math.Pow(temp, 4.0) * 0.001;
    }

    // part tooltip
    public override string GetInfo()
    {
      // calculate surface if necessary
      if (surface < double.Epsilon)
      {
        // get stock radiator modules
        radiator = part.Modules.GetModule<ModuleActiveRadiator>();
        deploy_module = part.Modules.GetModule<ModuleDeployableRadiator>();
        if (radiator != null) CalculateSurface();
      }

      PartResourceDefinition outputdef = PartResourceLibrary.Instance.GetDefinition(output_resource);
      PartResourceDefinition inputdef = PartResourceLibrary.Instance.GetDefinition(input_resource);

      string info = string.Empty;
      info += "<b>Active surface:</b> " + Lib.HumanReadableSurface(surface * (radiator_type == "frontal" ? 1.0 : 2.0)) + "\n";
      info += "\n";
      info += "<b>Min coolant temp:</b> " + Lib.HumanReadableTemp(temperature_min) + " :\n";
      info += outputdef.title + ": <color=#00ff00>" + Lib.HumanReadableRate(GetMaxVoidOutput(temperature_min)) + "</color>\n";
      info += inputdef.title + ": <color=#ff0000>" + Lib.HumanReadableRate(input_rate_min * GetMaxVoidOutput(temperature_min)) + "</color>\n";
      info += "\n";
      info += "<b>Max coolant temp:</b> " + Lib.HumanReadableTemp(temperature_max) + " :\n";
      info += outputdef.title + ": <color=#00ff00>" + Lib.HumanReadableRate(GetMaxVoidOutput(temperature_max)) + "</color>\n";
      info += inputdef.title + ": <color=#ff0000>" + Lib.HumanReadableRate(input_rate_max * GetMaxVoidOutput(temperature_max)) + "</color>\n";
      return info;
    }

    // module info support
    public string GetModuleTitle() { return "Coolant radiator"; }
    public string GetPrimaryField() { return string.Empty; }
    public Callback<Rect> GetDrawModulePanelCallback() { return null; }
  }
}
