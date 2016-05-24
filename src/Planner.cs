// ====================================================================================================================
// the vessel planner
// ====================================================================================================================


using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using ModuleWheels;
using UnityEngine;


namespace KERBALISM {

public class Planner
{
  public class environment_data
  {
    public CelestialBody body;                            // target body
    public double altitude;                               // target altitude
    public bool landed;                                   // true if landed
    public bool breathable;                               // true if inside breathable atmosphere
    public double sun_dist;                               // distance from the sun
    public double sun_flux;                               // flux received from the sun
    public double body_flux;                              // flux received from albedo radiation of the body, in direction of the sun
    public double body_back_flux;                         // flux received from albedo radiation of the body, in direction opposite to the sun
    public double background_temp;                        // space background temperature
    public double sun_temp;                               // temperature of a blackbody emitting sun flux
    public double body_temp;                              // temperature of a blackbody emitting body flux
    public double body_back_temp;                         // temperature of a blackbody emitting body back flux
    public double light_temp;                             // temperature at sunlight, if outside atmosphere
    public double shadow_temp;                            // temperature in shadow, if outside atmosphere
    public double atmo_temp;                              // temperature inside atmosphere, if any
    public double orbital_period;                         // length of orbit
    public double shadow_period;                          // length of orbit in shadow
    public double shadow_time;                            // proportion of orbit that is in shadow
    public double temp_diff;                              // average difference from survival temperature
    public double atmo_factor;                            // proportion of sun flux not absorbed by the atmosphere
  }

  public class crew_data
  {
    public uint count;                                    // number of crew on board
    public uint capacity;                                 // crew capacity of the vessel
    public bool engineer;                                 // true if an engineer is on board
  }

  public class ec_data
  {
    public double storage;                                // ec stored
    public double consumed;                               // ec consumed
    public double generated_sunlight;                     // ec generated in sunlight
    public double generated_shadow;                       // ec generated in shadow
    public double life_expectancy_sunlight;               // time-to-death for lack of climatization in sunlight
    public double life_expectancy_shadow;                 // time-to-death for lack of climatization in shadow
    public double best_ec_generator;                      // rate of best generator (for redundancy calculation)
  }

  public class supply_data
  {
    public double storage;                                // amount of supply
    public double consumed;                               // supply consumed per-second
    public double produced;                               // supply recycled/produced per-second
    public double tta;                                    // best time-to-harvest among all greenhouses, in seconds
    public double life_expectancy;                        // time-to-death for lack of supply
    public bool   has_greenhouse;                         // true if it has a greenhouse with same resource
    public bool   has_recycler;                           // true if it has a scrubber/recycler with same resource
  }

  public class qol_data
  {
    public double living_space;                           // living space per-crew
    public double entertainment = 1.0;                    // store multiplication of all entertainment from parts
    public string factors;                                // description of other quality-of-life factors
    public double bonus;                                  // modifier applied to rules with qol modifier keyword
    public double time_to_instability;                    // time-to-instability for stress
  }

  public class radiation_data
  {
    public double shielding_amount;                       // capacity of radiation shielding on the vessel
    public double shielding_capacity;                     // amount of radiation shielding on the vessel
    public double[] life_expectancy;                      // time-to-death or time-to-safemode for radiations (cosmic/storm/belt levels)
  }

  public class reliability_data
  {
    public double quality;                                // manufacturing quality
    public double failure_year;                           // estimated failures per-year, averaged per-component
    public string redundancy;                             // verbose description of redundancies
  }

  public class signal_data
  {
    public double ecc;                                    // error correcting code efficiency
    public double range;                                  // range of best antenna, if any
    public double transmission_cost_min;                  // min data transmission cost of best antenna, if any
    public double transmission_cost_max;                  // max data transmission cost of best antenna, if any
    public double relay_range;                            // range of best relay antenna, if any
    public double relay_cost;                             // relay cost of best relay antenna, if any
    public double second_best_range;                      // range of second-best antenna (for reliability calculation)
  }

  public class resource_data // replacement for supply data
  {
    public double amount;                                  // initial resource supply.
    public double capacity;                                // maximum amount of resources that can be stored.
    public double draw;                                    // resources drained per second by rules and Kerbalism modules, when available.
    public double consumed;                                // rate at which resources are being consumed, per second.
    public double produced;                                // rate at which resources are being produced, per second.

    public bool hasScrubber;
    public bool hasRecycler;
    public bool hasGreenhouse;
    public double harvestTime;
    public double harvestSize;

    public List<string> supplyRequires;

    // Set overrideable defaults passed to constructor
    public resource_data (
      double amount = 0.0, 
      double capacity = 0.0, 
      double draw = 0.0,
      double consumed = 0.0,
      double produced = 0.0,
      bool hasScrubber = false,
      bool hasRecycler = false,
      bool hasGreenhouse = false,
      double harvestTime = 0.0,
      double harvestSize = 0.0)
    {
      this.amount = amount;
      this.capacity = capacity;
      this.draw = draw;
      this.consumed = consumed;
      this.produced = produced;
      this.hasScrubber = hasScrubber;
      this.hasRecycler = hasRecycler;
      this.hasGreenhouse = hasGreenhouse;
      this.harvestTime = harvestTime;
      this.harvestSize = harvestSize;
      this.supplyRequires = new List<string>();
    }

    // Resource combination constructor
    public resource_data (resource_data data1, resource_data data2)
    {
      amount = data1.amount + data2.amount;
      capacity = data1.capacity + data2.capacity;
      draw = data1.draw + data2.draw;
      consumed = data1.consumed + data2.consumed;
      produced = data1.produced + data2.produced;
      hasScrubber = data1.hasScrubber || data2.hasScrubber;
      hasRecycler = data1.hasRecycler || data2.hasRecycler;
      hasGreenhouse = data1.hasGreenhouse || data2.hasGreenhouse;
      harvestTime = Math.Max(data1.harvestTime, data2.harvestTime);
      harvestSize = Math.Max(data1.harvestSize, data2.harvestSize);
      this.supplyRequires = data1.supplyRequires.Union(data2.supplyRequires).ToList();
    }

    // Sum values of two sets of resource_data
    public static resource_data operator+(resource_data data1, resource_data data2){
      resource_data dataSum = new resource_data(data1, data2);
      return dataSum;
    }

    // Calculate and return time in seconds until resource is empty at *current* use rates. Return null if resource will last indefinitely.
    // This function does not run the full resource model, and should not be used to set values displayed in planner.
    // Primarily used in sorting resources by time until they are at zero.
    public double? timeToEmpty(){
      double? timeLeft = null;
      if ((consumed - produced) > Double.Epsilon) timeLeft = amount / (consumed - produced); //If resource delta is negative, calculate time remaining until resource is exhausted. 
      return timeLeft;
    }

    public double lifeTime(resource_collection resources, int n = 0){
      double timeLeft = 0.0;
      if (n > 3) return timeLeft; //If function has stepped through 4 resources each requiring the next, assume we're caught in a loop and break out.
      if ((consumed - produced) > Double.Epsilon) timeLeft = amount / (consumed - produced); //If resource delta is negative, calculate time remaining until resource is exhausted. 
      if (supplyRequires.Count > 0) {
        timeLeft = amount / consumed; // Lifetime sans production, once dependency runs out.
        double dependentTime = double.MaxValue;
        foreach (string dependency in supplyRequires) {
          dependentTime = Math.Min(dependentTime, resources[dependency].lifeTime(resources, n+1));
        }
        timeLeft += dependentTime;
      }
      return timeLeft;
    }
  }

  // resource_data dictionary wrapper
  public class resource_collection : IEnumerable
  {
    public Dictionary<string, resource_data> resourcesByName;

    public resource_collection() { resourcesByName = new Dictionary<string, resource_data>(); }

    // IEnumerable Member
    public IEnumerator GetEnumerator() {
      foreach (KeyValuePair<string, resource_data> kv in resourcesByName) yield return kv;
    }

    // Get resource_data by Name
    public resource_data this[string name]{
      get {
        return resourcesByName [name];
      }
    }

    // Add provided resource data to named resource
    public void Add (string name, resource_data r_data){
      if(resourcesByName.ContainsKey(name)){
        resourcesByName [name] += r_data;
      } else {
        resourcesByName.Add (name, r_data);
      }
    }

    //Add new empty resource by name if resource is not currently in dictionary
    public void Add (string name){
      if(!resourcesByName.ContainsKey(name)){
        resourcesByName.Add (name, new resource_data());
      }
    }

    public void ApplyConsumption(resource_collection consumption){
      foreach (KeyValuePair<string, resource_data> kv in consumption) {
        if (resourcesByName.ContainsKey(kv.Key)) {
          this[kv.Key].consumed += kv.Value.consumed;
          this[kv.Key].draw -= kv.Value.draw;
        }
      }
    }

