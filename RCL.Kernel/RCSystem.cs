﻿
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Reflection;

namespace RCL.Kernel
{
  /// <summary>
  /// There are a few things are accessible on a global basis: the log, the arguments to
  /// Program, and the activator.
  /// It is possible to have multiple runners operating within an appDomain, so a RCRunner
  /// instance is not available this way.
  /// </summary>
  public class RCSystem
  {
    static RCSystem ()
    {
      Args = RCLArgv.Instance;
      // Setup the logger first in case the Activator throws an exception
      Log = new RCLogger (Args.NoKeys, Args.Show, Args.Hide);
      Log.SetVerbosity (Args.OutputEnum);
      Activator = RCActivator.CreateDefault ();
    }

    public static RCLArgv Args;
    public static RCLogger Log;
    public static RCActivator Activator;

    public static bool IsMono ()
    {
      bool result = false;
      if (Type.GetType ("Mono.Runtime") != null) {
        result = true;
      }
      return result;
    }

    public static RCValue Parse (string code)
    {
      bool fragment;
      return Parse (code, out fragment);
    }

    public static RCValue Parse (string code, out bool fragment)
    {
      RCParser parser = new RCLParser (Activator);
      RCArray<RCToken> tokens = new RCArray<RCToken> ();
      parser.Lex (code, tokens);
      RCValue result = parser.Parse (tokens, out fragment, canonical: false);
      RCAssert.IsNotNull (result, "The result of parser.Parse() was null.");
      return result;
    }

    public static RCValue ParseJSON (string code)
    {
      RCParser parser = new JSONParser ();
      RCArray<RCToken> tokens = new RCArray<RCToken> ();
      parser.Lex (code, tokens);
      bool fragment;
      RCValue result = parser.Parse (tokens, out fragment, canonical: false);
      RCAssert.IsNotNull (result, "The result of parser.Parse() was null.");
      return result;
    }

    public static void Reconfigure (RCLArgv args)
    {
      Args = args;
      Log = new RCLogger (Args.NoKeys, Args.Show, Args.Hide);
      Log.SetVerbosity (Args.OutputEnum);
    }

    [RCVerb ("operators")]
    public void Operators (RCRunner runner, RCClosure closure, RCBlock right)
    {
      RCCube result = new RCCube ("S");
      result.ReserveColumn ("module");
      result.ReserveColumn ("name");
      result.ReserveColumn ("method");
      result.ReserveColumn ("left");
      result.ReserveColumn ("right");
      foreach (KeyValuePair<RCActivator.OverloadKey,
                            RCActivator.OverloadValue> kv in Activator._dispatch)
      {
        RCSymbolScalar sym;
        if (kv.Key.Left == null) {
          sym = RCSymbolScalar.From ("operator",
                                     kv.Value.Module.Name,
                                     kv.Key.Name,
                                     RCValue.TypeNameForType (kv.Key.Right));
        }
        else {
          sym = RCSymbolScalar.From ("operator",
                                     kv.Value.Module.Name,
                                     kv.Key.Name,
                                     RCValue.TypeNameForType (kv.Key.Left),
                                     RCValue.TypeNameForType (kv.Key.Right));
        }
        result.WriteCell ("name", sym, kv.Key.Name);
        result.WriteCell ("module", sym, kv.Value.Module.Name);
        result.WriteCell ("method", sym, kv.Value.Implementation.Name);
        if (kv.Key.Left != null) {
          result.WriteCell ("left", sym, RCValue.TypeNameForType (kv.Key.Left));
        }
        result.WriteCell ("right", sym, RCValue.TypeNameForType (kv.Key.Right));
        result.Axis.Write (sym);
      }
      runner.Yield (closure, result);
    }

    /*
       /// <summary>
       /// This is a way to listen on debug messages in an isolated appdomain
       /// We don't currently need it because we aren't using Debug.Write,
       /// but that should change.
       /// </summary>
       private class DelegateTraceListener : TraceListener
       {
       private Action<string> _write;

       public DelegateTraceListener (Action<string> write)
       {
        _write = write;
       }

       public override void Write (string message)
       {
        _write (message);
       }

       public override void WriteLine (string message)
       {
        Write (message + Environment.NewLine);
       }
       }

       public class CrossDomainTraceHelper : MarshalByRefObject
       {
       private CrossDomainTraceHelper _parentDomain;

       public CrossDomainTraceHelper () {}

       public static void StartListening (AppDomain domain)
       {
        var listenerType = typeof (CrossDomainTraceHelper);
        // Create a remote instance
        var remoteHelper = (CrossDomainTraceHelper) domain.CreateInstanceAndUnwrap (
          listenerType.Assembly.FullName,
          listenerType.FullName);
        // Create a local instance
        var localHelper = new CrossDomainTraceHelper ();
        // Register the local helper in the remote domain
        remoteHelper.Register (localHelper);
       }

       private void Register (CrossDomainTraceHelper parentDomain)
       {
        // Store the parent domain to pass messages to later
        _parentDomain = parentDomain;
        // Create and register the delegate trace listener
        var listener = new DelegateTraceListener (Write);
        Debug.Listeners.Add (listener);
       }

       private void Write (string message)
       {
        // Send the message to the parent domain
        _parentDomain.RemoteWrite (message);
       }

       private void RemoteWrite (string message)
       {
        Debug.Write (message);
       }
       }
     */
  }
}
