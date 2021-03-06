using System;
using System.Text;
using System.Collections.Generic;

namespace RCL.Kernel
{
  /// <summary>
  /// A timeline is a recording of symbols and times where events have
  /// occured, without any reference to the events themselves.
  /// </summary>
  public class Timeline
  {
    /// <summary>
    /// (G)lobal provides the index of the row that this cube was sourced from.
    /// This allows you to read a small slice from a big cube and then find the
    /// source rows again.
    /// </summary>
    public readonly RCArray<long> Global;

    /// <summary>
    /// Multiple rows in the timeline can be grouped together to represent a single,
    /// discrete event.  Consecutive rows having the same value in E (but differing
    /// symbols) will be treated as a single event.
    /// </summary>
    public readonly RCArray<long> Event;

    /// <summary>
    /// The (T)ime at the point the event was recorded.
    /// Mostly useful for display.
    /// It is also used when merging two cubes that don't share a common source.
    /// </summary>
    public readonly RCArray<RCTimeScalar> Time;

    /// <summary>
    /// The (S)ymbol on the event.
    /// Symbols are intended to represent entities in your application.
    /// Including symbols in the timeline allows you perform the calcs for many
    /// different entities at once.
    /// </summary>
    public readonly RCArray<RCSymbolScalar> Symbol;

    /// <summary>
    /// A collection of methods customizing behavior for various axis configurations.
    /// </summary>
    public readonly CubeProto Proto;

    // The list of columns on the axis.
    public readonly RCArray<string> Colset;

    protected int _count = 0;

    public Timeline (RCArray<RCSymbolScalar> s)
    {
      Colset = new RCArray<string> (1);
      Colset.Write ("S");
      Symbol = s;
      _count = Symbol.Count;
      Proto = CubeProto.Create (this);
    }

    public Timeline (RCArray<string> cols)
    {
      Colset = cols;
      if (Colset.Contains ("G")) {
        Global = new RCArray<long> ();
      }
      if (Colset.Contains ("E")) {
        Event = new RCArray<long> ();
      }
      if (Colset.Contains ("T")) {
        Time = new RCArray<RCTimeScalar> ();
      }
      if (Colset.Contains ("S")) {
        Symbol = new RCArray<RCSymbolScalar> ();
      }
      Proto = CubeProto.Create (this);
    }

    public Timeline (int count)
    {
      Colset = new RCArray<string> (0);
      _count = count;
      Proto = CubeProto.Create (this);
    }
    public Timeline (RCArray<long> g,
                     RCArray<long> e,
                     RCArray<RCTimeScalar> t,
                     RCArray<RCSymbolScalar> s)
    {
      Colset = new RCArray<string> (4);
      if (g != null) {
        Colset.Write ("G");
        Global = g;
        _count = Global.Count;
      }
      if (e != null) {
        Colset.Write ("E");
        Event = e;
        _count = Event.Count;
      }
      if (t != null) {
        Colset.Write ("T");
        Time = t;
        _count = Time.Count;
      }
      if (s != null) {
        Colset.Write ("S");
        Symbol = s;
        _count = Symbol.Count;
      }
      Proto = CubeProto.Create (this);
    }

    public Timeline (params string[] cols)
      : this (new RCArray<string> (cols)) {}

    /// <summary>
    /// For a Timeline, the standard sort order is by time column (G, E or T) followed by
    /// the symbol column, ascending. Timelines need to be sorted before they can be
    /// merged together with other timelines.
    /// </summary>
    public Timeline EnsureSorted ()
    {
      return Proto.Sort ();
    }

    public long TimeAt (int i)
    {
      if (Event == null) {
        return i;
      }
      return Event[i];
    }

    public long EventAt (int i)
    {
      if (Event == null) {
        return i;
      }
      return Event[i];
    }

    public RCTimeScalar RealTimeAt (int i)
    {
      if (Time == null) {
        return RCTimeScalar.Empty;
      }
      return Time[i];
    }

    public RCSymbolScalar SymbolAt (int i)
    {
      if (Symbol == null) {
        return null;
      }
      return Symbol[i];
    }

    public int Count
    {
      get
      {
        return _count;
      }
      set
      {
        if (Colset.Locked ()) {
          throw new Exception ("Tried to modify Count after cube was locked.");
        }
        _count = value;
      }
    }

    public void Lock ()
    {
      if (Event != null) {
        Event.Lock ();
      }
      if (Time != null) {
        Time.Lock ();
      }
      if (Symbol != null) {
        Symbol.Lock ();
      }
      if (Global != null) {
        Global.Lock ();
      }
      Colset.Lock ();
    }

    public bool Has (string name)
    {
      return Colset.Contains (name);
    }

    public bool Exists
    {
      get { return Colset.Count > 0; }
    }

    public void Write ()
    {
      ++_count;
    }

    public void Write (RCSymbolScalar s)
    {
      Write (-1, -1, new RCTimeScalar (new DateTime (0), RCTimeType.Timestamp), s);
    }

    public void Write (long e, RCSymbolScalar s)
    {
      // You will get an exception if these arrays have been locked from writing.
      if (Event != null) {
        Event.Write (e);
      }
      if (Symbol != null) {
        Symbol.Write (s);
      }
      ++_count;
    }

    public void Write (RCTimeScalar t, RCSymbolScalar s)
    {
      // You will get an exception if these arrays have been locked from writing.
      if (Time != null) {
        Time.Write (t);
      }
      if (Symbol != null) {
        Symbol.Write (s);
      }
      ++_count;
    }

    public void Write (long g, long e, RCTimeScalar t, RCSymbolScalar s)
    {
      if (Global != null) {
        Global.Write (g);
      }
      if (Event != null) {
        Event.Write (e);
      }
      if (Time != null) {
        Time.Write (t);
      }
      if (Symbol != null) {
        Symbol.Write (s);
      }
      ++_count;
    }

    public void Write (Timeline source, int i)
    {
      if (source.Global != null) {
        Global.Write (source.Global[i]);
      }
      if (source.Event != null) {
        Event.Write (source.Event[i]);
      }
      if (source.Time != null) {
        Time.Write (source.Time[i]);
      }
      if (source.Symbol != null) {
        Symbol.Write (source.Symbol[i]);
      }
      ++_count;
    }

    public Timeline Match ()
    {
      return new Timeline (new RCArray<string> (Colset));
    }

    internal void ReverseInPlace ()
    {
      if (Global != null) {
        Global.ReverseInPlace ();
      }
      if (Event != null) {
        Event.ReverseInPlace ();
      }
      if (Time != null) {
        Time.ReverseInPlace ();
      }
      if (Symbol != null) {
        Symbol.ReverseInPlace ();
      }
    }

    public int ColCount
    {
      get { return Colset.Count; }
    }
  }
}
