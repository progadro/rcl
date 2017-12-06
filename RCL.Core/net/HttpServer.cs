using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Web;
using System.Text;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using RCL.Kernel;

namespace RCL.Core
{
  public class HttpCertCheck
  {
    [RCVerb ("httpcertcheck")]
    public void EvalHttpCertCheck (RCRunner runner, RCClosure closure, RCString right)
    {
      if (right[0] == "Enabled")
      {
        runner.Log.Record (runner, closure, "http", 0, "certcheck", "Enabled");
        ServicePointManager.ServerCertificateValidationCallback = null;
      }
      else if (right[0] == "AllowSelfSigned")
      {
        runner.Log.Record (runner, closure, "http", 0, "certcheck", "AllowSelfSigned");
        ServicePointManager.ServerCertificateValidationCallback =
          new CertificateValidator (runner, closure).AllowSelfSigned;
      }
      else if (right[0] == "None")
      {
        runner.Log.Record (runner, closure, "http", 0, "certcheck", "NoChecking");
        ServicePointManager.ServerCertificateValidationCallback =
          new CertificateValidator (runner, closure).NoChecking;
      }
      else 
      {
        throw new Exception ("Valid values are \"Enabled\", \"AllowSelfSigned\" and \"NoChecking\"");
      }
      runner.Yield (closure, right);
    }

    protected class CertificateValidator
    {
      protected readonly RCRunner Runner;
      protected readonly RCClosure Closure;

      public CertificateValidator (RCRunner runner, RCClosure closure)
      {
        Runner = runner;
        Closure = closure;
      }

      public bool NoChecking (System.Object sender,
                              X509Certificate certificate,
                              X509Chain chain,
                              SslPolicyErrors sslPolicyErrors)
      {
        Runner.Log.Record (Runner, Closure, "http", 0, "certcheck", "Allowing request in spite of policy error");
        return true;
      }

      public bool AllowSelfSigned (System.Object sender,
                                    X509Certificate certificate,
                                    X509Chain chain,
                                    SslPolicyErrors sslPolicyErrors)
      {
        try
        {
          // If there are errors in the certificate chain, look at each error to determine the cause.
          if (sslPolicyErrors != SslPolicyErrors.None)
          {
            for (int i = 0; i < chain.ChainStatus.Length; i++)
            {
              if (chain.ChainStatus[i].Status != X509ChainStatusFlags.RevocationStatusUnknown)
              {
                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan (0, 1, 0);
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                bool chainIsValid = chain.Build ((X509Certificate2) certificate);
                if (!chainIsValid)
                {
                  Runner.Log.Record (Runner, Closure, "http", 0, "certcheck", "Allowing request in spite of policy error");
                  return false;
                }
              }
            }
            Runner.Log.Record (Runner, Closure, "http", 0, "certcheck", "Allowing request in spite of policy error");
          }
        }
        catch (Exception ex)
        {
          Runner.Report (Closure, ex);
          return false;
        }
        return true;
      }
    }
  }

  public class HttpServer : IDisposable
  {
    protected internal object m_lock = new object ();
    protected int m_listener = 0;
    protected internal Dictionary<int, HttpListener> m_listeners =
      new Dictionary<int, HttpListener> ();

    //I'm sticking to the terminology of the api here but the context
    //is really a request.  And we have to keep them as mutable state
    //in order to respond through the http listener.
    protected int m_context = 0;
    protected internal readonly Dictionary<int, RequestInfo> m_contexts =
      new Dictionary<int, RequestInfo> ();
    protected int m_client = 0;

    //This is only for the logging on dispose, so that the bot number will be accurate.
    protected long m_bot = 0;

    public HttpServer () {}

    public class RequestInfo
    {
      public readonly HttpListenerContext Context;
      public readonly DateTime Time;

      public RequestInfo (HttpListenerContext context, DateTime time)
      {
        Context = context;
        Time = time;
      }
    }

    [RCVerb ("httpstart")]
    public void EvalHttpStart (
      RCRunner runner, RCClosure closure, RCString right)
    {
      HttpListener listener = new HttpListener ();
      for (int i = 0; i < right.Count; ++i)
      {
        listener.Prefixes.Add (right[i]);
      }
      listener.Start ();
      int handle;
      lock (m_lock)
      {
        ++m_listener;
        handle = m_listener;
        m_listeners.Add (handle, listener);
        // set these to optimal for MONO and .NET
        //lgsp.Expect100Continue = false;
        //lgsp.UseNagleAlgorithm = true;
        //lgsp.MaxIdleTime = 100000;
        runner.Log.Record (runner, closure, "http", handle, "start", right);
        m_bot = closure.Bot.Id;
      }
      runner.Yield (closure, new RCLong (handle));
    }