    public void ApplyProduction(resource_collection production){      
      foreach (KeyValuePair<string, resource_data> kv in production) {
        if (resourcesByName.ContainsKey(kv.Key)) this[kv.Key].produced += kv.Value.produced;
      }
    }

    public double Availability (string name) {
      if (!resourcesByName.ContainsKey(name)) return 0.0;     //Resource not in dictionary
      if (this[name].amount > 0.0) return 1.0;     //Resource has supply available, regardless of resupply rate.
      if (this[name].draw <= 0.0) return 0.0;
      return Math.Max(Math.Min((this[name].produced - this[name].consumed) / this[name].draw, 1.0), 0.0);
    }

    // Get name of the next non-empty resource that will run empty at the current usage rates
    public string NextToEmpty(){
      List<KeyValuePair<string, resource_data>> resourceList = resourcesByName.ToList ();
      //Remove list items that have an indefinite time remaining or no time remaining, then sort.
      resourceList = resourceList.FindAll(rkv => rkv.Value.timeToEmpty() != 0.0 && rkv.Value.timeToEmpty() != null );
      resourceList.Sort(new SortResourcesByTime());
      return resourceList.First().Key;
    }

    // Combine resource_collections appropriately with + operator
    public static resource_collection operator+(resource_collection coll1, resource_collection coll2){
      resource_collection collSum = new resource_collection ();
      foreach (KeyValuePair<string, resource_data> kv in coll1) collSum.Add (kv.Key, kv.Value);
      foreach (KeyValuePair<string, resource_data> kv in coll2) collSum.Add (kv.Key, kv.Value);
      return collSum;
    }

    // Get resource dictionary as a List (for sorting, and such.)
    public List<KeyValuePair<string, resource_data>> ToList (){
      return resourcesByName.ToList ();
    }

    //Get IComparer for sorting resources in order of remaining time until empty
    public static IComparer<KeyValuePair<string, resource_data>> GetTimeLeftComparer(){
      return new SortResourcesByTime();
    }

    // Sort Comparer: compare name/resource_data KeyValuePairs by least time remaining until resource runs empty
    private class SortResourcesByTime : IComparer<KeyValuePair<string, resource_data>> {
      public int Compare(KeyValuePair<string, resource_data> a, KeyValuePair<string, resource_data> b){
        double? aTime = a.Value.timeToEmpty(); //Nullable: Null == Indefinite time until resource is empty
        double? bTime = b.Value.timeToEmpty(); //Nullable: Null == Indefinite time until resource is empty

        // If neither aTime nor bTime is null/indefinite, then simply compare their values as normal
        if (aTime != null && bTime != null) return aTime.Value.CompareTo (bTime.Value);

        // Either aTime or bTime is null/indefinite (or both)...
        if (aTime != null) return -1;     //If bTime is null/indefinite and aTime is not, aTime (clearly) comes sooner
        if (bTime != null) return 1;      //If aTime is null/indefinite and bTime is not, bTime (clearly) comes sooner
        return 0;                         //aTime and bTime are both null/indefinite, and therefore equivalent.
      }
    }
  }

  public class resource_transaction 
  { 
    public object target;                                // Rule, Scrubber, Recycler, or Greenhouse object to be applied when this transaction is processed.
    public string type;                                  // Type of module/rule set as target. ("Rule", "Scrubber", etc.)
    public double baseRate;                              // Base rate of resource effects when transaction runs (set from Rule and Converter rates, with environmental modifiers.)
    public List<string> inputs;                          // Resources that are consumed in this transaction.
    public List<string> output;                          // Resources produced in this transaction.
    public bool hasRun;                                  // Flagged as true when this transaction has been processed.
    public bool hasSupply = false;                       // If all input resources have sufficient supply, set to true, and transaction runs regardless of blockingTransactions.

    List<resource_transaction> blockingTransactions;     // Resource transactions affecting supply resource(s), which should run first, if possible.

    public resource_transaction() {
      blockingTransactions = new List<resource_transaction>();
      inputs = new List<string>();
      output = new List<string>();
    }

    // Run this transaction on a given resource_collection, return a collection (delta) with resource values after the transaction.
    public resource_collection Run(resource_collection resources, environment_data env) {
      resource_collection delta = new resource_collection();
      foreach (KeyValuePair<string, resource_data> kv in resources) {
        delta.Add(kv.Key); //Add placeholder resources
      }

      if (type == "Rule") {
        Rule rule = (Rule)target;
        double rateMod = resources.Availability(rule.resource_name);
        if (!String.IsNullOrEmpty(rule.resource_name)) {
          delta[rule.resource_name].consumed += baseRate * rateMod; 
          delta[rule.resource_name].draw -= baseRate; 
        }
        if (!String.IsNullOrEmpty(rule.waste_name)) delta[rule.waste_name].produced += baseRate * rateMod * rule.waste_ratio;
      } else if (type == "Scrubber") {
        Scrubber scrubber = (Scrubber)target;
        if (!env.breathable) {
          double rateMod = resources.Availability(scrubber.waste_name);
          delta[scrubber.waste_name].consumed += rateMod * baseRate;
          delta[scrubber.waste_name].draw -= baseRate;
          delta[scrubber.resource_name].produced += rateMod * baseRate * Scrubber.DeduceEfficiency();
          delta[scrubber.resource_name].hasScrubber = true;
        } else {
          delta[scrubber.resource_name].produced += scrubber.intake_rate;
          delta[scrubber.resource_name].hasScrubber = true;
        }
      } else if (type == "Recycler") {
        Recycler recycler = (Recycler)target;
        double rateMod = resources.Availability(recycler.waste_name);
        if(!String.IsNullOrEmpty(recycler.filter_name)) rateMod = Math.Min(rateMod, resources.Availability(recycler.filter_name));
        delta[recycler.waste_name].consumed += rateMod * baseRate;
        delta[recycler.waste_name].draw -= baseRate;
        delta[recycler.resource_name].produced += rateMod * baseRate * recycler.waste_ratio;
        delta[recycler.resource_name].hasRecycler = true;
        if (!String.IsNullOrEmpty(recycler.filter_name)) {
          delta[recycler.filter_name].consumed += rateMod * recycler.filter_rate;
          delta[recycler.filter_name].draw -= recycler.filter_rate;
        }
      } else if (type == "Greenhouse") {
        Greenhouse greenhouse = (Greenhouse)target;
        double rateMod = 1.0;
        if(!String.IsNullOrEmpty(greenhouse.input_name)) rateMod = resources.Availability(greenhouse.input_name);
        double wastePerc = resources.Availability(greenhouse.waste_name);
        double natural_lighting = Greenhouse.NaturalLighting(env.sun_dist);
        double lighting = (greenhouse.door_opened ? natural_lighting : 0.0) + greenhouse.lamps;
        double growth_bonus = (env.landed ? greenhouse.soil_bonus : 0.0) + (greenhouse.waste_bonus * wastePerc);
        double growth_factor = (greenhouse.growth_rate * (1.0 + growth_bonus)) * lighting;
        if(growth_factor != 0.0){
          double cycleTime = rateMod / growth_factor;
          delta[greenhouse.waste_name].consumed += wastePerc * greenhouse.waste_rate;
          delta[greenhouse.waste_name].draw -= greenhouse.waste_rate;
          delta[greenhouse.resource_name].produced += Math.Min(greenhouse.harvest_size, resources[greenhouse.resource_name].capacity) / cycleTime;
          delta[greenhouse.resource_name].hasGreenhouse = true;
          delta[greenhouse.resource_name].harvestTime = cycleTime;
          delta[greenhouse.resource_name].harvestSize = Math.Min(greenhouse.harvest_size, resources[greenhouse.resource_name].capacity);
          if (!String.IsNullOrEmpty(greenhouse.input_name)) {
            delta[greenhouse.input_name].consumed += rateMod * greenhouse.input_rate;
            delta[greenhouse.input_name].draw -= greenhouse.input_rate;
          }
        }
      }
      hasRun = true;
      return delta;
    }

    public bool hasSomeSupply(resource_collection resourcePool){
      if (type == "Rule") {
        Rule rule = (Rule)target;
        if (resourcePool.Availability(rule.resource_name) == 0.0) return false;
      } else if (type == "Scrubber") {
        Scrubber scrubber = (Scrubber)target;
        if (resourcePool.Availability(scrubber.waste_name) == 0.0) return false;
      } else if (type == "Recycler") {
        Recycler recycler = (Recycler)target;
        if (resourcePool.Availability(recycler.waste_name) == 0.0) return false;
        if (!String.IsNullOrEmpty(recycler.filter_name) && resourcePool.Availability(recycler.filter_name) == 0.0) return false;
      } else if (type == "Greenhouse") {
        Greenhouse greenhouse = (Greenhouse)target;
        if (!String.IsNullOrEmpty(greenhouse.input_name) && resourcePool.Availability(greenhouse.input_name) == 0.0) return false;
      }
      return true;
    }

