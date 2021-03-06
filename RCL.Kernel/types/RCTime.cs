
using System;

namespace RCL.Kernel
{
  /// <summary>
  /// Represents dates, times, and quantities of time in RCL.
  /// </summary>
  public class RCTime : RCVector<RCTimeScalar>
  {
    /// <summary>
    /// The zero-count vector.
    /// </summary>
    public static RCTime Empty = new RCTime ();

    /// <summary>
    /// The timezone to use for display purposes only.
    /// Note this is a non-volatile multithreaded read.
    /// The intent is to avoid slowing down FormatScalar on the memory barrier.
    /// A slowdown would be worse than having a stale read since displayTimezone is
    /// expected to be set at program launch and not changed.
    /// </summary>
    public static TimeZoneInfo DisplayTimeZone = TimeZoneInfo.Utc;

    /// <summary>
    /// Defines formats for each type of RCTimeScalar.
    /// </summary>
    public static readonly string[] DEFAULT_FORMATS = new string[] {
      "yyyy.MM.dd",
      "HH:mm:ss",
      "yyyy.MM.dd HH:mm:ss",
      "yyyy.MM.dd HH:mm:ss.ffffff",
    };

    /// <summary>
    /// Defines formats for each type of RCTimeScalar.
    /// </summary>
    public static readonly string[] ALLOWED_FORMATS = new string[] {
      // date
      "yyyy.MM.dd",
      // daytime
      "HH:mm",
      "HH:mm:ss",
      // datetime
      "yyyy.MM.dd HH:mm",
      "yyyy.MM.dd HH:mm:ss",
      // timestamp
      "yyyy.MM.dd HH:mm:ss.ffffff",
      "yyyy.MM.dd HH:mm:ss.fffffff",
      "yyyy.MM.dd HH:mm:ss.fffff",
      "yyyy.MM.dd HH:mm:ss.ffff",
      "yyyy.MM.dd HH:mm:ss.fff",
      "yyyy.MM.dd HH:mm:ss.ff",
      "yyyy.MM.dd HH:mm:ss.f",
    };

    public static readonly RCTimeType[] ALLOWED_FORMAT_TYPES = new RCTimeType[] {
      RCTimeType.Date,
      RCTimeType.Daytime,
      RCTimeType.Daytime,
      RCTimeType.Datetime,
      RCTimeType.Datetime,
      RCTimeType.Timestamp,
      RCTimeType.Timestamp,
      RCTimeType.Timestamp,
      RCTimeType.Timestamp,
      RCTimeType.Timestamp,
      RCTimeType.Timestamp,
      RCTimeType.Timestamp
    };

    /// <summary>
    /// Constructor for inline use.
    /// </summary>
    public RCTime (params RCTimeScalar[] data) : base (data) {}

    /// <summary>
    /// Constructor for programmatic use.
    /// </summary>
    public RCTime (RCArray<RCTimeScalar> data) : base (data) {}

    /// <summary>
    /// The ScalarType of the vector is used for operator dispatch.
    /// </summary>
    public override Type ScalarType {
      get { return typeof (RCTimeScalar); }
    }

    /// <summary>
    /// Equality for time data.
    /// </summary>
    public override bool ScalarEquals (RCTimeScalar x, RCTimeScalar y)
    {
      return x.Ticks == y.Ticks && x.Type == y.Type;
    }

    /// <summary>
    /// Some scalar types can be identified by a suffix. Not this one.
    /// </summary>
    public override string Suffix
    {
      get { return ""; }
    }

    /// <summary>
    /// Single character mnemonic for the time type.
    /// </summary>
    public override char TypeCode
    {
      get { return 't'; }
    }

    /// <summary>
    /// In-language name for the type.
    /// </summary>
    public override string TypeName
    {
      get { return RCValue.TIME_TYPENAME; }
    }

    /// <summary>
    /// Why is this set to -1? Did we forget to implement binary for the time type?
    /// </summary>
    public override int SizeOfScalar
    {
      get { return -1; }
    }

    /// <summary>
    /// Conversion of time scalars to strings.
    /// </summary>
    public override string ScalarToString (string format, RCTimeScalar scalar)
    {
      return FormatScalar (format, scalar);
    }

    /// <summary>
    /// Standard time formatting method.
    /// </summary>
    public static string FormatScalar (string format, RCTimeScalar scalar)
    {
      if (format == null) {
        if (scalar.Type == RCTimeType.Timespan) {
          TimeSpan ts = new TimeSpan (scalar.Ticks);
          int micros = ((int) (scalar.Ticks % TimeSpan.TicksPerSecond)) / 10;
          string result = string.Format ("{0}.{1:00}:{2:00}:{3:00}.{4:000000}",
                                         ts.Days,
                                         Math.Abs (ts.Hours),
                                         Math.Abs (ts.Minutes),
                                         Math.Abs (ts.Seconds),
                                         Math.Abs (micros));
          return result;
        }
        else if (scalar.Type == RCTimeType.Date) {
          return new DateTime (scalar.Ticks).ToString (DEFAULT_FORMATS[(int) scalar.Type]);
        }
        else {
          DateTime displayTime = TimeZoneInfo.ConvertTimeFromUtc (new DateTime (scalar.Ticks),
                                                                  RCTime.DisplayTimeZone);
          return displayTime.ToString (DEFAULT_FORMATS[(int) scalar.Type]);
        }
      }
      else {
        if (scalar.Type == RCTimeType.Timespan) {
          throw new NotImplementedException (
                  "Custom formats for RCTimeType.Timespan are not implemented. Please fix.");
        }
        else if (scalar.Type == RCTimeType.Date) {
          return new DateTime (scalar.Ticks).ToString (format);
        }
        else {
          DateTime displayTime = TimeZoneInfo.ConvertTimeFromUtc (new DateTime (scalar.Ticks),
                                                                  RCTime.DisplayTimeZone);
          return displayTime.ToString (format);
        }
      }
    }

    /// <summary>
    /// Serialize the time data.
    /// </summary>
    public override void ToByte (RCArray<byte> result)
    {
      Binary.WriteVectorTime (result, this);
    }

    /// <summary>
    /// Extend the vector with boxing (expensive).
    /// </summary>
    public override void Write (object box)
    {
      _data.Write ((RCTimeScalar) box);
    }
  }
}