    [RCVerb ("httpstop")]
    public void EvalHttpStop (
      RCRunner runner, RCClosure closure, RCLong right)
    {
      lock (m_lock)
      {
        for (int i = 0; i < right.Count; ++i)
        {
          HttpListener listener = m_listeners[(int) right[i]];
          //I should be ok calling this in a lock right?
          listener.Close ();
          runner.Log.Record (runner, closure, "http", right[i], "stop", "");
          //Shouldn't I remove this from the m_listeners?
          //Wait I want to see if retaining it fixes the object disposed exception.
        }
      }
      runner.Yield (closure, new RCBoolean (true));
    }

    [RCVerb ("httprecv")]
    public void EvalHttpRecv (
      RCRunner runner, RCClosure closure, RCLong right)
    {
      HttpListener listener;
      //Let's have only one listener at a time, at least for now.
      if (right.Count > 1)
      {
        throw new Exception ("Can only httprecv from one listener per call");
      }

      lock (m_lock)
      {
        listener = m_listeners[(int) right[0]];
        listener.BeginGetContext (new AsyncCallback (Process),
                                  new RCAsyncState (runner, closure, listener));
      }
      //These updates were just noise in the log file.
      //runner.Log.Record (runner, closure, closure.Bot.Id, "https", right[0], "recv", "");
    }

    [RCVerb ("httpcheck")]
    public void EvalHttpCheck (
      RCRunner runner, RCClosure closure, RCString left, RCLong right)
    {
      RequestInfo info;
      //Let's have only one listener at a time, at least for now.
      if (right.Count > 1)
      {
        throw new Exception ("Can only httprecv from one listener per call");
      }
      if (left.Count != 2)
      {
        throw new Exception ("Left argument must have the form \"id\" \"1234567\"");
      }
      lock (m_lock)
      {
        info = m_contexts[(int) right[0]];
      }
      Cookie cookie = info.Context.Request.Cookies[left[0]];
      if (cookie == null || !cookie.Value.Equals (left[1]))
      {
        info.Context.Response.StatusCode = 401;
        info.Context.Response.OutputStream.Close ();
        runner.Log.Record (runner, closure, "http", right[0], "session", cookie != null ? cookie.Value : "null");
        throw new RCException (closure, RCErrors.Session, "Invalid session id");
      }
      runner.Yield (closure, right);
    }

    [RCVerb ("httpcookie")]
    public void EvalHttpCookie (
      RCRunner runner, RCClosure closure, RCLong right)
    {
      RequestInfo info;
      if (right.Count > 1)
      {
        throw new Exception ("Can only get one httpcookie per call");
      }
      lock (m_lock)
      {
        info = m_contexts[(int) right[0]];
      }
      RCBlock result = RCBlock.Empty;
      for (int i = 0; i < info.Context.Request.Cookies.Count; ++i)
      {
        Cookie cookie = info.Context.Request.Cookies[i];
        result = new RCBlock (result, cookie.Name, ":", new RCString (cookie.Value));
      }
      runner.Yield (closure, result);
    }

    protected void Process (IAsyncResult result)
    {
      RCAsyncState state = (RCAsyncState) result.AsyncState;
      HttpListener listener = (HttpListener) state.Other;
      try
      {
        int handle;
        HttpListenerContext context;
        lock (m_lock)
        {
          context = listener.EndGetContext (result);
          m_context++;
          handle = m_context;
          m_contexts.Add (handle, new RequestInfo (context, DateTime.Now));
        }
        state.Runner.Log.Record (state.Runner, state.Closure,
                                 "http", handle, "recieve",
                                 context.Request.HttpMethod + " " + context.Request.RawUrl);
        state.Runner.Yield (state.Closure, new RCLong (handle));
      }
      catch (ObjectDisposedException)
      {
        //This happens when you close down the listener
        //Seems like this is the best known solution.
        //http://stackoverflow.com/questions/13351615/cleanly-interupt-httplisteners-begingetcontext-method
      }
      catch (Exception ex)
      {
        state.Runner.Report (state.Closure, ex);
      }
    }