    // Check if this transaction should run.
    public bool ShouldRun(){ 
      if (hasSupply) return true; // If we have sufficient supply to meet all input resource needs, there's no need to wait on blockingTransactions.
      foreach (resource_transaction rt in blockingTransactions) if (!rt.hasRun) return false; // If any blockingTransaction has not run, this should not run.
      return true; 
    } 

    public void SetBlocking(List<resource_transaction> transactions){
      foreach(resource_transaction rt in transactions){
        // A transform should not block itself, even if it has an input and output resource in common.
        if (rt != this){
          foreach (string input in inputs) {
            // If an output in rt matches an input in this transaction and the blocking list does not already contain rt, add it.
            if (rt.output.Contains(input) && !blockingTransactions.Contains(rt)) { blockingTransactions.Add(rt); }
          }
        }
      }
    }
  }

  // rate throttling
  float lastUpdateTime = 0;
  float timeBetweenUpdates = 0.2f; //4 seconds between background updates
  bool updateNeeded;

  // styles
  GUIStyle leftmenu_style;
  GUIStyle midmenu_style;
  GUIStyle rightmenu_style;
  GUIStyle row_style;
  GUIStyle title_style;
  GUIStyle content_style;
  GUIStyle quote_style;

  // body index & situation
  int body_index;
  int situation_index;

  // current planner page
  uint page;

  // useful rules
  bool rules_detected = false;
  Rule rule_temp = null;
  Rule rule_radiation = null;
  Rule rule_qol = null;

  // automatic page layout of panels
  uint panels_count;
  uint panels_per_page;
  uint pages_count;

  // planner data, set when planner render updates are run
  List<Part> parts;
  environment_data env;
  crew_data crew;
  radiation_data radiation;
  ec_data ec;
  signal_data signal;
  qol_data qol;
  Dictionary<string, supply_data> supplies;
  reliability_data reliability;
  resource_collection resources;

  // ctor
  public Planner()
  {
    // set default body index & situation
    body_index = FlightGlobals.GetHomeBodyIndex();
    situation_index = 1;

    // left menu style
    leftmenu_style = new GUIStyle(HighLogic.Skin.label);
    leftmenu_style.richText = true;
    leftmenu_style.normal.textColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);
    leftmenu_style.fixedWidth = 120.0f;
    leftmenu_style.stretchHeight = true;
    leftmenu_style.fontSize = 10;
    leftmenu_style.alignment = TextAnchor.MiddleLeft;

    // mid menu style
    midmenu_style = new GUIStyle(leftmenu_style);
    midmenu_style.fixedWidth = 0.0f;
    midmenu_style.stretchWidth = true;
    midmenu_style.alignment = TextAnchor.MiddleCenter;

    // right menu style
    rightmenu_style = new GUIStyle(leftmenu_style);
    rightmenu_style.alignment = TextAnchor.MiddleRight;

    // row style
    row_style = new GUIStyle();
    row_style.stretchWidth = true;
    row_style.fixedHeight = 16.0f;

    // title style
    title_style = new GUIStyle(HighLogic.Skin.label);
    title_style.normal.background = Lib.GetTexture("black-background");
    title_style.normal.textColor = Color.white;
    title_style.stretchWidth = true;
    title_style.stretchHeight = false;
    title_style.fixedHeight = 16.0f;
    title_style.fontSize = 12;
    title_style.border = new RectOffset(0, 0, 0, 0);
    title_style.padding = new RectOffset(3, 4, 3, 4);
    title_style.alignment = TextAnchor.MiddleCenter;

    // content style
    content_style = new GUIStyle(HighLogic.Skin.label);
    content_style.richText = true;
    content_style.normal.textColor = Color.white;
    content_style.stretchWidth = true;
    content_style.stretchHeight = true;
    content_style.fontSize = 12;
    content_style.alignment = TextAnchor.MiddleLeft;

    // quote style
    quote_style = new GUIStyle(HighLogic.Skin.label);
    quote_style.richText = true;
    quote_style.normal.textColor = Color.white;
    quote_style.stretchWidth = true;
    quote_style.stretchHeight = true;
    quote_style.fontSize = 11;
    quote_style.alignment = TextAnchor.LowerCenter;
  }


  public static environment_data analyze_environment(CelestialBody body, double altitude_mult)
  {
    // shortcuts
    CelestialBody sun = Sim.Sun();

    // calculate data
    environment_data env = new environment_data();
    env.body = body;
    env.altitude = body.Radius * altitude_mult;
    env.landed = env.altitude <= double.Epsilon;
    env.breathable = env.landed && body.atmosphereContainsOxygen;
    env.sun_dist = Sim.Apoapsis(Lib.PlanetarySystem(body)) - sun.Radius - body.Radius;
    Vector3d sun_dir = (sun.position - body.position).normalized;
    env.sun_flux = Sim.SolarFlux(env.sun_dist);
    env.body_flux = Sim.BodyFlux(body, body.position + sun_dir * (body.Radius + env.altitude));
    env.body_back_flux = Sim.BodyFlux(body, body.position - sun_dir * (body.Radius + env.altitude));
    env.background_temp = Sim.BackgroundTemperature();
    env.sun_temp = Sim.BlackBody(env.sun_flux);
    env.body_temp = Sim.BlackBody(env.body_flux);
    env.body_back_temp = Sim.BlackBody(env.body_back_flux);
    env.light_temp = env.background_temp + env.sun_temp + env.body_temp;
    env.shadow_temp = env.background_temp + env.body_back_temp;
    env.atmo_temp = body.GetTemperature(0.0);
    env.orbital_period = Sim.OrbitalPeriod(body, env.altitude);
    env.shadow_period = Sim.ShadowPeriod(body, env.altitude);
    env.shadow_time = env.shadow_period / env.orbital_period;
    env.temp_diff = env.landed && body.atmosphere
      ? Sim.TempDiff(env.atmo_temp)
      : Lib.Mix(Sim.TempDiff(env.light_temp), Sim.TempDiff(env.shadow_temp), env.shadow_time);
    env.atmo_factor = env.landed ? Sim.AtmosphereFactor(body, 0.7071) : 1.0;

    // return data
    return env;
  }


  public static crew_data analyze_crew(List<Part> parts)
  {
    // store data
    crew_data crew = new crew_data();

    // get number of kerbals assigned to the vessel in the editor
    // note: crew manifest is not reset after root part is deleted
    var cad = KSP.UI.CrewAssignmentDialog.Instance;
    if (cad != null && cad.GetManifest() != null)
    {
      List<ProtoCrewMember> manifest = cad.GetManifest().GetAllCrew(false);
      crew.count = (uint)manifest.Count;
      crew.engineer = manifest.Find(k => k.trait == "Engineer") != null;
    }

    // scan the parts
    foreach(Part p in parts)
    {
      // accumulate crew capacity
      crew.capacity += (uint)p.CrewCapacity;
    }

    // return data
    return crew;
  }


