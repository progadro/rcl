
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Globalization;

namespace RCL.Kernel
{
  /// <summary>
  /// Recognizer for content between xml brackets.
  /// </summary>
  public class XMLContentToken : RCTokenType
  {
    public override RCToken TryParseToken (string code, int startPos, int index, int line, RCToken previous)
    {
      if (previous == null)
      {
        return null;
      }

      if (!previous.Text.Equals (">"))
      {
        return null;
      }

      for (int current = startPos; current < code.Length; ++current)
      {
        if (code[current] == '<')
        {
          if (code[current + 1] != '/')
          {
            return null;
          }
          if (current > startPos)
          {
            return new RCToken (code.Substring (startPos, current - startPos), this, startPos, index, line, 0);
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
      //Need to handle xml escape characters here, this is incomplete.
      return token.Text;
    }

    public override string TypeName
    {
      get { return "xmlcontent"; }
    }
  }

  /// <summary>
  /// Recognizer for content between xml brackets.
  /// </summary>
  public class XMLDeclarationToken : RCTokenType
  {
    public override RCToken TryParseToken (string code, int startPos, int index, int line, RCToken previous)
    {
      int current = startPos;
      if (code[current] != '<') return null;
      ++current;
      if (code[current] != '?') return null;
      ++current;
      while (code[current] != '?')
      {
        ++current;
      }
      ++current;
      if (code[current] == '>')
      {
        string result = code.Substring (startPos, current - startPos);
        return new RCToken (result, this, startPos, index, line, 0);
      }
      return null;
    }

    public override void Accept (RCParser parser, RCToken token)
    {
      parser.AcceptXmlDeclaration (token);
    }

    public override string ParseString (RCLexer lexer, RCToken token)
    {
      return token.Text;
    }

    public override string TypeName
    {
      get { return "xmldeclaration"; }
    }
  }
}
