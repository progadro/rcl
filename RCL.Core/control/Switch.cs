
using System;
using System.Collections.Generic;
using System.Threading;
using RCL.Kernel;

namespace RCL.Core
{
  public class Switch : RCOperator
  {
    protected delegate RCValue Picker<T>(T val);

    [RCVerb ("switch")]
    public void EvalSwitch (
      RCRunner runner, RCClosure closure, RCBoolean left, RCBlock right)
    {
      Picker<bool> picker = delegate (bool val)
      {
        long i = val ? 0 : 1;
        return i >= right.Count ? RCBlock.Empty : right.Get (i);
      };
      DoSwitch<bool> (runner, closure, left, right, picker);
    }

    [RCVerb ("switch")]
    public void EvalSwitch (
      RCRunner runner, RCClosure closure, RCByte left, RCBlock right)
    {
      Picker<byte> picker = delegate (byte val)
      {
        long i = val < 0 ? 1 : 0;
        return i >= right.Count ? RCBlock.Empty : right.Get (i);
      };
      DoSwitch<byte> (runner, closure, left, right, picker);
    }

    [RCVerb ("switch")]
    public void EvalSwitch (
      RCRunner runner, RCClosure closure, RCLong left, RCBlock right)
    {
      //What on earth was I thinking... we need to make this work.
      Picker<long> picker = delegate (long val)
      {
        return right.Get (val);
      };
      DoSwitch<long> (runner, closure, left, right, picker);
    }

    [RCVerb ("switch")]
    public void EvalSwitch (
      RCRunner runner, RCClosure closure, RCSymbol left, RCBlock right)
    {
      Picker<RCSymbolScalar> picker = delegate (RCSymbolScalar val)
      {
        if (val.Length > 1)
        {
          throw new Exception (
            "switch only supports block lookups using tuples of count 1.  But this could change.");
        }
        RCValue code = right.Get ((string) val.Key);
        if (code == null) 
        {
          code = RCBlock.Empty;
        }
        return code;
      };
      DoSwitch<RCSymbolScalar> (runner, closure, left, right, picker);
    }

    [RCVerb ("switch")]
    public void EvalSwitch (
      RCRunner runner, RCClosure closure, RCString left, RCBlock right)
    {
      Picker<string> picker = delegate (string val)
      {
        RCValue code = right.Get (val);
        if (code == null)
        {
          code = RCBlock.Empty;
        }
        return code;
      };
      DoSwitch<string> (runner, closure, left, right, picker);
    }

    [RCVerb ("then")]
    public void EvalThen (RCRunner runner, RCClosure closure, RCBoolean left, RCBlock right)
    {
      if (left[0])
      {
        int i = closure.Index - 2;
        if (i < left.Count)
        {
          RCClosure child = new RCClosure (closure,
                                           closure.Bot,
                                           right,
                                           closure.Left,
                                           RCBlock.Empty,
                                           0);
          right.Eval (runner, child);
        }
        else
        {
          runner.Yield (closure, closure.Parent.Result);
        }
      }
      else
      {
        runner.Yield (closure, RCBoolean.False);
      }
    }

    protected virtual void DoSwitch<T> (RCRunner runner,
                                        RCClosure closure,
                                        RCVector<T> left,
                                        RCBlock right,
                                        Picker<T> picker)
    {
      int i = closure.Index - 2;
      if (i < left.Count)
      {
        RCValue code = picker (left[i]);
        RCClosure child = new RCClosure (closure,
                                         closure.Bot,
                                         code,
                                         closure.Left,
                                         RCBlock.Empty,
                                         0);
        code.Eval (runner, child);
      }
      else
      {
        runner.Yield (closure, closure.Parent.Result);
      }
    }

    //This higher order thingy needs to go away it makes no sense.
    public override bool IsHigherOrder ()
    {
      return true;
    }

    public override bool IsLastCall (RCClosure closure, RCClosure arg)
    {
      if (arg == null)
      {
        return base.IsLastCall (closure, arg);
      }
      if (!base.IsLastCall (closure, arg))
      {
        return false;
      }
      bool isBeforeLastCall = arg.Code.IsBeforeLastCall (arg);
      return isBeforeLastCall;
    }
  }
}
