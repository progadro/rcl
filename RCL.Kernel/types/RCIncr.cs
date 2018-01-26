
using System;
using System.Text;
using System.Globalization;
using System.Collections.Generic;

namespace RCL.Kernel
{  
  public enum RCIncrScalar
  {
    Increment = 0,
    Decrement = 1,
    Delete = 2
  }

  public class RCIncr : RCVector<RCIncrScalar>
  {
    public static readonly RCIncr Empty = new RCIncr ();
    public RCIncr (params RCIncrScalar[] data) : base (data) { }
    public RCIncr (RCArray<RCIncrScalar> data) : base (data) { }

    public override bool ScalarEquals (RCIncrScalar x, RCIncrScalar y)
    {
      return x == y;
    }

    public override string ScalarToString (RCIncrScalar scalar)
    {
      return FormatScalar (scalar);
    }

    public static string FormatScalar (RCIncrScalar scalar)
    {
      switch (scalar)
      {
        case RCIncrScalar.Increment : return "++";
        case RCIncrScalar.Decrement : return "+-";
        case RCIncrScalar.Delete    : return "+~";
      }
      throw new InvalidOperationException ();
    }

    public override string Suffix
    {
      get { return ""; }
    }

    public override char TypeCode
    {
      get { return 'n'; }
    }

    public override string TypeName
    {
      get { return "incr"; }
    }

    public override int SizeOfScalar
    {
      get { return 1; }
    }

    public override Type ScalarType
    {
      get { return typeof (RCIncrScalar); }
    }

    public override void ToByte (RCArray<byte> result)
    {
      Binary.WriteVectorIncr (result, this);
    }

    public override void Write (object box)
    {
      m_data.Write ((RCIncrScalar) box);
    }
  }
}