  public static ec_data analyze_ec(List<Part> parts, environment_data env, crew_data crew, Rule rule_temp, resource_collection resources)
  {
    // store data
    ec_data ec = new ec_data();

    // calculate climate cost
    ec.consumed = rule_temp != null ? (double)crew.count * env.temp_diff * rule_temp.rate : 0.0;

    // scan the parts
    foreach(Part p in parts)
    {
      // accumulate EC storage
      ec.storage += Lib.GetResourceAmount(p, "ElectricCharge");

      // remember if we already considered a resource converter module
      // rationale: we assume only the first module in a converter is active
      bool first_converter = true;

      // for each module
      foreach(PartModule m in p.Modules)
      {
        // command
        if (m.moduleName == "ModuleCommand")
        {
          ModuleCommand mm = (ModuleCommand)m;
          foreach(ModuleResource res in mm.inputResources)
          {
            if (res.name == "ElectricCharge")
            {
              ec.consumed += res.rate;
            }
          }
        }
        // solar panel
        else if (m.moduleName == "ModuleDeployableSolarPanel")
        {
          ModuleDeployableSolarPanel mm = (ModuleDeployableSolarPanel)m;
          double solar_k = (mm.useCurve ? mm.powerCurve.Evaluate((float)env.sun_dist) : env.sun_flux / Sim.SolarFluxAtHome());
          double generated = mm.chargeRate * solar_k * env.atmo_factor;
          ec.generated_sunlight += generated;
          ec.best_ec_generator = Math.Max(ec.best_ec_generator, generated);
        }
        // generator
        else if (m.moduleName == "ModuleGenerator")
        {
          // skip launch clamps, that include a generator
          if (p.partInfo.name == "launchClamp1") continue;

          ModuleGenerator mm = (ModuleGenerator)m;
          foreach(ModuleResource res in mm.inputList)
          {
            if (res.name == "ElectricCharge")
            {
              ec.consumed += res.rate;
            }
          }
          foreach(ModuleResource res in mm.outputList)
          {
            if (res.name == "ElectricCharge")
            {
              ec.generated_shadow += res.rate;
              ec.generated_sunlight += res.rate;
              ec.best_ec_generator = Math.Max(ec.best_ec_generator, res.rate);
            }
          }
        }
        // converter
        // note: only electric charge is considered for resource converters
        // note: we only consider the first resource converter in a part, and ignore the rest
        // note: support PlanetaryBaseSystem converters
        else if ((m.moduleName == "ModuleResourceConverter" || m.moduleName == "ModuleKPBSConverter") && first_converter)
        {
          ModuleResourceConverter mm = (ModuleResourceConverter)m;
          foreach(ResourceRatio rr in mm.inputList)
          {
            if (rr.ResourceName == "ElectricCharge")
            {
              ec.consumed += rr.Ratio;
            }
          }
          foreach(ResourceRatio rr in mm.outputList)
          {
            if (rr.ResourceName == "ElectricCharge")
            {
              ec.generated_shadow += rr.Ratio;
              ec.generated_sunlight += rr.Ratio;
              ec.best_ec_generator = Math.Max(ec.best_ec_generator, rr.Ratio);
            }
          }
          first_converter = false;
        }
        // harvester
        // note: only electric charge is considered for resource harvesters
        else if (m.moduleName == "ModuleResourceHarvester")
        {
          ModuleResourceHarvester mm = (ModuleResourceHarvester)m;
          foreach(ResourceRatio rr in mm.inputList)
          {
            if (rr.ResourceName == "ElectricCharge")
            {
              ec.consumed += rr.Ratio;
            }
          }
        }
        // active radiators
        else if (m.moduleName == "ModuleActiveRadiator")
        {
          ModuleActiveRadiator mm = (ModuleActiveRadiator)m;
          if (mm.IsCooling)
          {
            foreach(var rr in mm.inputResources)
            {
              if (rr.name == "ElectricCharge")
              {
                ec.consumed += rr.rate;
              }
            }
          }
        }
        // wheels
        else if (m.moduleName == "ModuleWheelMotor")
        {
          ModuleWheelMotor mm = (ModuleWheelMotor)m;
          if (mm.motorEnabled && mm.inputResource.name == "ElectricCharge")
          {            
            ec.consumed += mm.inputResource.rate;
          }
        }
        else if (m.moduleName == "ModuleWheelMotorSteering")
        {
          ModuleWheelMotorSteering mm = (ModuleWheelMotorSteering)m;
          if (mm.motorEnabled && mm.inputResource.name == "ElectricCharge")
          {
            ec.consumed += mm.inputResource.rate;
          }
        }
        // scrubber
        else if (m.moduleName == "Scrubber")
        {
          Scrubber scrubber = (Scrubber)m;
          if (scrubber.is_enabled && !env.breathable)
          {
            ec.consumed += scrubber.ec_rate;
          }
        }
        // recycler
        else if (m.moduleName == "Recycler")
        {
          Recycler recycler = (Recycler)m;
          if (recycler.is_enabled)
          {
            double rateMod = resources.Availability(recycler.waste_name);
            if (!String.IsNullOrEmpty(recycler.filter_name)) rateMod = Math.Min(rateMod, resources.Availability(recycler.filter_name));
            ec.consumed += recycler.ec_rate * rateMod;
          }
        }
        // greenhouse
        else if (m.moduleName == "Greenhouse")
        {
          Greenhouse greenhouse = (Greenhouse)m;
          ec.consumed += greenhouse.ec_rate * greenhouse.lamps;
        }
        // gravity ring hab
        else if (m.moduleName == "GravityRing")
        {
          GravityRing mm = (GravityRing)m;
          if (mm.opened) ec.consumed += mm.ec_rate * mm.speed;
        }
        // antennas
        else if (m.moduleName == "Antenna")
        {
          Antenna antenna = (Antenna)m;
          if (antenna.relay) ec.consumed += antenna.relay_cost;
        }
        // SCANsat support
        else if (m.moduleName == "SCANsat" || m.moduleName == "ModuleSCANresourceScanner")
        {
          // include it in ec consumption, if deployed
          if (SCANsat.isDeployed(p, m)) ec.consumed += Lib.ReflectionValue<float>(m, "power");
        }
        // NearFutureSolar support
        // note: assume half the components are in sunlight, and average inclination is half
        else if (m.moduleName == "ModuleCurvedSolarPanel")
        {
          // get total rate
          double tot_rate = Lib.ReflectionValue<float>(m, "TotalEnergyRate");

          // get number of components
          int components = p.FindModelTransforms(Lib.ReflectionValue<string>(m, "PanelTransformName")).Length;

          // approximate output
          // 0.7071: average clamped cosine
          ec.generated_sunlight += 0.7071 * tot_rate;
        }
        // NearFutureElectrical support
        else if (m.moduleName == "FissionGenerator")
        {
          double max_rate = Lib.ReflectionValue<float>(m, "PowerGeneration");

          // get fission reactor tweakable, will default to 1.0 for other modules
          var reactor = p.FindModuleImplementing<ModuleResourceConverter>();
          double tweakable = reactor == null ? 1.0 : Lib.ReflectionValue<float>(reactor, "CurrentPowerPercent") * 0.01f;

          ec.generated_sunlight += max_rate * tweakable;
          ec.generated_shadow += max_rate * tweakable;
        }
        else if (m.moduleName == "ModuleRadioisotopeGenerator")
        {
          double max_rate = Lib.ReflectionValue<float>(m, "BasePower");

          ec.generated_sunlight += max_rate;
          ec.generated_shadow += max_rate;
        }
      }
    }

    // finally, calculate life expectancy of ec
    ec.life_expectancy_sunlight = ec.storage / Math.Max(ec.consumed - ec.generated_sunlight, 0.0);
    ec.life_expectancy_shadow = ec.storage / Math.Max(ec.consumed - ec.generated_shadow, 0.0);

    // return data
    return ec;
  }


  public static supply_data analyze_supply(List<Part> parts, environment_data env, crew_data crew, Rule rule)
  {
    // store data
    supply_data data = new supply_data();

    // calculate resource consumed
    // note: this assume the waste of the rule is the same as the waste on the scrubber/greenhouse
    data.consumed = (double)crew.count * (rule.interval > 0 ? rule.rate / rule.interval : rule.rate);
    if (rule.modifier.Contains("breathable") && env.breathable) data.consumed = 0;

    // calculate waste produced
    double sim_waste = data.consumed * rule.waste_ratio;

    // scan the parts
    foreach(Part p in parts)
    {
      // accumulate storage
      data.storage += Lib.GetResourceAmount(p, rule.resource_name);

      // for each module
      foreach(PartModule m in p.Modules)
      {
        // greenhouse
        if (m.moduleName == "Greenhouse")
        {
          Greenhouse greenhouse = (Greenhouse)m;
          if (greenhouse.resource_name != rule.resource_name) continue;
          data.has_greenhouse = true;

          // calculate natural lighting
          double natural_lighting = Greenhouse.NaturalLighting(env.sun_dist);

          // calculate lighting
          double lighting = natural_lighting * (greenhouse.door_opened ? 1.0 : 0.0) + greenhouse.lamps;

          // calculate waste used
          double waste_perc = 0.0;
          if (greenhouse.waste_name.Length > 0)
          {
            double waste_used = Math.Min(sim_waste, greenhouse.waste_rate);
            waste_perc = waste_used / greenhouse.waste_rate;
            sim_waste -= waste_used;
          }

          // calculate growth bonus
          double growth_bonus = 0.0;
          growth_bonus += greenhouse.soil_bonus * (env.landed ? 1.0 : 0.0);
          growth_bonus += greenhouse.waste_bonus * waste_perc;

          // calculate growth factor
          double growth_factor = (greenhouse.growth_rate * (1.0 + growth_bonus)) * lighting;

          // calculate food cultivated
          data.produced += greenhouse.harvest_size * growth_factor;

          // calculate time-to-harvest
          if (growth_factor > double.Epsilon) data.tta = 1.0 / growth_factor;
        }
        // scrubber
        else if (m.moduleName == "Scrubber")
        {
          Scrubber scrubber = (Scrubber)m;
          if (scrubber.resource_name != rule.resource_name) continue;
          data.has_recycler = true;

          // do nothing inside breathable atmosphere
          if (scrubber.is_enabled && !env.breathable)
          {
            double co2_scrubbed = Math.Min(sim_waste, scrubber.co2_rate);
            if (co2_scrubbed > double.Epsilon)
            {
              data.produced += co2_scrubbed * Scrubber.DeduceEfficiency();
              sim_waste -= co2_scrubbed;
            }
          }
        }
        // recycler
        else if (m.moduleName == "Recycler")
        {
          Recycler recycler = (Recycler)m;
          if (recycler.resource_name != rule.resource_name) continue;
          data.has_recycler = true;

          if (recycler.is_enabled)
          {
            double waste_recycled = Math.Min(sim_waste, recycler.waste_rate);
            if (waste_recycled > double.Epsilon)
            {
              data.produced += waste_recycled * recycler.waste_ratio;
              sim_waste -= waste_recycled;
            }
          }
        }
      }
    }

    // calculate life expectancy
    data.life_expectancy = data.storage / Math.Max(data.consumed - data.produced, 0.0);

    // return data
    return data;
  }


