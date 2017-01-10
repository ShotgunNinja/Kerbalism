﻿using System;
using System.Collections.Generic;


namespace KERBALISM {


// store info about a resource in a vessel
public sealed class resource_info
{
  public resource_info(Vessel v, string res_name)
  {
    // remember resource name
    resource_name = res_name;

    // get amount & capacity
    if (v.loaded)
    {
      foreach(Part p in v.Parts)
      {
        foreach(PartResource res in p.Resources)
        {
          if (res.flowState && res.resourceName == resource_name)
          {
            amount += res.amount;
            capacity += res.maxAmount;
          }
        }
      }
    }
    else
    {
      foreach(ProtoPartSnapshot pps in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartResourceSnapshot pprs in pps.resources)
        {
          if (pprs.resourceName == resource_name && pprs.flowState)
          {
            amount += pprs.amount;
            capacity += pprs.maxAmount;
          }
        }
      }
    }

    // calculate level
    level = capacity > double.Epsilon ? amount / capacity : 0.0;
  }

  // record a deferred production
  public void Produce(double quantity)
  {
    deferred += quantity;
  }

  // record a deferred consumption
  public void Consume(double quantity)
  {
    deferred -= quantity;
  }

  // synchronize amount from cache to vessel
  public void Sync(Vessel v, double elapsed_s)
  {
    // for loaded vessels
    if (v.loaded)
    {
      // for each part resource
      double new_amount = 0.0;
      capacity = 0.0;
      foreach(Part p in v.Parts)
      {
        foreach(PartResource r in p.Resources)
        {
          if (r.flowState && r.resourceName == resource_name)
          {
            // get amount/capacity
            new_amount += r.amount;
            capacity += r.maxAmount;

            // stock RequestResource() is iterating on all parts and all resources probably,
            // so we do both amount/capacity detection and resource synchronization at the same time
            // this also give coherency among flow rules between loaded and unloaded vessels
            if (Math.Abs(deferred) > 0.0000001)
            {
              double amount_diff = Lib.Clamp(r.amount + deferred, 0.0, r.maxAmount) - r.amount;
              r.amount += amount_diff;
              deferred -= amount_diff;
            }
            if (r.amount < 0.0000001) r.amount = 0.0;
          }
        }
      }

      // calculate rate of change per-second
      // note: do not update rate during and immediately after warp blending (stock modules have instabilities during warp blending)
      // note: rate is not updated during the simulation steps where meal is consumed, to avoid counting it twice
      if (Kerbalism.warp_blending > 50 && !meal_happened) rate = (new_amount - amount) / elapsed_s;

      // update amount
      amount = new_amount;
    }
    // for unloaded vessels
    else
    {
      // apply all deferred requests
      amount = Lib.Clamp(amount + deferred , 0.0, capacity);

      // calculate rate of change per-second
      // note: rate is not updated during the simulation steps where meal is consumed, to avoid counting it twice
      if (!meal_happened) rate = Lib.Clamp(deferred, -amount, capacity - amount) / elapsed_s;

      // syncronize the amount to the vessel
      foreach(ProtoPartSnapshot pps in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartResourceSnapshot res in pps.resources)
        {
          if (res.resourceName == resource_name && res.flowState)
          {
            double new_amount = Lib.Clamp(res.amount + deferred, 0.0, res.maxAmount);
            deferred -= new_amount - res.amount;
            res.amount = new_amount;
            if (Math.Abs(deferred) < 0.0000001) break;
          }
        }
      }
    }

    // recalculate level
    level = capacity > double.Epsilon ? amount / capacity : 0.0;

    // reset deferred consumption/production
    deferred = 0.0;

    // reseal meal flag
    meal_happened = false;
  }


  // estimate time until depletion
  public double Depletion(int crew_count)
  {
    // calculate all interval-normalized rates from related rules
    double meal_rate = 0.0;
    if (crew_count > 0)
    {
      foreach(Rule rule in Profile.rules)
      {
        if (rule.interval > 0)
        {
          if (rule.input == resource_name)  meal_rate -= rule.rate / rule.interval;
          if (rule.output == resource_name) meal_rate += rule.rate / rule.interval;
        }
      }
      meal_rate *= (double)crew_count;
    }

    // calculate total rate of change
    double delta = rate + meal_rate;

    // return depletion
    return amount <= double.Epsilon ? 0.0 : delta >= -0.0000001 ? double.NaN : amount / -delta;
  }


  public string resource_name;        // associated resource name
  public double deferred;             // accumulate deferred requests
  public double amount;               // amount of resource
  public double capacity;             // storage capacity of resource
  public double level;                // amount vs capacity, or 0 if there is no capacity
  public double rate;                 // rate of change in amount per-second
  public bool   meal_happened;        // true if a meal-like consumption/production was processed in the last simulation step
}


public sealed class resource_recipe
{
  public struct entry
  {
    public entry(string name, double quantity)
    {
      this.name = name;
      this.quantity = quantity;
      this.inv_quantity = 1.0 / quantity;
    }
    public string name;
    public double quantity;
    public double inv_quantity;
  }

  public resource_recipe(bool dump = false)
  {
    this.inputs = new List<entry>();
    this.outputs = new List<entry>();
    this.dump = dump;
    this.left = 1.0;
  }

