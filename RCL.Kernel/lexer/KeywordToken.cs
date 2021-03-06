
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Globalization;

namespace RCL.Kernel
{
  public abstract class KeywordLexer : RCTokenType
  {
    protected string[] _keywords;
    public KeywordLexer (params string[] keywords)
    {
      if (keywords == null) {
        throw new ArgumentNullException ("keywords");
      }
      _keywords = keywords;
    }

    public override RCToken TryParseToken (string text,
                                           int start,
                                           int index,
                                           int line,
                                           RCToken
                                           previous)
    {
      int length = LengthOfOperator (text, start);
      if (length < 0) {
        return null;
      }
      string opChar = text.Substring (start, length);
      return new RCToken (opChar, this, start, index, line, 0);
    }

    protected int LengthOfOperator (string text, int start)
    {
      for (int i = 0; i < _keywords.Length; ++i)
      {
        int length = LengthOfKeyword (text, start, _keywords[i]);
        // Make sure we don't include == as an evaluator, it should be an operator.
        if (length > 0) {
          return length;
        }
      }
      return -1;
    }
  }
}