  public static qol_data analyze_qol(List<Part> parts, environment_data env, crew_data crew, signal_data signal, Rule rule_qol)
  {
    // store data
    qol_data qol = new qol_data();

    // scan the parts
    foreach(Part p in parts)
    {
      // for each module
      foreach(PartModule m in p.Modules)
      {
        // entertainment
        if (m.moduleName == "Entertainment")
        {
          Entertainment mm = (Entertainment)m;
          qol.entertainment *= mm.rate;
        }
        else if (m.moduleName == "GravityRing")
        {
          GravityRing mm = (GravityRing)m;
          qol.entertainment *= 1.0 + (mm.entertainment_rate - 1.0) * mm.speed;
        }
      }
    }

    // calculate Quality-Of-Life bonus
    // note: ignore kerbal-specific variance
    if (crew.capacity > 0)
    {
      bool linked = signal.range > 0.0 || !Kerbalism.features.signal;

      qol.living_space = QualityOfLife.LivingSpace(crew.count, crew.capacity);
      qol.bonus = QualityOfLife.Bonus(qol.living_space, qol.entertainment, env.landed, linked, crew.count == 1);

      qol.time_to_instability = qol.bonus / rule_qol.degeneration;
      List<string> factors = new List<string>();
      if (crew.count > 1) factors.Add("not-alone");
      if (linked) factors.Add("call-home");
      if (env.landed) factors.Add("firm-ground");
      if (factors.Count == 0) factors.Add("none");
      qol.factors = String.Join(", ", factors.ToArray());
    }
    else
    {
      qol.living_space = 0.0;
      qol.time_to_instability = double.NaN;
      qol.factors = "none";
      qol.bonus = 1.0;
    }

    // return data
    return qol;
  }


  public static radiation_data analyze_radiation(List<Part> parts, environment_data env, crew_data crew, Rule rule_radiation)
  {
    // store data
    radiation_data radiation = new radiation_data();

    // scan the parts
    foreach(Part p in parts)
    {
      // accumulate shielding amount and capacity
      radiation.shielding_amount += Lib.GetResourceAmount(p, "Shielding");
      radiation.shielding_capacity += Lib.GetResourceCapacity(p, "Shielding");
    }

    // calculate radiation data
    double shielding = Radiation.Shielding(radiation.shielding_amount, radiation.shielding_capacity);
    double belt_strength = Settings.BeltRadiation * Radiation.Dynamo(env.body) * 0.5; //< account for the 'ramp'
    if (crew.capacity > 0)
    {
      radiation.life_expectancy = new double[]
      {
        rule_radiation.fatal_threshold / (Settings.CosmicRadiation * (1.0 - shielding)),
        rule_radiation.fatal_threshold / (Settings.StormRadiation * (1.0 - shielding)),
        Radiation.HasBelt(env.body) ? rule_radiation.fatal_threshold / (belt_strength * (1.0 - shielding)) : double.NaN
      };
    }
    else
    {
      radiation.life_expectancy = new double[]{double.NaN, double.NaN, double.NaN};
    }

    // return data
    return radiation;
  }


  public static reliability_data analyze_reliability(List<Part> parts, ec_data ec, signal_data signal)
  {
    // store data
    reliability_data reliability = new reliability_data();

    // get manufacturing quality
    reliability.quality = Malfunction.DeduceQuality();

    // count parts that can fail
    uint components = 0;

    // scan the parts
    double year_time = 60.0 * 60.0 * Lib.HoursInDay() * Lib.DaysInYear();
    foreach(Part p in parts)
    {
      // for each module
      foreach(PartModule m in p.Modules)
      {
        // malfunctions
        if (m.moduleName == "Malfunction")
        {
          Malfunction mm = (Malfunction)m;
          ++components;
          double avg_lifetime = (mm.min_lifetime + mm.max_lifetime) * 0.5 * reliability.quality;
          reliability.failure_year += year_time / avg_lifetime;
        }
      }
    }

    // calculate reliability data
    double ec_redundancy = ec.best_ec_generator < ec.generated_sunlight ? (ec.generated_sunlight - ec.best_ec_generator) / ec.generated_sunlight : 0.0;
    double antenna_redundancy = signal.second_best_range > 0.0 ? signal.second_best_range / signal.range : 0.0;
    List<string> redundancies = new List<string>();
    if (ec_redundancy >= 0.5) redundancies.Add("ec");
    if (antenna_redundancy >= 0.99) redundancies.Add("antenna");
    if (redundancies.Count == 0) redundancies.Add("none");
    reliability.redundancy = String.Join(", ", redundancies.ToArray());

    // return data
    return reliability;
  }