    [RCVerb ("httpqs")]
    public void EvalHttpQs (
      RCRunner runner, RCClosure closure, RCLong right)
    {
      if (right.Count > 1)
      {
        throw new Exception ("httpqs only allows one request per call");
      }

      RequestInfo info;
      lock (m_lock)
      {
        info = m_contexts[(int) right[0]];
      }
      RCBlock query = RCBlock.Empty;
      for (int i = 0; i < info.Context.Request.QueryString.Count; ++i)
      {
        query = new RCBlock (query,
                             info.Context.Request.QueryString.Keys[i], ":",
                             new RCString (info.Context.Request.QueryString[i]));
      }
      runner.Yield (closure, query);
    }

    [RCVerb ("httpmethod")]
    public void EvalHttpMethod (
      RCRunner runner, RCClosure closure, RCLong right)
    {
      if (right.Count > 1)
      {
        throw new Exception ("httpheader only allows one request per call");
      }

      RequestInfo info;
      lock (m_lock)
      {
        info = m_contexts[(int) right[0]];
      }

      RCSymbolScalar method = new RCSymbolScalar (null,
                                                  info.Context.Request.HttpMethod.ToLower ());
      runner.Yield (closure, new RCSymbol (method));
    }

    /*
      http://stackoverflow.com/questions/2019735/request-rawurl-vs-request-url
      
      http://localhost:12345/site/page.aspx?q1=1&q2=2
      Value of HttpContext.Current.Request.Url.Host
      localhost
      
      Value of HttpContext.Current.Request.Url.Authority
      localhost:12345
      
      Value of HttpContext.Current.Request.Url.AbsolutePath
      /site/page.aspx
      
      Value of HttpContext.Current.Request.ApplicationPath
      /site
      
      Value of HttpContext.Current.Request.Url.AbsoluteUri
      http://localhost:12345/site/page.aspx?q1=1&q2=2
      
      Value of HttpContext.Current.Request.RawUrl
      /site/page.aspx?q1=1&q2=2
      
      Value of HttpContext.Current.Request.Url.PathAndQuery
      /site/page.aspx?q1=1&q2=2
    */

    [RCVerb ("httpheader")]
    public void EvalHttpHeader (
      RCRunner runner, RCClosure closure, RCLong right)
    {
      if (right.Count > 1)
      {
        throw new Exception ("httpheader only allows one request per call");
      }

      RequestInfo info;
      lock (m_lock)
      {
        info = m_contexts[(int) right[0]];
      }

      NameValueCollection values = info.Context.Request.Headers;
      RCBlock result = RCBlock.Empty;
      result = new RCBlock (result, "Verb", ":", new RCString (info.Context.Request.HttpMethod));
      result = new RCBlock (result, "RawUrl", ":", new RCString (info.Context.Request.RawUrl));
      result = new RCBlock (result, "Url", ":", new RCString (info.Context.Request.Url.AbsolutePath));
      for (int i = 0; i < values.AllKeys.Length; ++i)
      {
        string key = values.AllKeys[i];
        result = new RCBlock (result, key, ":", new RCString (values[key]));
      }
      runner.Yield (closure, result);
    }

    [RCVerb ("httpbody")]
    public void EvalHttpBody (
      RCRunner runner, RCClosure closure, RCLong right)
    {
      if (right.Count > 1)
      {
        throw new Exception ("httpbody only allows one request per call");
      }

      RequestInfo info;
      lock (m_lock)
      {
        info = m_contexts[(int) right[0]];
      }
      string body = new StreamReader (info.Context.Request.InputStream).ReadToEnd ();
      //ParseQueryString really means ParseUrlEncodedForm.
      NameValueCollection values = HttpUtility.ParseQueryString (body);
      RCBlock result = RCBlock.Empty;
      for (int i = 0; i < values.AllKeys.Length; ++i)
      {
        string key = values.AllKeys[i];
        result = new RCBlock (result, key, ":", new RCString (values[key]));
      }
      runner.Yield (closure, result);
    }

    [RCVerb ("httpraw")]
    public void EvalHttpRaw (
      RCRunner runner, RCClosure closure, RCLong right)
    {
      if (right.Count > 1)
      {
        throw new Exception ("httpraw only allows one request per call");
      }

      RequestInfo info;
      lock (m_lock)
      {
        info = m_contexts[(int) right[0]];
      }
      string body = new StreamReader (info.Context.Request.InputStream).ReadToEnd ();
      runner.Yield (closure, new RCString (body));
    }

