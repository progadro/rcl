
using System;
using System.Text;
using System.Globalization;
using System.Collections.Generic;

namespace RCL.Kernel
{
  public class RCSymbol : RCVector<RCSymbolScalar>
  {
    public static RCSymbol Empty = new RCSymbol ();
    public static RCSymbol Wild = new RCSymbol (RCSymbolScalar.Empty);
    public RCSymbol (params RCSymbolScalar[] data) : base (data) {}
    public RCSymbol (RCArray<RCSymbolScalar> data) : base (data) {}

    public override Type ScalarType {
      get { return typeof (RCSymbolScalar); }
    }

    public override bool ScalarEquals (RCSymbolScalar x, RCSymbolScalar y)
    {
      return x.Equals (y);
    }

    public override string Suffix
    {
      get { return ""; }
    }

    public override char TypeCode
    {
      get { return 'y'; }
    }

    public override string TypeName
    {
      get { return "symbol"; }
    }

    public override int SizeOfScalar
    {
      get { return -1; }
    }

    public override string ScalarToString (string format, RCSymbolScalar scalar)
    {
      return scalar.ToString ();
    }

    public override string ScalarToCsvString (string format, RCSymbolScalar scalar)
    {
      return scalar.ToCsvString ();
    }

    public static string FormatScalar (RCSymbolScalar scalar)
    {
      return scalar.ToString ();
    }

    public override void ToByte (RCArray<byte> result)
    {
      Binary.WriteVectorSymbol (result, this);
    }

    public override void Write (object box)
    {
      _data.Write ((RCSymbolScalar) box);
    }
  }
}
