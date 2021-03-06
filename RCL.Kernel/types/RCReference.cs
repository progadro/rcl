﻿
using System;
using System.Text;
using System.Collections.Generic;

namespace RCL.Kernel
{
  public class RCReference : RCValue
  {
    protected static readonly Dictionary<string, Type> _types = new Dictionary<string, Type> ();
    protected static readonly Dictionary<Type, string> _codes = new Dictionary<Type, string> ();

    public readonly string Name;
    public readonly RCArray<string> Parts;
    protected internal RCBlock _static;

    public RCReference (string name)
    {
      Name = name;
      Parts = RCName.MultipartName (name, '.');
      Parts.Lock ();
    }

    public RCReference (string[] parts)
    {
      Parts = new RCArray<string> (parts);
      Parts.Lock ();
      Name = "";
      if (parts.Length > 1) {
        for (int i = 0; i < parts.Length; ++i)
        {
          Name += parts[i];
          if (i < parts.Length - 1) {
            Name += '.';
          }
        }
      }
      else {
        Name = parts[0];
      }
    }

    public override bool IsReference
    {
      get { return true; }
    }

    public override bool ArgumentEval
    {
      get { return true; }
    }

    public override string TypeName
    {
      get { return RCValue.REFERENCE_TYPENAME; }
    }

    public override char TypeCode
    {
      get { return 'r'; }
    }

    public override void Eval (RCRunner runner, RCClosure closure)
    {
      RCL.Kernel.Eval.DoEval (runner, closure, this);
    }

    public static string Delimit (RCArray<string> parts, string delimeter)
    {
      StringBuilder builder = new StringBuilder ();
      for (int i = 0; i < parts.Count; ++i)
      {
        builder.Append (parts[i]);
        if (i < parts.Count - 1) {
          builder.Append (delimeter);
        }
      }
      return builder.ToString ();
    }

    public override void Format (StringBuilder builder, RCFormat args, int level)
    {
      RCL.Kernel.Format.DoFormat (this, builder, args, null, level);
    }

    public override void Format (StringBuilder builder, RCFormat args, RCColmap colmap, int level)
    {
      RCL.Kernel.Format.DoFormat (this, builder, args, colmap, level);
    }

    public override void Cubify (RCCube target, Stack<object> names)
    {
      object[] array = names.ToArray ();
      System.Array.Reverse (array);
      RCSymbolScalar symbol = RCSymbolScalar.From (array);
      target.WriteCell (this.TypeCode.ToString (), symbol, Name, -1, true, false);
      target.Write (symbol);
    }

    public void SetStatic (RCBlock context)
    {
      if (IsLocked) {
        throw new Exception (
                "Attempted to modify a locked instance of RCReference.");
      }

      _static = context;
    }

    public override RCOperator AsOperator (RCActivator activator, RCValue left, RCValue right)
    {
      return activator.New (Name, left, right);
    }

    public override void ToByte (RCArray<byte> result)
    {
      Binary.WriteReference (result, this);
    }

    public override int Count
    {
      get { return 1; }
    }
  }
}
