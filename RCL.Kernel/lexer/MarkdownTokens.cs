
using System;

namespace RCL.Kernel
{
  public class MarkdownContentToken : RCTokenType
  {
    public override RCToken TryParseToken (string code,
                                           int start,
                                           int index,
                                           RCToken previous)
    {
      string text;
      int current = start;
      for (; current < code.Length; ++current)
      {
        //Console.Out.Write (code[current]);
        if (code[current] == '\r' || code[current] == '\n' ||
            code[current] == '[' || code[current] == '_' || code[current] == '*')
        {
          int length = current - start;
          if (length > 0)
          {
            text = code.Substring (start, length);
            return new RCToken (text, this, start, index);
          }
          else return null;
        }
      }
      text = code.Substring (start, current - start);
      return new RCToken (text, this, start, index);
      //throw new Exception ("No newline at end of content");
    }
    
    public override void Accept (RCParser parser, RCToken token)
    {
      parser.AcceptMarkdownContent (token);
    }
    
    public override string TypeName
    {
      get { return "MarkdownContentToken"; }
    }
  }

  public class MarkdownBeginBoldToken : RCTokenType
  {
    public override RCToken TryParseToken (string code,
                                           int start,
                                           int index,
                                           RCToken previous)
    {
      int length = LengthOfKeyword (code, start, "**");
      if (length > 0)
      {
        if (previous == null || previous.Text.EndsWith (" "))
        {
          return new RCToken ("**", this, start, index);  
        }
      }
      return null;
    }
    
    public override void Accept (RCParser parser, RCToken token)
    {
      parser.AcceptMarkdownBeginBold (token);
    }
    
    public override string TypeName
    {
      get { return "MarkdownBeginBoldToken"; }
    }
  }

  public class MarkdownEndBoldToken : RCTokenType
  {
    public override RCToken TryParseToken (string code,
                                           int start,
                                           int index,
                                           RCToken previous)
    {
      int length = LengthOfKeyword (code, start, "**");
      if (length > 0)
      {
        //EndsWith here is an approximation, needs to handle newlines
        //and also bold, italic tokens
        if (previous != null && !previous.Text.EndsWith (" "))
        {
          return new RCToken ("**", this, start, index);  
        }
      }
      return null;
    }
    
    public override void Accept (RCParser parser, RCToken token)
    {
      parser.AcceptMarkdownEndBold (token);
    }
    
    public override string TypeName
    {
      get { return "MarkdownEndBoldToken"; }
    }
  }

  public class MarkdownBeginItalicToken : RCTokenType
  {
    public override RCToken TryParseToken (string code,
                                           int start,
                                           int index,
                                           RCToken previous)
    {
      int length = LengthOfKeyword (code, start, "_");
      if (length > 0)
      {
        if (previous == null || previous.Text.EndsWith (" "))
        {
          return new RCToken ("_", this, start, index);  
        }
      }
      return null;
    }
    
    public override void Accept (RCParser parser, RCToken token)
    {
      parser.AcceptMarkdownBeginItalic (token);
    }
    
    public override string TypeName
    {
      get { return "MarkdownBeginItalicToken"; }
    }
  }

  public class MarkdownEndItalicToken : RCTokenType
  {
    public override RCToken TryParseToken (string code,
                                           int start,
                                           int index,
                                           RCToken previous)
    {
      int length = LengthOfKeyword (code, start, "_");
      if (length > 0)
      {
        if (previous != null && !previous.Text.EndsWith (" "))
        {
          return new RCToken ("_", this, start, index);  
        }
      }
      return null;
    }
    
    public override void Accept (RCParser parser, RCToken token)
    {
      parser.AcceptMarkdownEndItalic (token);
    }
    
    public override string TypeName
    {
      get { return "MarkdownBeginItalicToken"; }
    }
  }

  public class MarkdownLinkToken : RCTokenType
  {
    public override RCToken TryParseToken (string code,
                                           int start,
                                           int index,
                                           RCToken previous)
    {
      int current = start;
      if (code[current] == '!') ++current;
      if (code[current] != '[') return null;
      for (; current < code.Length; ++current)
      {
        if (code[current] == ']')
        {
          ++current;
          if (code[current] != '(') return null;
          for (; current < code.Length; ++current)
          {
            if (code[current] == ')') 
            {
              ++current;
              string text = code.Substring (start, current - start);
              return new RCToken (text, this, start, index);
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

  public class MarkdownLiteralLinkToken : RCTokenType
  {
    public override RCToken TryParseToken (string code,
                                           int start,
                                           int index,
                                           RCToken previous)
    {
      int current = start;
      if (code[current] != '<') return null;
      for (; current < code.Length; ++current)
      {
        if (code[current] == '>')
        {
          ++current;
          string text = code.Substring (start, current - start);
          return new RCToken (text, this, start, index);
        }
      }
      return null;
    }
    
    public override void Accept (RCParser parser, RCToken token)
    {
      parser.AcceptMarkdownLiteralLink (token);
    }
    
    public override string TypeName
    {
      get { return "MarkdownLiteralLink"; }
    }
  }

  public class MarkdownHeaderToken : RCTokenType
  {
    public override RCToken TryParseToken (string code,
                                           int start,
                                           int index,
                                           RCToken previous)
    {
      int current = start;
      int hcount = 0;
      while (current < code.Length && code[current] == '#')
      {
        ++hcount;
        ++current;
      }
      if (hcount == 0) return null;
      if (hcount > 6) return null;
      if (current < code.Length && code[current] == ' ')
      {
        for (; current < code.Length; ++current)
        {
          if (code[current] == '\r' || code[current] == '\n')
          {
            string text = code.Substring (start, current - start);
            return new RCToken (text, this, start, index);
          }
        }
      }
      return null;
    }
    
    public override void Accept (RCParser parser, RCToken token)
    {
      parser.AcceptMarkdownHeader (token);
    }
    
    public override string TypeName
    {
      get { return "MarkdownHeaderToken"; }
    }
  }

  public class MarkdownBlockquoteToken : RCTokenType
  {
    public override RCToken TryParseToken (string code,
                                           int start,
                                           int index,
                                           RCToken previous)
    {
      //A single line of a markdown blockquote
      int current = start;
      int quoteLevel = 0;
      while (current < code.Length && (code[current] == '>' || code[current] == ' '))
      {
        if (code[current] == '>')
        {
          ++quoteLevel;
        }
        ++current;
      }
      if (quoteLevel == 0) return null;
      for (; current < code.Length; ++current)
      {
        if (code[current] == '\r' || code[current] == '\n')
        {
          string text = code.Substring (start, current - start);
          return new RCToken (text, this, start, index);
        }
      }
      return null;
    }
    
    public override void Accept (RCParser parser, RCToken token)
    {
      parser.AcceptMarkdownBlockquote (token);
    }
    
    public override string TypeName
    {
      get { return "MarkdownBlockquoteToken"; }
    }
  }
}