  public static resource_collection analyze_resources(List<Rule> rules, List<Part> parts, environment_data env, crew_data crew, qol_data qol) {
    resource_collection resources = new resource_collection ();
    List<resource_transaction> transactionSet = new List<resource_transaction>();

    // Modeling resource lifetimes:
    // I.    Build resource dictionary and transaction list based on Rules and Converters (Scrubbers, Recyclers, etc.)
    // II.   Add amount/capacity of resources from List<Part> parts to resource dictionary.
    // III.  Use transaction list to set transaction blocking for each transaction. 
    // IV.   Loop through transaction list, executing transactions, until all transactions have executed, or some set remain blocked because of looping inputs.
    // V.    If some transactions remain... well it's complicated. (See below)
    // VI.   With all resource values populated (resupply, consumption, etc.), it is now possible to calculate an accurate lifetime for each resource.

    // I. Build resource dictionary and transaction list based on Rules and Converters (Scrubbers, Recyclers, etc.)
    // I.a: Rules
    foreach (Rule r in rules.FindAll(k => k.rate > 0.0)) {
      double rate = (double)crew.count * (r.interval > 0 ? r.rate / r.interval : r.rate);
      foreach (string modifier in r.modifier) {
        switch (modifier) {
        case "breathable": 
          rate *= env.breathable ? 0.0 : 1.0; 
          break;
        case "temperature": 
          rate *= env.temp_diff; 
          break;
        case "qol": 
          rate *= 1.0 / qol.bonus; 
          break;
        } 
      }

      resource_transaction transaction = new resource_transaction();
      transaction.type = "Rule";
      transaction.target = r;
      transaction.baseRate = rate;
      if (!String.IsNullOrEmpty(r.resource_name)) {        
        resources.Add(r.resource_name);
        resources[r.resource_name].draw += rate;
        transaction.inputs.Add(r.resource_name);
      }
      if (!String.IsNullOrEmpty(r.waste_name)) {
        resources.Add(r.waste_name);
        transaction.output.Add(r.waste_name);
      }
      transactionSet.Add(transaction);
    }

    // I.b: Converters (Scrubbers, Recyclers, and Greenhouses)
    foreach (Part p in parts.FindAll(part => part.Modules.Contains<Scrubber>() || part.Modules.Contains<Recycler>() || part.Modules.Contains<Greenhouse>())) {
      foreach (PartModule m in p.Modules) {      
        resource_transaction transaction = new resource_transaction();
        switch (m.moduleName) {
        case "Scrubber":
          Scrubber scrubber = (Scrubber)m;
          resources.Add(scrubber.resource_name);
          resources.Add(scrubber.waste_name);
          if (!scrubber.is_enabled) break; //If module not enabled, break out early
          if (!env.breathable) resources[scrubber.waste_name].draw += scrubber.co2_rate; //No draw in breathable atmosphere
          // Add transactions
          transaction.type = "Scrubber";
          transaction.target = m;
          if (!env.breathable) transaction.baseRate = scrubber.co2_rate;
          transaction.output.Add(scrubber.resource_name);
          if (!env.breathable) transaction.inputs.Add(scrubber.waste_name);
          transactionSet.Add(transaction);
          break;
        case "Recycler":
          Recycler recycler = (Recycler)m;
          resources.Add(recycler.resource_name);
          resources.Add(recycler.waste_name);
          if (!String.IsNullOrEmpty(recycler.filter_name)) resources.Add(recycler.filter_name); 
          if (!recycler.is_enabled) break; //If module not enabled, break out early
          resources[recycler.waste_name].draw += recycler.waste_rate;
          if (!String.IsNullOrEmpty(recycler.filter_name)) {
            resources[recycler.filter_name].draw += recycler.filter_rate;
            resources[recycler.resource_name].supplyRequires.Add(recycler.filter_name);
          }
          // Add transactions
          transaction.type = "Recycler";
          transaction.target = m;
          transaction.baseRate = recycler.waste_rate;
          transaction.output.Add(recycler.resource_name);
          transaction.inputs.Add(recycler.waste_name);
          if (!String.IsNullOrEmpty(recycler.filter_name)) transaction.inputs.Add(recycler.filter_name); 
          transactionSet.Add(transaction);
          break;
        case "Greenhouse":
          Greenhouse greenhouse = (Greenhouse)m;
          resources.Add(greenhouse.resource_name);
          resources.Add(greenhouse.waste_name);
          resources[greenhouse.waste_name].draw += greenhouse.waste_rate;
          if (!String.IsNullOrEmpty(greenhouse.input_name)) {
            resources.Add(greenhouse.input_name);
            resources[greenhouse.input_name].draw += greenhouse.input_rate;
          }
          // Add transactions
          transaction.type = "Greenhouse";
          transaction.target = m;
          transaction.output.Add(greenhouse.resource_name);
          transaction.inputs.Add(greenhouse.waste_name);
          if (!String.IsNullOrEmpty(greenhouse.input_name)) transaction.inputs.Add(greenhouse.input_name); 
          transactionSet.Add(transaction);
          break;
        }
      }
    }

    // II. Add amount/capacity of resources from List<Part> parts to resource dictionary.    
    foreach (Part p in parts) {
      foreach (KeyValuePair<string, resource_data> resource in resources)
      {
        resource.Value.amount += Lib.GetResourceAmount(p, resource.Key);
        resource.Value.capacity += Lib.GetResourceCapacity(p, resource.Key);
      }
    }

    // III.  Use transaction list to find/set blocking transactions for each transaction. Also, set hasSupply flag. 
    foreach(resource_transaction rt in transactionSet){ 
      rt.SetBlocking(transactionSet); 
      rt.hasSupply = true;
      foreach (string inputResource in rt.inputs) {
        if (resources[inputResource].amount <= 0.0) rt.hasSupply = false;
      } 
    }

    // IV. Loop through transaction list, executing transactions, until all transactions have executed, or some set remain blocked because of looping inputs.
    bool done;
    do {
      done = true;
      foreach (resource_transaction transaction in transactionSet) {
        if (!transaction.hasRun && transaction.ShouldRun()) {
          resources += transaction.Run(resources, env);
          done = false;
        } 
      }
    } while (!done);

    // V. The remainder...
    // If there are any transactions left, they're either duds that will never run (because of some resource requirement that is never met) or they have looping inputs/outputs.
    // Duds we can ignore at this point, as they won't have any effect on anything else. Loops, though, require some special handling.
    // 
    // Because of the feedback between looped rules/converters, the steady state a looping system quickly falls into is a function of the sum of an infinite geometric series, 
    // based on the efficiency of the system, and the initial input. Thankfully, we only actually need to know the initial input and two iterations of the system to calculate
    // the final value at infinite iterations using this equation: The FinalRate will equal the BaseRate + (RateDelta1 / (1 - (RateDelta2/RateDelta1)))
    //
    // So, as an example, a system with a resupply rate of 8 units per second initially, 11/s after one iteration (delta1 = +3), and 12.125/s the next (delta2 = +1.125),
    // would have an actual rate of resupply of 8 + (3/(1-(1.125/3))) = 12.8 per second after a moment or two, and that's the value to use when calculating resource lifetime.
    //
    List<resource_transaction> blockedTransactions = transactionSet.FindAll(tr => tr.hasRun == false); //Transactions that did not run because they have looping inputs/ouputs.

    List<string> resourceNames = new List<string>();
    foreach (resource_transaction rt in blockedTransactions) {
      resourceNames = resourceNames.Union(rt.inputs).ToList();
      resourceNames = resourceNames.Union(rt.output).ToList();
    }

    if (blockedTransactions.Count != 0) {
      resource_collection[] deltaSlice = new resource_collection[4];
      deltaSlice[0] = new resource_collection() + resources; //Initial state for iteration

      int iteration = 0;
      do {
        // initialize next iteration to baseline
        deltaSlice[iteration+1] = new resource_collection();
        foreach(KeyValuePair<string, resource_data> kv in resources) deltaSlice[iteration+1].Add(kv.Key, new resource_data(draw: kv.Value.draw));

        //Spend initial resource supply from previous iteration/baserate
        foreach(resource_transaction transaction in blockedTransactions.FindAll(tr => tr.hasSomeSupply(deltaSlice[iteration]) )){ 
          deltaSlice[iteration] += transaction.Run(deltaSlice[iteration], env);
        }

        //Then loop through the transaction list looking for any transactions that are now unblocked, adding the result to the next iteration
        do {
          done = true;
          foreach (resource_transaction transaction in blockedTransactions) {
            if (transaction.ShouldRun() && !transaction.hasRun) {
              resource_collection delta = transaction.Run(deltaSlice[iteration], env);
              deltaSlice[iteration].ApplyConsumption(delta);
              deltaSlice[iteration+1].ApplyProduction(delta);
              done = false;
            } 
          }
        } while (!done);

        //Reset hasRun flag for all transactions in blockedTransactions to prepare for next iteration
        foreach (resource_transaction transaction in blockedTransactions) transaction.hasRun = false;
        iteration++;
      } while (iteration < 3);

      // Calculate results and add them back to resources.
      resource_collection calculatedDelta = new resource_collection();

      foreach (KeyValuePair<string, resource_data> kv in deltaSlice[0]) {
        double[] conDelta = new double[3];
        conDelta[0] = kv.Value.consumed;
        conDelta[1] = deltaSlice[1][kv.Key].consumed;
        conDelta[2] = deltaSlice[2][kv.Key].consumed;

        double[] prodDelta = new double[3];
        prodDelta[0] = kv.Value.produced;
        prodDelta[1] = deltaSlice[1][kv.Key].produced;
        prodDelta[2] = deltaSlice[2][kv.Key].produced;

        double consumption;
        if (conDelta[1] == 0.0) {
          consumption = conDelta[0];
        } else if (conDelta[2] >= conDelta[1]) {
          consumption = resources[kv.Key].draw; //Infinite/accelerating growth, rate capped by draw
        } else {
          consumption = conDelta[0] + (conDelta[1] / (1 - (conDelta[2] / conDelta[1])));
        }

        double production;
        if (prodDelta[1] == 0.0) {
          production = prodDelta[0];
        } else if (prodDelta[2] >= prodDelta[1]) {
          production = resources[kv.Key].draw; //Infinite/accelerating growth, rate capped by draw
        } else {
          production = prodDelta[0] + (prodDelta[1] / (1 - (prodDelta[2] / prodDelta[1])));
        }

        if(resourceNames.Contains(kv.Key)) calculatedDelta.Add(kv.Key, new resource_data(consumed: consumption, produced: production));
      }
      resources += calculatedDelta;
    }
  return resources;
}

  public static signal_data analyze_signal(List<Part> parts)
  {
    // store data
    signal_data signal = new signal_data();

    // get error correcting code factor
    signal.ecc = Signal.ECC();

    // scan the parts
    foreach(Part p in parts)
    {
      // for each module
      foreach(PartModule m in p.Modules)
      {
        // antenna
        if (m.moduleName == "Antenna")
        {
          Antenna mm = (Antenna)m;

          // calculate actual range
          double range = Signal.Range(mm.scope, mm.penalty, signal.ecc);

          // maintain 2nd best antenna
          signal.second_best_range = range > signal.range ? signal.range : Math.Max(signal.second_best_range, range);

          // keep track of best antenna
          if (range > signal.range)
          {
            signal.range = range;
            signal.transmission_cost_min = mm.min_transmission_cost;
            signal.transmission_cost_max = mm.max_transmission_cost;
          }

          // keep track of best relay antenna
          if (mm.relay && range > signal.relay_range)
          {
            signal.relay_range = range;
            signal.relay_cost = mm.relay_cost;
          }
        }
      }
    }

    // return data
    return signal;
  }


  void render_title(string title)
  {
    GUILayout.BeginHorizontal();
    GUILayout.Label(title, title_style);
    GUILayout.EndHorizontal();
  }


  void render_content(string desc, string value, string tooltip="")
  {
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(new GUIContent(desc + ": <b>" + value + "</b>", tooltip.Length > 0 ? "<i>" + tooltip + "</i>" : ""), content_style);
    GUILayout.EndHorizontal();
  }


  void render_space()
  {
    GUILayout.Space(10.0f);
  }


