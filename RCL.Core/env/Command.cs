
using System;
using System.Text;
using System.IO;
using System.Threading;
using RCL.Kernel;

namespace RCL.Core
{
  public class Command
  {
    [RCVerb ("path")]
    public void EvalPath (RCRunner runner, RCClosure closure, RCSymbol right)
    {
      RCArray<string> result = new RCArray<string> (right.Count);
      for (int i = 0; i < right.Count; ++i)
      {
        result.Write (PathSymbolToString (right[i]));
      }
      runner.Yield (closure, new RCString (result));
    }

    public static string PathSymbolToString (RCSymbolScalar symbol)
    {
      object[] parts = symbol.ToArray ();
      string path = "";
      string zero = parts[0].ToString ();
      int startIndex = 0;
      if (zero == "home")
      {
        string home = Environment.GetEnvironmentVariable ("RCL_HOME");
        if (home == null) 
        {
          home = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
        }
        path += home;
        startIndex = 1;
      }
      //Need to handle windows drive letter.
      for (int i = startIndex; i < parts.Length; ++i)
      {
        path += Path.DirectorySeparatorChar + parts[i].ToString ();
      }
      return path;
    }
    [RCVerb ("load")]
    public void EvalLoad (
      RCRunner runner, RCClosure closure, RCString right)
    {
      string code = File.ReadAllText (right[0], Encoding.UTF8);
      //I want to change this to split lines.
      runner.Yield (closure, new RCString (code));
    }

    [RCVerb ("load")]
    public void EvalLoad (RCRunner runner, RCClosure closure, RCSymbol right)
    {
      //Need check for windows drive letter
      string path = PathSymbolToString (right[0]);
      string code = File.ReadAllText (path, Encoding.UTF8);
      runner.Yield (closure, new RCString (code));
    }

    protected long m_handle = -1;
    [RCVerb ("save")]
    public void EvalSave (RCRunner runner, RCClosure closure, RCSymbol left, RCString right)
    {
      Save (runner, closure, PathSymbolToString (left[0]), right.ToArray ());
    }
      
    [RCVerb ("save")]
    public void EvalSave (RCRunner runner, RCClosure closure, RCString left, RCString right)
    {
      Save (runner, closure, left[0], right.ToArray ());
    }

    protected void Save (RCRunner runner, RCClosure closure, string path, string[] lines)
    {
      //BRIAN READ THIS WHEN YOU GET BACK HERE.
      //Should not be doing sync io like this.
      //The least I can do is use a thread pool thread.
      //The endgame is for load, save, delete and so on to be replaced with
      //getf, putf, delf and lsf.
      //These operators should be properly async.
      //Also return RCValues just like getm, putm and so on.
      //Also use a symbols system for paths so programs do not contain RC dependent paths.
      //And provide path abstraction which should help with security.
      //getf, putf, delf should always store values in the text files.
      //Then we need some way to store binary files.
      //But I will still need some way to work with raw text files that might not be RC syntax.
      //So I guess I WILL need operators like save and load, huh.
      //And I also guess that those should work on string arrays as lines...
      //Then I could use writeAllLines and readAllLines to get around the terminal line issue.
      //But that would mean changing that parser to interpret string breaks as line breaks.
      //So, not today.
      WriteAllLinesBetter (path, lines);

      runner.Log.RecordDoc (runner, closure,
                            "save", Interlocked.Increment (ref m_handle), path, lines);
      //ideally this should return a symbol right?
      runner.Yield (closure, new RCString (path));
    }

    //http://stackoverflow.com/questions/11689337/net-file-writealllines-leaves-empty-line-at-the-end-of-file
    public static void WriteAllLinesBetter(string path, params string[] lines)
    {
      if (path == null)
      {
        throw new ArgumentNullException ("path");
      }
      if (lines == null)
      {
        throw new ArgumentNullException ("lines");
      }

      //using (var stream = File.OpenWrite (path))
      using (var stream = File.Open (path, FileMode.Create))
        using (StreamWriter writer = new StreamWriter (stream))
      {
        if (lines.Length > 0)
        {
          for (int i = 0; i < lines.Length - 1; i++)
          {
            writer.WriteLine (lines[i]);
          }
          writer.Write (lines[lines.Length - 1]);
        }
      }
    }

