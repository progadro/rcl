
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using RCL.Kernel;

namespace RCL.Core
{
  public class TcpCollector
  {
    public readonly RCRunner Runner;
    public readonly RCClosure Closure;
    public readonly RCSymbol Ids;

    protected readonly object _lock = new object ();
    protected Dictionary<RCSymbolScalar, RCValue> _results =
      new Dictionary<RCSymbolScalar, RCValue> ();

    public TcpCollector (RCRunner runner, RCClosure closure, RCSymbol ids)
    {
      if (runner == null) {
        throw new ArgumentNullException ("runner");
      }
      if (closure == null) {
        throw new ArgumentNullException ("closure");
      }
      if (ids == null) {
        throw new ArgumentNullException ("ids");
      }

      Runner = runner;
      Closure = closure;
      Ids = ids;
    }

    public void Accept (RCSymbolScalar id, RCValue message)
    {
      RCBlock result = null;
      lock (_lock)
      {
        _results.Add (id, message);
        // Console.Out.WriteLine("id:{0},Ids:{1}", id.ToString (), Ids.ToString());
        if (_results.Count >= Ids.Count) {
          foreach (RCValue val in _results.Values)
          {
            result = new RCBlock (result, "", ":", val);
          }
          // Console.Out.WriteLine ("Yielding {0}", result);
        }
      }
      if (result != null) {
        Runner.Yield (Closure, result);
      }
    }
  }
}