  void render_environment(environment_data env)
  {
    bool in_atmosphere = env.landed && env.body.atmosphere;
    string temperature_str = in_atmosphere
      ? Lib.HumanReadableTemp(env.atmo_temp)
      : Lib.HumanReadableTemp(env.light_temp) + "</b> / <b>"
      + Lib.HumanReadableTemp(env.shadow_temp);
    string temperature_tooltip = in_atmosphere
      ? "atmospheric"
      : "sunlight / shadow\n"
      + "solar: <b>" + Lib.HumanReadableTemp(env.sun_temp) + "</b>\n"
      + "albedo (sunlight): <b>" + Lib.HumanReadableTemp(env.body_temp) + "</b>\n"
      + "albedo (shadow): <b>" + Lib.HumanReadableTemp(env.body_back_temp) + "</b>\n"
      + "background: <b>" + Lib.HumanReadableTemp(env.background_temp) + "</b>";
    string atmosphere_tooltip = in_atmosphere
      ? "light absorption: <b>" + ((1.0 - env.atmo_factor) * 100.0).ToString("F0") + "%</b>\n"
      + "pressure: <b>" + env.body.atmospherePressureSeaLevel.ToString("F0") + " kPa</b>\n"
      + "breathable: <b>" + (env.body.atmosphereContainsOxygen ? "yes" : "no") + "</b>"
      : "";
    string shadowtime_str = Lib.HumanReadableDuration(env.shadow_period) + " (" + (env.shadow_time * 100.0).ToString("F0") + "%)";

    render_title("ENVIRONMENT");
    render_content("temperature", temperature_str, temperature_tooltip);
    render_content("temp diff", env.temp_diff.ToString("F0") + "K", "average difference between\nexternal and survival temperature");
    render_content("inside atmosphere", in_atmosphere ? "yes" : "no", atmosphere_tooltip);
    render_content("shadow time", shadowtime_str, "the time in shadow\nduring the orbit");
    render_space();
  }


  void render_ec(ec_data ec)
  {
    bool shadow_different = Math.Abs(ec.generated_sunlight - ec.generated_shadow) > double.Epsilon;
    string generated_str = Lib.HumanReadableRate(ec.generated_sunlight) + (shadow_different ? "</b> / <b>" + Lib.HumanReadableRate(ec.generated_shadow) : "");
    string life_str = Lib.HumanReadableDuration(ec.life_expectancy_sunlight) + (shadow_different ? "</b> / <b>" + Lib.HumanReadableDuration(ec.life_expectancy_shadow) : "");

    render_title("ELECTRIC CHARGE");
    render_content("storage", Lib.ValueOrNone(ec.storage));
    render_content("consumed", Lib.HumanReadableRate(ec.consumed));
    render_content("generated", generated_str, "sunlight / shadow");
    render_content("life expectancy", life_str, "sunlight / shadow");
    render_space();
  }


  void render_supply(supply_data supply, Rule rule)
  {
    render_title(rule.resource_name.ToUpper());
    render_content("storage", Lib.ValueOrNone(supply.storage));
    render_content("consumed", Lib.HumanReadableRate(supply.consumed));
    if (supply.has_greenhouse) render_content("time to harvest", Lib.HumanReadableDuration(supply.tta));
    else if (supply.has_recycler) render_content("recycled", Lib.HumanReadableRate(supply.produced));
    else render_content("produced", Lib.HumanReadableRate(supply.produced));
    render_content(rule.breakdown ? "time to instability" : "life expectancy", Lib.HumanReadableDuration(supply.life_expectancy));
    render_space();
  }

  void render_resource(resource_collection resources, string name, Rule rule)
  {
    resource_data resource = resources[name];
    render_title(rule.resource_name.ToUpper());
    render_content("storage", Lib.ValueOrNone(resource.capacity));
    render_content("consumed", Lib.HumanReadableRate(resource.consumed));
    if (resource.hasGreenhouse) render_content("greenhouse", String.Format("{0} every {1}", resource.harvestSize, Lib.HumanReadableDuration(resource.harvestTime)));
    else if (resource.hasRecycler && resource.supplyRequires.Count > 0) render_content("recycled",  String.Format("{0}, req. filter.", Lib.HumanReadableRate(resource.produced)));
    else if (resource.hasRecycler) render_content("recycled", Lib.HumanReadableRate(resource.produced));
    else if (resource.hasScrubber) render_content("scrubbed", Lib.HumanReadableRate(resource.produced));
    else render_content("produced", Lib.HumanReadableRate(resource.produced));
    render_content(rule.breakdown ? "time to instability" : "life expectancy", Lib.HumanReadableDuration( resource.lifeTime(resources) ));
    render_space();
  }

  void render_qol(qol_data qol, Rule rule)
  {
    render_title("QUALITY OF LIFE");
    render_content("living space", QualityOfLife.LivingSpaceToString(qol.living_space));
    render_content("entertainment", QualityOfLife.EntertainmentToString(qol.entertainment));
    render_content("other factors", qol.factors);
    render_content(rule.breakdown ? "time to instability" : "life expectancy", Lib.HumanReadableDuration(qol.time_to_instability));
    render_space();
  }


  void render_radiation(radiation_data radiation, environment_data env, crew_data crew)
  {
    string magnetosphere_str = Radiation.HasMagnetosphere(env.body) ? Lib.HumanReadableRange(Radiation.MagnAltitude(env.body)) : "none";
    string belt_strength_str = Radiation.HasBelt(env.body) ? " (" + (Radiation.Dynamo(env.body) * Settings.BeltRadiation * (60.0 * 60.0)).ToString("F0") + " rad/h)" : "";
    string belt_str = Radiation.HasBelt(env.body) ? Lib.HumanReadableRange(Radiation.BeltAltitude(env.body)) : "none";
    string shield_str = Radiation.ShieldingToString(radiation.shielding_amount, radiation.shielding_capacity);
    string shield_tooltip = radiation.shielding_capacity > 0 ? "average over the vessel" : "";
    string life_str = Lib.HumanReadableDuration(radiation.life_expectancy[0]) + "</b> / <b>" + Lib.HumanReadableDuration(radiation.life_expectancy[1]);
    string life_tooltip = "cosmic / storm";
    if (Radiation.HasBelt(env.body))
    {
      life_str += "</b> / <b>" + Lib.HumanReadableDuration(radiation.life_expectancy[2]);
      life_tooltip += " / belt";
    }

    render_title("RADIATION");
    render_content("magnetosphere", magnetosphere_str, "protect from cosmic radiation");
    render_content("radiation belt", belt_str, "abnormal radiation zone" + belt_strength_str);
    render_content("shielding", shield_str, shield_tooltip);
    render_content("life expectancy", crew.capacity > 0 ? life_str : "perpetual", crew.capacity > 0 ? life_tooltip : "");
    render_space();
  }


  void render_reliability(reliability_data reliability, crew_data crew)
  {
    render_title("RELIABILITY");
    render_content("malfunctions", Lib.ValueOrNone(reliability.failure_year, "/y"), "average case estimate\nfor the whole vessel");
    render_content("redundancy", reliability.redundancy);
    render_content("quality", Malfunction.QualityToString(reliability.quality), "manufacturing quality");
    render_content("engineer", crew.engineer ? "yes" : "no");
    render_space();
  }


  void render_signal(signal_data signal, environment_data env, crew_data crew)
  {
    // approximate min/max distance between home and target body
    CelestialBody home = FlightGlobals.GetHomeBody();
    double home_dist_min = 0.0;
    double home_dist_max = 0.0;
    if (env.body == home)
    {
      home_dist_min = env.altitude;
      home_dist_max = env.altitude;
    }
    else if (env.body.referenceBody == home)
    {
      home_dist_min = Sim.Periapsis(env.body);
      home_dist_max = Sim.Apoapsis(env.body);
    }
    else
    {
      double home_p = Sim.Periapsis(Lib.PlanetarySystem(home));
      double home_a = Sim.Apoapsis(Lib.PlanetarySystem(home));
      double body_p = Sim.Periapsis(Lib.PlanetarySystem(env.body));
      double body_a = Sim.Apoapsis(Lib.PlanetarySystem(env.body));
      home_dist_min = Math.Min(Math.Abs(home_a - body_p), Math.Abs(home_p - body_a));
      home_dist_max = home_a + body_a;
    }

    // calculate if antenna is out of range from target body
    string range_tooltip = "";
    if (signal.range > double.Epsilon)
    {
      if (signal.range < home_dist_min) range_tooltip = "<color=#ff0000>out of range</color>";
      else if (signal.range < home_dist_max) range_tooltip = "<color=#ffff00>partially out of range</color>";
      else range_tooltip = "<color=#00ff00>in range</color>";
      if (home_dist_max > double.Epsilon) //< if not landed at home
      {
        range_tooltip += "\nbody distance (min): <b>" + Lib.HumanReadableRange(home_dist_min) + "</b>"
        + "\nbody distance (max): <b>" + Lib.HumanReadableRange(home_dist_max) + "</b>";
      }
    }
    else if (crew.capacity == 0) range_tooltip = "<color=#ff0000>no antenna on unmanned vessel</color>";

    // calculate transmission cost
    double cost = signal.range > double.Epsilon
      ? signal.transmission_cost_min + (signal.transmission_cost_max - signal.transmission_cost_min) * Math.Min(home_dist_max, signal.range) / signal.range
      : 0.0;
    string cost_str = signal.range > double.Epsilon ? cost.ToString("F1") + " EC/Mbit" : "none";

    // generate ecc table
    Func<double, double, double, string> deduce_color = (double range, double dist_min, double dist_max) =>
    {
      if (range < dist_min) return "<color=#ff0000>";
      else if (range < dist_max) return "<color=#ffff00>";
      else return "<color=#ffffff>";
    };
    double signal_100 = signal.range / signal.ecc;
    double signal_15 = signal_100 * 0.15;
    double signal_33 = signal_100 * 0.33;
    double signal_66 = signal_100 * 0.66;
    string ecc_tooltip = signal.range > double.Epsilon
      ? "<align=left /><b>ecc</b>\t<b>range</b>"
      + "\n15%\t"  + deduce_color(signal_15, home_dist_min, home_dist_max) + Lib.HumanReadableRange(signal_15) + "</color>"
      + "\n33%\t"  + deduce_color(signal_33, home_dist_min, home_dist_max) + Lib.HumanReadableRange(signal_33) + "</color>"
      + "\n66%\t"  + deduce_color(signal_66, home_dist_min, home_dist_max) + Lib.HumanReadableRange(signal_66) + "</color>"
      + "\n100%\t" + deduce_color(signal_100,home_dist_min, home_dist_max) + Lib.HumanReadableRange(signal_100) + "</color>"
      : "";


    render_title("SIGNAL");
    render_content("range", Lib.HumanReadableRange(signal.range), range_tooltip);
    render_content("relay", signal.relay_range <= double.Epsilon ? "none" : signal.relay_range < signal.range ? Lib.HumanReadableRange(signal.relay_range) : "yes");
    render_content("transmission", cost_str, "worst case data transmission cost");
    render_content("error correction", (signal.ecc * 100.0).ToString("F0") + "%", ecc_tooltip);
    render_space();
  }