    [RCVerb ("loadbin")]
    public void EvalLoadbin (
      RCRunner runner, RCClosure closure, RCString right)
    {
      byte[] bytes = File.ReadAllBytes (right[0]);
      //I want to change this to split lines.
      runner.Yield (closure, new RCByte (bytes));
    }

    [RCVerb ("savebin")]
    public void EvalSavebin (RCRunner runner, RCClosure closure, RCString left, RCByte right)
    {
      //BRIAN READ THIS WHEN YOU GET BACK HERE.
      //Should not be doing sync io like this.
      //The least I can do is use a thread pool thread.
      File.WriteAllBytes (left[0], right.ToArray ());
      runner.Log.RecordDoc (runner, closure,
                            "save", Interlocked.Increment (ref m_handle), left[0], right);
      runner.Yield (closure, new RCString (left[0]));
    }

    [RCVerb ("delete")]
    public void EvalDelete (
      RCRunner runner, RCClosure closure, RCString right)
    {
      //It kind of sucks that if one file can not be deleted
      //it could leave the disk in an inconsistent state.
      //Todo: worry about it
      for (int i = 0; i < right.Count; ++i)
      {
        File.Delete (right[i]);
      }
      runner.Yield (closure, right);
    }

    /*
    [RCVerb ("list")]
    public void EvalList (
      RCRunner runner, RCClosure closure, RCString right)
    {
      RCArray<string> result = new RCArray<string> ();
      for (int i = 0; i < right.Count; ++i)
      {
        result.Write (Directory.GetFiles (right[i]));
        result.Write (Directory.GetDirectories (right[i]));
      }
      runner.Yield (closure, new RCString (result));
    }
    */

    [RCVerb ("cd")]
    public void EvalCd (
      RCRunner runner, RCClosure closure, RCString right)
    {
      if (right.Count > 1)
        throw new Exception ("cd can only change into one directory");

      Environment.CurrentDirectory = right[0];
      runner.Yield (closure, new RCString (Environment.CurrentDirectory));
    }

    [RCVerb ("pwd")]
    public void EvalPwd (
      RCRunner runner, RCClosure closure, RCBlock right)
    {
      runner.Yield (closure, new RCString (Environment.CurrentDirectory));
    }

    //Let's call this getarg to be more like getenv
    [RCVerb ("option")]
    public void EvalOptions (
      RCRunner runner, RCClosure closure, RCString left, RCString right)
    {
      RCValue result = runner.Argv.Options.Get (right[0]);
      if (result == null)
      {
        result = left;
      }
      runner.Yield (closure, result);
    }

    [RCVerb ("option")]
    public void EvalOptions (
      RCRunner runner, RCClosure closure, RCString right)
    {
      RCValue result = runner.Argv.Options.Get (right[0]);
      if (result == null)
      {
        throw new Exception ("No such option:" + right[0]);
      }
      runner.Yield (closure, result);
    }

    [RCVerb ("info")]
    public void EvalInfo (
      RCRunner runner, RCClosure closure, RCSymbol right)
    {
      if (right.Count > 1)
      {
        throw new Exception ("info can only provide one value at a time. info #help gives a list of valid values.");
      }
      Info (runner, closure, right[0].Part(0).ToString ());
    }

    [RCVerb ("info")]
    public void EvalInfo (
      RCRunner runner, RCClosure closure, RCString right)
    {
      if (right.Count > 1)
      {
        throw new Exception ("info can only provide one value at a time. info #help gives a list of valid values.");
      }
      Info (runner, closure, right[0]);
    }

    protected void Info (RCRunner runner, RCClosure closure, string value)
    {
      if (value == "arguments")
      {
        runner.Yield (closure, runner.Argv.Arguments);
      }
      else if (value == "options")
      {
        runner.Yield (closure, runner.Argv.Options);
      }
      else if (value == "directory")
      {
        runner.Yield (closure, new RCString (Environment.CurrentDirectory));
      }
      else if (value == "drives")
      {
        runner.Yield (closure, new RCString (Environment.GetLogicalDrives ()));
      }
      else if (value == "host")
      {
        runner.Yield (closure, new RCString (Environment.MachineName));
      }
      else if (value == "ending")
      {
        runner.Yield (closure, new RCString (Environment.NewLine));
      }
      else if (value == "os")
      {
        runner.Yield (closure, new RCString (Environment.OSVersion.VersionString));
      }
      else if (value == "help")
      {
        runner.Yield (closure, new RCString ("arguments", "options", "directory", "drives", "host", "end", "os", "help"));
      }
    }

