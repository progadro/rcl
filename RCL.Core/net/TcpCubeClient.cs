
using System;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using RCL.Kernel;

namespace RCL.Core
{
  /* WARNING - This code was part of an experiment that never really worked */
  public class TcpCubeClient : Tcp.Client
  {
    protected string _path = "";
    protected DirectoryInfo _dir;
    protected Dictionary<string, FileStream> _files;
    protected long _id = 0;
    protected long _handle;

    public TcpCubeClient (long handle, RCSymbolScalar right)
    {
      object[] parts = right.ToArray ();
      for (int i = 1; i < parts.Length; ++i)
      {
        Path.Combine (_path, (string) parts[i]);
      }
      _dir = new DirectoryInfo (_path);
      _files = new Dictionary<string, FileStream> ();
      _handle = handle;
    }

    public override void Open (RCRunner runner, RCClosure closure)
    {
      // Once there are files being written to we would want to open them here.
      // Technically this should use an async method to check for existence.
      if (!_dir.Exists) {
        _dir.Create ();
      }

      FileInfo[] files = _dir.GetFiles ();
      for (int i = 0; i < files.Length; ++i) {
        _files.Add (files[i].Name, files[i].Open (FileMode.Open, FileAccess.Read));
      }

      runner.Yield (closure, new RCLong (_handle));
    }

    public override void Close (RCRunner runner, RCClosure closure)
    {
      throw new NotImplementedException ();
    }

    public override TcpSendState Send (RCRunner runner, RCClosure closure, RCBlock message)
    {
      string verb = (string) ((RCSymbol) message.Get ("verb"))[0].Part (0);
      if (verb.Equals ("read")) {
        RCSymbol symbol = ((RCSymbol) message.Get ("symbol"));
        RCLong rows = ((RCLong) message.Get ("rows"));
        long id = Interlocked.Increment (ref _id);
        ReadFromFiles (runner, closure, symbol, rows);
        return new TcpSendState (_handle, id, message);
      }
      else if (verb.Equals ("write")) {
        throw new NotImplementedException ();
        /*
           RCCube cube = (RCCube) message.Get ("cube");
           if (cube != null)
           {
           long id = Interlocked.Increment (ref _id);
           WriteToFiles (runner, closure, cube);
           return new SendState (_handle, id, message);
           }
           RCBlock block = (RCBlock) message.Get ("block");
           if (block != null)
           {
           RCSymbol symbol = (RCSymbol) message.Get ("symbol");
           long id = Interlocked.Increment (ref _id);
           WriteToFiles (runner, closure, symbol, block);
           return new SendState (_handle, id, message);
           }
           throw new Exception ("verb:#write requires a block or a cube");
         */
      }
      else {
        throw new Exception ("Unknown verb:" + verb);
      }
    }

    protected virtual void WriteToFiles (RCRunner runner, RCClosure closure, RCValue val)
    {}

    protected virtual void WriteToFiles (RCRunner runner,
                                         RCClosure closure,
                                         RCSymbol symbol,
                                         RCBlock data)
    {
      for (int i = 0; i < data.Count; ++i)
      {
        FileStream stream = null;
        RCBlock column = data.GetName (i);
        if (!_files.TryGetValue (column.Name, out stream)) {
          string path = Path.Combine (_dir.Name, column.Name);
          stream = new FileStream (path,
                                   FileMode.CreateNew,
                                   FileAccess.Write);
          _files[column.Name] = stream;
        }
        RCArray<byte> array = new RCArray<byte> ();
        column.Value.ToByte (array);
        // throw new NotImplementedException ("This is where it's at baby, get er done.");
        // RCAsyncState state = new RCAsyncState (runner, closure, stream);
        // stream.BeginWrite (array, 0, array.Length, new AsyncCallback (EndWrite),
        // state);
      }
    }

    protected void EndWrite (IAsyncResult result)
    {
      RCAsyncState state = (RCAsyncState) result.AsyncState;
      try
      {
        FileStream stream = (FileStream) result.AsyncState;
        stream.EndWrite (result);
      }
      catch (Exception ex)
      {
        state.Runner.Report (state.Closure, ex);
      }
    }

    protected virtual void ReadFromFiles (RCRunner runner,
                                          RCClosure closure,
                                          RCSymbol symbol,
                                          RCLong rows)
    {}

    public override void Receive (TcpCollector gatherer, RCSymbolScalar token)
    {
      throw new NotImplementedException ();
    }
  }
}