  // add an input to the recipe
  public void Input(string resource_name, double quantity)
  {
    if (quantity > double.Epsilon) //< avoid division by zero
    {
      inputs.Add(new entry(resource_name, quantity));
    }
  }

  // add an output to the recipe
  public void Output(string resource_name, double quantity)
  {
    if (quantity > double.Epsilon) //< avoid division by zero
    {
      outputs.Add(new entry(resource_name, quantity));
    }
  }

  // execute the recipe
  public bool Execute(Vessel v, vessel_resources resources)
  {
    // determine worst input ratio
    // note: pure input recipes can just underflow
    double worst_input = left;
    if (outputs.Count > 0)
    {
      for(int i=0; i<inputs.Count; ++i)
      {
        entry e = inputs[i];
        resource_info res = resources.Info(v, e.name);
        worst_input = Lib.Clamp((res.amount + res.deferred) * e.inv_quantity, 0.0, worst_input);
      }
    }

    // determine worst output ratio
    // note: pure output recipes can just overflow
    // note: recipes that dump overboard can just overflow
    double worst_output = left;
    if (inputs.Count > 0 && !dump)
    {
      for(int i=0; i<outputs.Count; ++i)
      {
        entry e = outputs[i];
        resource_info res = resources.Info(v, e.name);
        worst_output = Lib.Clamp((res.capacity - (res.amount + res.deferred)) * e.inv_quantity, 0.0, worst_output);
      }
    }

    // determine worst-io
    double worst_io = Math.Min(worst_input, worst_output);

    // consume inputs
    for(int i=0; i<inputs.Count; ++i)
    {
      entry e = inputs[i];
      resources.Consume(v, e.name, e.quantity * worst_io);
    }

    // produce outputs
    for(int i=0; i<outputs.Count; ++i)
    {
      entry e = outputs[i];
      resources.Produce(v, e.name, e.quantity * worst_io);
    }

    // update amount left to execute
    left -= worst_io;

    // the recipe was executed, at least partially
    return worst_io > double.Epsilon;
  }


  public List<entry>  inputs;   // set of input resources
  public List<entry>  outputs;  // set of output resources
  public bool         dump;     // dump excess output if true
  public double       left;     // what proportion of the recipe is left to execute
}



// the resource cache of a vessel
public sealed class vessel_resources
{
  // return a resource handler
  public resource_info Info(Vessel v, string resource_name)
  {
    // try to get existing entry if any
    resource_info res;
    if (resources.TryGetValue(resource_name, out res)) return res;

    // create new entry
    res = new resource_info(v, resource_name);

    // remember new entry
    resources.Add(resource_name, res);

    // return new entry
    return res;
  }

  // apply deferred requests for a vessel and synchronize the new amount in the vessel
  public void Sync(Vessel v, double elapsed_s)
  {
    // execute all possible recipes
    bool executing = true;
    while(executing)
    {
      executing = false;
      for(int i=0; i<recipes.Count; ++i)
      {
        resource_recipe recipe = recipes[i];
        if (recipe.left > double.Epsilon)
        {
          executing |= recipe.Execute(v, this);
        }
      }
    }

    // forget the recipes
    recipes.Clear();

    // apply all deferred requests and synchronize to vessel
    foreach(var pair in resources) pair.Value.Sync(v, elapsed_s);
  }


  // record deferred production of a resource (shortcut)
  public void Produce(Vessel v, string resource_name, double quantity)
  {
    Info(v, resource_name).Produce(quantity);
  }

  // record deferred consumption of a resource (shortcut)
  public void Consume(Vessel v, string resource_name, double quantity)
  {
    Info(v, resource_name).Consume(quantity);
  }

  // record deferred execution of a recipe
  public void Transform(resource_recipe recipe)
  {
    recipes.Add(recipe);
  }


  public Dictionary<string, resource_info> resources = new Dictionary<string, resource_info>(32);
  public List<resource_recipe> recipes = new List<resource_recipe>(4);
}


// manage per-vessel resource caches
public static class ResourceCache
{
  public static void init()
  {
    entries = new Dictionary<Guid, vessel_resources>();
  }

  public static void clear()
  {
    entries.Clear();
  }

  public static void purge(Vessel v)
  {
    entries.Remove(v.id);
  }

  public static void purge(ProtoVessel pv)
  {
    entries.Remove(pv.vesselID);
  }

  // return resource cache for a vessel
  public static vessel_resources Get(Vessel v)
  {
    // try to get existing entry if any
    vessel_resources entry;
    if (entries.TryGetValue(v.id, out entry)) return entry;

    // create new entry
    entry = new vessel_resources();

    // remember new entry
    entries.Add(v.id, entry);

    // return new entry
    return entry;
  }

  // return a resource handler (shortcut)
  public static resource_info Info(Vessel v, string resource_name)
  {
    return Get(v).Info(v, resource_name);
  }

  // register deferred production of a resource (shortcut)
  public static void Produce(Vessel v, string resource_name, double quantity)
  {
    Info(v, resource_name).Produce(quantity);
  }

  // register deferred consumption of a resource (shortcut)
  public static void Consume(Vessel v, string resource_name, double quantity)
  {
    Info(v, resource_name).Consume(quantity);
  }

  // register deferred execution of a recipe (shortcut)
  public static void Transform(Vessel v, resource_recipe recipe)
  {
    Get(v).Transform(recipe);
  }


  // resource cache
  static Dictionary<Guid, vessel_resources> entries;
}


} // KERBALISM