    [RCVerb ("getenv")]
    public void EvalGetenv (
      RCRunner runner, RCClosure closure, RCString right)
    {
      RCArray<string> result = new RCArray<string> (right.Count);
      for (int i = 0; i < right.Count; ++i)
      {
        string variable = Environment.GetEnvironmentVariable (right[i]);
        if (variable == null)
        {
          throw new Exception ("No environment variable set:" + right[i]);
        }
        result.Write (variable);
      }
      runner.Yield (closure, new RCString (result));
    }

    [RCVerb ("getenv")]
    public void EvalGetenv (
      RCRunner runner, RCClosure closure, RCString left, RCString right)
    {
      RCArray<string> result = new RCArray<string> (right.Count);
      for (int i = 0; i < right.Count; ++i)
      {
        string variable = Environment.GetEnvironmentVariable (right[i]);
        if (variable == null)
        {
          variable = left[i];
        }
        result.Write (variable);
      }
      runner.Yield (closure, new RCString (result));
    }

    [RCVerb ("setenv")]
    public void EvalSetenv (
      RCRunner runner, RCClosure closure, RCString left, RCString right)
    {
      for (int i = 0; i < left.Count; ++i)
      {
        Environment.SetEnvironmentVariable (left[i], right[i]);
      }
      runner.Yield (closure, left);
    }

    public class Print
    {
      [RCVerb ("print")]
      public void EvalPrint (
        RCRunner runner, RCClosure closure, RCString right)
      {
        runner.Log.RecordDoc (runner, closure, "print", 0, "out", right);
        runner.Yield (closure, RCLong.Zero);
      }

      [RCVerb ("print")]
      public void EvalPrint (
        RCRunner runner, RCClosure closure, RCString left, RCString right)
      {
        runner.Log.RecordDoc (runner, closure, "print", 0, left[0], right);
        runner.Yield (closure, RCLong.Zero);
      }
    }

    [RCVerb ("module")]
    public void EvalModule (
      RCRunner runner, RCClosure closure, RCBlock right)
    {
      RCArray<RCLReference> references = new RCArray<RCLReference> ();
      RCBlock result = (RCBlock) right.Edit (runner, delegate (RCValue val)
      {
        RCLReference reference = val as RCLReference;
        if (reference != null)
        {
          RCLReference r = new RCLReference (reference.Name);
          references.Write (r);
          return r;
        }
        UserOperator op = val as UserOperator;
        if (op != null)
        {
          RCLReference r = new RCLReference (op.Name);
          references.Write (r);
          UserOperator outop = new UserOperator (r);
          outop.Init (op.Name, op.Left, op.Right);
          return outop;
        }
        else return null;
      });

      //Ugh there must be some better way than this.
      //But sometimes you just need to modify a value after allocation.
      //We can use the Lock() mechanism to tighten this up at least.
      //SetStatic will throw an exception if you call it after Lock().
      for (int i = 0; i < references.Count; ++i)
      {
        references[i].SetStatic (result);
      }

      runner.Yield (closure, result);
    }

    [RCVerb ("exit")]
    public void EvalExit (RCRunner runner, 
                          RCClosure closure, 
                          RCLong right)
    {
      runner.Log.RecordDoc (runner, closure, "runner", 0, "exit", right);
      runner.Exit ((int) right[0]);
    }

    /*
    Making this an operator did not work cause fucked up stuff happens when you yield
    after calling reset on the runner.
    [RCVerb ("reset")]
    public void EvalReset (
      RCRunner runner, RCClosure closure, RCLong right)
    {
      runner.Log.RecordDoc (runner, closure,
                            closure.Bot.Id, "runner", 0, "reset", right);
      runner.Reset ();
      //runner.Yield (closure, right);
    }
    */

    private static RCBlock m_options = null;
    public static void SetOptions (RCBlock options)
    {
      if (m_options != null)
      {
        throw new Exception ("Set options called more than once.");
      }
      m_options = options;
    }

    public void EvalOption (RCRunner runner, 
                            RCClosure closure, 
                            RCString right)
    {
      runner.Yield (closure, m_options.Get (right[0]));
    }
  }
}