    //This should be called httpreply, not send, for consistency with other apis.
    [RCVerb ("httpsend")]
    public void EvalHttpSend (
      RCRunner runner, RCClosure closure, RCLong left, RCBlock right)
    {
      try
      {
        DoHttpSend (runner, closure, left, right);
      }
      catch (Exception)
      {
        throw;
      }
    }

    [RCVerb ("httpsend")]
    public void EvalHttpSend (
      RCRunner runner, RCClosure closure, RCLong left, RCString right)
    {
      try
      {
        //Maybe we should send multiple here?
        DoHttpSend (runner, closure, left, right[0]);
      }
      catch (Exception)
      {
        throw;
      }
    }

    [RCVerb ("httpsend")]
    public void EvalHttpSend (
      RCRunner runner, RCClosure closure, RCLong left, RCByte right)
    {
      try
      {
        //Maybe we should send multiple here?
        DoHttpSend (runner, closure, left, right.ToArray ());
      }
      catch (Exception)
      {
        throw;
      }
    }

    protected void DoHttpSend (
      RCRunner runner, RCClosure closure, RCLong left, byte[] payload)
    {
      RCBlock result = RCBlock.Empty;
      int total = 0;
      try
      {
        for (int i = 0; i < left.Count; ++i)
        {
          RequestInfo info;
          lock (m_lock)
          {
            info = m_contexts[(int) left[i]];
          }
          try
          {
            byte[] buffer = new byte[1024 * 16];
            MemoryStream stream = new MemoryStream (payload);
            int nbytes;
            while ((nbytes = stream.Read (buffer, 0, buffer.Length)) > 0)
            {
              info.Context.Response.OutputStream.Write (buffer, 0, nbytes);
              total += nbytes;
            }
            result = CreateLogEntry (info, left[i], total);
          }
          catch (Exception ex)
          {
            runner.Report (closure, ex);
            result = CreateLogEntry (info, left[i], 0);
          }
          finally
          {
            runner.Log.Record (runner, closure, "http", left[i], "send", result);
            info.Context.Response.OutputStream.Close ();
            info.Context.Request.InputStream.Close ();
            lock (m_lock)
            {
              m_contexts.Remove ((int) left[i]);
            }
          }
        }
      }
      catch (Exception ex)
      {
        runner.Report (closure, ex);
      }
      runner.Yield (closure, result);
    }

    protected void DoHttpSend (
      RCRunner runner, RCClosure closure, RCLong left, string message)
    {
      RCBlock result = RCBlock.Empty;
      int total = 0;
      try
      {
        for (int i = 0; i < left.Count; ++i)
        {
          RequestInfo info;
          lock (m_lock)
          {
            info = m_contexts[(int) left[i]];
          }
          try
          {
            byte[] payload = Encoding.UTF8.GetBytes (message);
            byte[] buffer = new byte[1024 * 16];
            MemoryStream stream = new MemoryStream (payload);
            int nbytes;
            while ((nbytes = stream.Read (buffer, 0, buffer.Length)) > 0)
            {
              info.Context.Response.OutputStream.Write (buffer, 0, nbytes);
              total += nbytes;
            }
            result = CreateLogEntry (info, left[i], total);
          }
          catch (Exception ex)
          {
            runner.Report (closure, ex);
            result = CreateLogEntry (info, left[i], 0);
          }
          finally
          {
            runner.Log.Record (runner, closure, "http", left[i], "send", result);
            info.Context.Response.OutputStream.Close ();
            info.Context.Request.InputStream.Close ();
            lock (m_lock)
            {
              m_contexts.Remove ((int) left[i]);
            }
          }
        }
      }
      catch (Exception ex)
      {
        runner.Report (closure, ex);
      }
      runner.Yield (closure, result);
    }

