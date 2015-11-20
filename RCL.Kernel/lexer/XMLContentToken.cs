
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Globalization;

namespace RCL.Kernel
{
  public class XMLContentToken : RCTokenType
  {
    public override RCToken TryParseToken (
      string code, int startPos, int index, RCToken previous)
    {
      if (previous == null)
        return null;
      if (!previous.Text.Equals (">"))
        return null;

      for (int current = startPos; current < code.Length; ++current)
      {
        if (code[current] == '<')
        {
          if (code[current + 1] != '/')
            return null;
          if (current > startPos)
          {
            return new RCToken (
              code.Substring (startPos, current - startPos), this, startPos, index);
          }
          else return null;
        }
      }

      return null;
    }

    public override void Accept (RCParser parser, RCToken token)
    {
      parser.AcceptXmlContent (token);
    }

    public override string ParseString (RCLexer lexer, RCToken token)
    {
      //Need to handle xml escape characters.
      return token.Text;
    }

    public override string TypeName
    {
      get { return "xmlcontent"; }
    }
  }
}