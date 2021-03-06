﻿
namespace RCL.Kernel
{
  public class MarkdownLinkToken : RCTokenType
  {
    public override RCToken TryParseToken (string code,
                                           int start,
                                           int index,
                                           int line,
                                           RCToken previous)
    {
      int current = start;
      if (code[current] == '!')
      {
        ++current;
      }
      if (code[current] != '[')
      {
        return null;
      }
      for (; current < code.Length; ++current)
      {
        if (code[current] == ']')
        {
          ++current;
          if (code[current] != '(')
          {
            return null;
          }
          for (; current < code.Length; ++current)
          {
            if (code[current] == ')')
            {
              ++current;
              string text = code.Substring (start, current - start);
              return new RCToken (text, this, start, index, line, 0);
            }
          }
          return null;
        }
      }
      return null;
    }

    public override void Accept (RCParser parser, RCToken token)
    {
      parser.AcceptMarkdownLink (token);
    }

    public override string TypeName
    {
      get { return "MarkdownLink"; }
    }
  }
}