    protected RCBlock CreateLogEntry (RequestInfo info, long id, int bytes)
    {
      HttpListenerContext context = info.Context;
      string ip = context.Request.UserHostAddress;
      string user = context.User != null ? info.Context.User.Identity.Name : "";
      string resource = string.Format ("{0} {1} HTTP/{2}", 
                                       context.Request.HttpMethod, 
                                       context.Request.Url.LocalPath, 
                                       context.Request.ProtocolVersion.ToString ());
      string timestamp = string.Format ("[{0:dd/MMM/yyyy:hh:mm:ss zzz}]", info.Time);
      string httpversion = context.Request.ProtocolVersion.ToString ();
      string status = context.Response.StatusCode.ToString ();
      string byteString = bytes.ToString ();
      string referrer = context.Request.UrlReferrer != null ? 
                        context.Request.UrlReferrer.ToString () : "";
      string agent = context.Request.UserAgent != null ? 
                     context.Request.UserAgent : "";
      string cookie = "";
      if (context.Request.Cookies != null &&
          context.Request.Cookies.Count > 0)
      {
        StringBuilder builder = new StringBuilder ();
        for (int i = 0; i < context.Request.Cookies.Count; ++i)
        {
          Cookie c = context.Request.Cookies[i];
          builder.Append (c.Name);
          if (c.Value != null)
          {
            builder.Append ("=");
            builder.Append (c.Value);
          }
          if (i < context.Request.Cookies.Count - 1)
          {
            builder.Append ("; ");
          }
        }
        cookie = builder.ToString ();
      }
      RCBlock result = RCBlock.Empty;
      result = new RCBlock (result, "id", ":", new RCString (id.ToString ()));
      result = new RCBlock (result, "ip", ":", new RCString (ip));
      result = new RCBlock (result, "user", ":", new RCString (user));
      result = new RCBlock (result, "resource", ":", new RCString (resource));
      result = new RCBlock (result, "timestamp", ":", new RCString (timestamp));
      result = new RCBlock (result, "httpversion", ":", new RCString (httpversion));
      result = new RCBlock (result, "status", ":", new RCString (status));
      result = new RCBlock (result, "size", ":", new RCString (byteString));
      result = new RCBlock (result, "referrer", ":", new RCString (referrer));
      result = new RCBlock (result, "agent", ":", new RCString (agent));
      result = new RCBlock (result, "cookie", ":", new RCString (cookie));
      return result;
    }

    protected void DoHttpSend (
      RCRunner runner, RCClosure closure, RCLong left, RCBlock right)
    {
      if (left.Count > 1)
      {
        throw new Exception (
          "httpsend only allows one request per call.  Maybe this can change though.");
      }
      RequestInfo info;
      lock (m_lock)
      {
        info = m_contexts[(int) left[0]];
      }
      try
      {
        RCLong status = (RCLong) right.Get ("status");
        RCBlock headers = (RCBlock) right.Get ("headers");
        RCString body = (RCString) right.Get ("body");
        if (status != null)
        {
          if (status[0] == 0)
          {
            info.Context.Response.StatusCode = 200;
          }
          else if (status[0] == 1)
          {
            info.Context.Response.StatusCode = 400;
          }
          else
          {
            info.Context.Response.StatusCode = (int) status[0];
          }
        }
        if (headers != null)
        {
          for (int i = 0; i < headers.Count; ++i)
          {
            RCBlock header = headers.GetName (i);
            RCString val = (RCString) header.Value;
            info.Context.Response.AppendHeader (header.RawName, val[0]);
          }
        }
        if (body == null)
        {
          body = new RCString ("");
        }
        byte[] bytes = Encoding.UTF8.GetBytes (body[0]);
        byte[] buffer = new byte[1024 * 16];
        MemoryStream stream = new MemoryStream (bytes);
        int nbytes;
        while ((nbytes = stream.Read (buffer, 0, buffer.Length)) > 0)
        {
          info.Context.Response.OutputStream.Write (buffer, 0, nbytes);
        }
      }
      catch (Exception)
      {
        info.Context.Response.StatusCode = 400;
      }
      finally
      {
        info.Context.Response.OutputStream.Close ();
        runner.Yield (closure, left);
      }
    }

    public void Dispose ()
    {
      Dispose (true);
      GC.SuppressFinalize (this);
    }

    protected bool m_disposed = false;
    protected virtual void Dispose (bool disposing)
    {
      if (!m_disposed)
      {
        if (disposing)
        {
          lock (m_lock)
          {
            foreach (KeyValuePair<int, HttpServer.RequestInfo> kv in m_contexts)
            {
              RCLogger.RecordFilter (m_bot, (long) 0, "http", (long) kv.Key, "cancel", kv.Value.Context.Request.RawUrl);
              kv.Value.Context.Response.OutputStream.Close ();
            }
            foreach (KeyValuePair<int, HttpListener> kv in m_listeners)
            {
              //I should be ok calling this in a lock right?
              foreach (string prefix in kv.Value.Prefixes)
              {
                RCLogger.RecordFilter (m_bot, (long) 0, "http", (long) kv.Key, "close", prefix);
              }
              kv.Value.Close ();
            }
            m_contexts.Clear ();
            m_listeners.Clear ();
          }
        }
        m_disposed = true;
      }
    }
  }
}