  public float width()
  {
    return 300.0f;
  }


  public float height()
  {
    // detect rules first time
    detect_rules();

    return 26.0f + 100.0f * (float)panels_per_page;
  }


  public void render()
  {
    // TODO: consider connected spaces in calculations

    // detect rules first time
    detect_rules();

    // if there is something in the editor
    if (EditorLogic.RootPart != null)
    {
      // store situations and altitude multipliers
      string[] situations = {"Landed", "Low Orbit", "Orbit", "High Orbit"};
      double[] altitude_mults = {0.0, 0.33, 1.0, 3.0};

      // get body, situation and altitude multiplier
      CelestialBody body = FlightGlobals.Bodies[body_index];
      string situation = situations[situation_index];
      double altitude_mult = altitude_mults[situation_index];

      // render menu
      GUILayout.BeginHorizontal (row_style);
      GUILayout.Label (body.name, leftmenu_style);
      if (Lib.IsClicked ()) {
        body_index = (body_index + 1) % FlightGlobals.Bodies.Count;
        if (body_index == 0) ++body_index;
        updateNeeded = true;
      } else if (Lib.IsClicked (1)) {
        body_index = (body_index - 1) % FlightGlobals.Bodies.Count;
        if (body_index == 0) body_index = FlightGlobals.Bodies.Count - 1;
        updateNeeded = true;
      }
      GUILayout.Label ("[" + (page + 1) + "/" + pages_count + "]", midmenu_style);
      if (Lib.IsClicked ()) {
        page = (page + 1) % pages_count;
        updateNeeded = true;
      } else if (Lib.IsClicked (1)) {
        page = (page == 0 ? pages_count : page) - 1u;
        updateNeeded = true;
      }
      GUILayout.Label (situation, rightmenu_style);
      if (Lib.IsClicked ()) {
        situation_index = (situation_index + 1) % situations.Length;
        updateNeeded = true;
      } else if (Lib.IsClicked (1)) {
        situation_index = (situation_index == 0 ? situations.Length : situation_index) - 1;
        updateNeeded = true;
      }
      GUILayout.EndHorizontal ();

      uint panel_index = 0;

      // throttle automatic data updates to a reasonable rate
      if (Time.time - lastUpdateTime >= timeBetweenUpdates) updateNeeded = true;

      // update data when requested
      if (updateNeeded) {        
        // get parts recursively
        parts = Lib.GetPartsRecursively (EditorLogic.RootPart);

        // analyze some stuff
        env = analyze_environment (body, altitude_mult);
        crew = analyze_crew (parts);
        signal = analyze_signal(parts);
        supplies = new Dictionary<string, supply_data>();
        foreach(Rule r in Kerbalism.supply_rules.FindAll(k => k.degeneration > 0.0)) supplies.Add(r.name, analyze_supply(parts, env, crew, r));
        if (rule_qol != null) qol = analyze_qol(parts, env, crew, signal, rule_qol);
        if (rule_radiation != null) radiation = analyze_radiation(parts, env, crew, rule_radiation);
        resources = analyze_resources (Kerbalism.supply_rules, parts, env, crew, qol);
        ec = analyze_ec(parts, env, crew, rule_temp, resources); 
        if (Kerbalism.features.malfunction) reliability = analyze_reliability(parts, ec, signal);

        /* 
        //Debug console spam used for testing
        foreach (KeyValuePair<string, resource_data> r in resources) {
          Debug.Log(String.Format ("{0}: [{1}/ {2}], draw of [{3:N6}/s]. Delta of {4:N6} [{5:N6} - {6:N6} /s].",
            r.Key, r.Value.amount, r.Value.capacity, r.Value.draw, r.Value.produced - r.Value.consumed, r.Value.produced, r.Value.consumed));
        } 
        Debug.Log ("Next resource empty: " + resources.NextToEmpty()); 
        */

        updateNeeded = false;
        lastUpdateTime = Time.time; //reset last update time to current time
      }

      // ec
      if (panel_index / panels_per_page == page)
      {
        render_ec(ec);
      }
      ++panel_index;

      // resources (supplies replacement)
      foreach (Rule r in Kerbalism.supply_rules) {
        if (panel_index / panels_per_page == page) render_resource(resources, r.resource_name, r);
        ++panel_index;
      }

/*      // supplies
      foreach(Rule r in Kerbalism.supply_rules.FindAll(k => k.degeneration > 0.0))
      {
        if (panel_index / panels_per_page == page) render_supply(supplies[r.name], r);
        ++panel_index;
      } */

      // qol
      if (rule_qol != null)
      {
        if (panel_index / panels_per_page == page) render_qol(qol, rule_qol);
        ++panel_index;
      }

      // radiation
      if (rule_radiation != null)
      {
        if (panel_index / panels_per_page == page) render_radiation(radiation, env, crew);
        ++panel_index;
      }

      // reliability
      if (Kerbalism.features.malfunction)
      {
        if (panel_index / panels_per_page == page) render_reliability(reliability, crew);
        ++panel_index;
      }

      // signal
      if (Kerbalism.features.signal)
      {
        if (panel_index / panels_per_page == page) render_signal(signal, env, crew);
        ++panel_index;
      }

      // environment
      if (panel_index / panels_per_page == page) render_environment(env);
      ++panel_index;
    }
    // if there is nothing in the editor
    else
    {
      // render quote
      GUILayout.FlexibleSpace();
      GUILayout.BeginHorizontal();
      GUILayout.Label("<i>In preparing for space, I have always found that\nplans are useless but planning is indispensable.\nWernher von Kerman</i>", quote_style);
      GUILayout.EndHorizontal();
      GUILayout.Space(10.0f);
    }
  }

  void detect_rules()
  {
    if (!rules_detected)
    {
      foreach(var p in Kerbalism.rules)
      {
        Rule r = p.Value;
        if (r.modifier.Contains("radiation")) rule_radiation = r;
        if (r.modifier.Contains("qol")) rule_qol = r;
        if (r.modifier.Contains("temperature") && r.resource_name == "ElectricCharge") rule_temp = r;
      }
      rules_detected = true;

      // guess number of panels
      panels_count = 2u
                   + (uint)Kerbalism.supply_rules.FindAll(k => k.degeneration > 0.0).Count
                   + (rule_qol != null ? 1u : 0)
                   + (rule_radiation != null ? 1u : 0)
                   + (Kerbalism.features.malfunction ? 1u : 0)
                   + (Kerbalism.features.signal ? 1u : 0);

      // calculate number of panels per page and number of pages
      switch(panels_count)
      {
        case 2u: panels_per_page = 2u; break;
        case 3u: panels_per_page = 3u; break;
        case 4u: panels_per_page = 4u; break;
        case 5u: panels_per_page = 3u; break;
        case 6u: panels_per_page = 3u; break;
        case 7u: panels_per_page = 4u; break;
        case 8u: panels_per_page = 4u; break;
        case 9u: panels_per_page = 3u; break;
        default: panels_per_page = 4u; break;
      }

      // calculate number of pages
      pages_count = (panels_count - 1u) / panels_per_page + 1u;
    }
  }
}


} // KERBALISM