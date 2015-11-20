
using System;
using System.Text;

namespace RCL.Kernel
{
  public class Eval
  {
    [RCVerb ("eval")]
    public void EvalEval (RCRunner runner, RCClosure closure, RCBlock right)
    {
      RCClosure parent = new RCClosure (closure.Parent, 
                                        closure.Bot, 
                                        right, 
                                        closure.Left, 
                                        RCBlock.Empty, 
                                        0);
      DoEval (runner, parent, right);
    }

    //Kicks off evaluation for an operator and its arguments.
    public static void DoEval (RCRunner runner, RCClosure closure, RCOperator op)
    {
      if (op.Left == null)
      {
        if (closure.Index == 0)
        {
          EvalArgument (runner, closure, op.Right);
        }
        else
        {
          op.EvalOperator (runner, closure);
        }
      }
      else
      {
        if (closure.Index == 0)
        {
          EvalArgument (runner, closure, op.Left);
        }
        else if (closure.Index == 1)
        {
          EvalArgument (runner, closure, op.Right);
        }
        else
        {
          op.EvalOperator (runner, closure);
        }
      }
    }

    //Evaluates the argument if the argument is another operator or a reference.
    protected static void EvalArgument (RCRunner runner, RCClosure closure, RCValue argument)
    {
      if (argument.ArgumentEval)
      {
        argument.Eval (
          runner, new RCClosure (closure, closure.Bot, argument, closure.Left, null, 0));
      }
      else
      {
        //pretty clear scope for optimization here, why go back to the runner in this case?
        //runner calls have a lot of overhead.
        //It was easier to write this way early on.
        DoYield (runner, closure, argument);
      }
    }

    public static void DoEvalOperator (RCRunner runner, RCClosure closure, RCOperator op)
    {
      RCValue left = closure.Result.Get ("0");
      RCValue right = closure.Result.Get ("1");

      if (left == null)
      {
        runner.Activator.Invoke (runner, closure, op.Name, right);
      }
      else
      {
        runner.Activator.Invoke (runner, closure, op.Name, left, right);
      }

      //A lot of good men died to bring us this one line of code...
      //Let's keep this around as a memorial.
      //right.BindRight (runner, closure, this, left);
    }

    //Kicks of evaluation for a block.
    public static void DoEval (RCRunner runner, RCClosure closure, RCBlock block)
    {
      if (block.Count == 0)
      {
        DoYield (runner, closure, block);
      }
      else
      {
        RCBlock current = block.GetName (closure.Index);
        if (current.Evaluator.Invoke)
        {
          string op = ((RCString) current.Value)[0];
          runner.Activator.Invoke (runner, closure, op, closure.Result);
        }
        else if (current.Evaluator.Template)
        {
          runner.Yield (closure, ExpandTemplate (new StringBuilder (),
                                                 (RCTemplate) current,
                                                 closure.Result, 0, ""));
        }
        //This means that Value is an operator or a reference.
        else if (current.Value.ArgumentEval)
        {
          current.Value.Eval (runner, new RCClosure (closure,
                                                     closure.Bot,
                                                     current.Value,
                                                     closure.Left,
                                                     null, 0));
        }
        else if (current.Evaluator.Return)
        {
          DoYield (runner, closure, current.Value);
        }
        else
        {
          if ((closure.Index < block.Count - 1)  ||
              (closure.Parent != null))
          {
            DoYield (runner, closure, current.Value);
          }
          else
          {
            //This is the end of the block.
            //Return the completed result set.
            DoYield (runner, closure, current);
          }
        }
      }
    }

    protected static RCString ExpandTemplate (StringBuilder builder, 
                                              RCTemplate template, 
                                              RCBlock right,
                                              int I,
                                              string parentIndent)
    {
      string indent = parentIndent;
      for (int i = 0; i < right.Count; ++i)
      {
        RCValue child = right.Get (i);
        RCVector<string> text = child as RCVector<string>;
        if (text == null)
        {
          ExpandTemplate (builder, template, (RCBlock) child, I + i, indent);
        }
        else
        {
          for (int j = 0; j < text.Count; ++j)
          {
            string section = text [j];
            int start = 0;
            for (int k = 0; k < section.Length; ++k)
            {
              if (section [k] == '\n')
              {
                string line;
                if (i % 2 == 1)
                {
                  if (k > 0 && section.Length > 0 && section [k - 1] == '\r')
                  {
                    line = section.Substring (start, k - start - 1);
                  }
                  else
                  {
                    line = section.Substring (start, k - start);
                  }
                  if (j > 0 || start > 0)
                  {
                    //Notice below in the section with w. If there is extra content
                    //before the code section on the same line, it will have been 
                    //inserted/indented already.
                    builder.Append (indent);
                  }
                  builder.AppendLine (line);
                }
                else
                {
                  //In content sections after the first one,
                  //skip newlines if they are the first thing in the section.
                  line = section.Substring (start, k - start);
                  if (I + i == 0)
                  {
                    builder.AppendLine (line);
                  }
                  else if (line != "")
                  {
                    if (builder [builder.Length - 1] == '\n')
                    {
                      if (start == 0 && (k < section.Length - 1 || i == right.Count - 1))
                      {
                        builder.Append (indent);
                      }
                      else if (k == section.Length - 1 && i < right.Count - 1)
                      {
                        builder.Append (indent);
                      }
                    }
                    builder.AppendLine (line);
                  }
                  else if (builder [builder.Length - 1] != '\n')
                  {
                    builder.AppendLine (line);
                    //builder.Append (indent);
                  }
                }
                start = k + 1;
              }
            }
            if (template.Multiline)
            {
              //If this is a code section, the lastPiece is just the last line of the template.
              //There is no newline at the end.
              //If this is a text section, the lastPiece is a prefix for the next code section.
              string lastPiece = section.Substring (start, section.Length - start);
              if (i % 2 == 1)
              {
                //Odd sections are always code sections.
                //Code sections don't have a newline at the end.
                if (j == 0)
                {
                  //This means there was a newline at the end of section.
                  if (start > 0 && lastPiece != "")
                  {
                    builder.Append (indent);
                  }
                }
                else if (j == text.Count - 1)
                {
                  indent = parentIndent; //"";
                }
                builder.Append (lastPiece);
              }
              else
              {
                int w;
                for (w = 0; w < lastPiece.Length; ++w)
                {
                  if (lastPiece [w] != ' ')
                  {
                    break;
                  }
                }
                //indent only includes spaces before the first non-space character.
                //The non-space part of the text is only inserted once. 
                //\t not spoken here.
                indent = parentIndent + lastPiece.Substring (0, w);
                if (i < right.Count - 1)
                {
                  builder.Append (indent);
                }
                builder.Append (lastPiece.Substring (w, lastPiece.Length - w));
              }
            }
            else
            {
              //If there are no newlines in the template then just drop the whole thing in as is.
              builder.Append (text [j]);
            }
          }
        }
      }
      //Go back and remove the final newline now.
      //Let the enclosing template decide how to finish off.
      if (template.Multiline)
      {
        if (builder [builder.Length - 1] != '\n')
        {
          builder.AppendLine ();
        }
      }
      return new RCString (builder.ToString ());
    }

    public static void DoEval (RCRunner runner, 
                               RCClosure closure, 
                               RCLReference reference)
    {
      runner.Yield (closure, 
                    Resolve (reference.m_static, closure, reference.Parts, null));
    }

    protected static RCValue Resolve (RCBlock context, 
                                      RCClosure closure, 
                                      RCArray<string> name, 
                                      RCArray<RCBlock> @this)
    {
      if (context != null)
      {
        RCValue result = context.Get (name, @this);
        if (result != null)
        {
          return result;
        }
      }
      RCClosure parent = closure;
      RCValue val = null;
      while (parent != null)
      {
        IRefable result = parent.Result;
        if (result != null)
        {
          val = result.Get (name, @this);
        }
        if (val != null)
        {
          break;
        }
        parent = parent.Parent;
      }
      if (val == null)
      {
        throw new RCException (
          //Delimit thing is annoying.
          closure, "Unable to resolve name " + RCLReference.Delimit (name, "."));
      }
      return val;
    }

    public static void DoEvalUserOp (RCRunner runner, RCClosure closure, UserOperator op)
    {
      RCValue code = closure.UserOp;
      RCArray<RCBlock> @this = closure.UserOpContext;
      if (code == null)
      {
        if (op.m_reference.Parts.Count > 1)
        {
          @this = new RCArray<RCBlock> ();
        }
        code = Resolve (op.m_reference.m_static,
                        closure,
                        op.m_reference.Parts,
                        @this);
      }
      if (code == null)
      {
        throw new Exception (
          "Cannot find definition for operator: " + op.m_reference.Name);
      }
      code.Eval (runner, UserOpClosure (closure, code, @this));
    }

    /// <summary>
    /// This method creates an identical closure where the left and right arguments can be accessed in user space.
    /// This has to be done by operators that evaluate user provided code.
    /// </summary>
    protected static RCClosure UserOpClosure (RCClosure previous,
                                              RCValue code,
                                              RCArray<RCBlock> @this)
    {
      RCValue left = previous.Result.Get ("0");
      RCValue right = previous.Result.Get ("1");
      RCBlock result = null;
      //But what if Parent is null? Can that happen?
      if (@this != null && @this.Count > 0)
      {
        result = @this [0];
        //This is only for when the this context contains more than one object.
        //I'm not even sure whether to support this, I guess I should.
        //But this is not going to be the fastest solution possible.
        for (int i = 1; i < @this.Count; ++i)
        {
          for (int j = 0; j < @this [i].Count; ++j)
          {
            RCBlock block = @this [i].GetName (j);
            result = new RCBlock (result, block.Name, ":", block.Value);
          }
        }
      }
      if (left == null)
      {
        result = new RCBlock (result, "R", ":", right);
      }
      else
      {
        result = new RCBlock (result, "L", ":", left);
        result = new RCBlock (result, "R", ":", right);
      }
      RCClosure replacement = new RCClosure (previous.Parent,
                                             previous.Bot,
                                             previous.Code,
                                             previous.Left,
                                             result,
                                             previous.Index);
      RCClosure child = new RCClosure (replacement,
                                       previous.Bot,
                                       code,
                                       previous.Left,
                                       RCBlock.Empty,
                                       0);
      return child;
    }

    public static void DoEvalInline (RCRunner runner, RCClosure closure, InlineOperator op)
    {
      op.m_code.Eval (runner, UserOpClosure (closure, op.m_code, null));
    }

    public static void DoEvalTemplate (RCRunner runner, RCClosure closure, RCTemplate template)
    {
      throw new Exception ("Not implemented");
    }

    public static void DoYield (RCRunner runner, RCClosure closure, RCValue result)
    {
      if (result == null)
        throw new ArgumentNullException ("result");

      //Do not permit any further changes to result or its children values.
      result.Lock ();
      RCClosure next = closure.Code.Next (runner, closure, closure, result);
      if (next == null)
      {
        result = closure.Code.Finish (runner, closure, result);
        closure.Bot.ChangeFiberState (closure.Fiber, "done");
        runner.Log.RecordDoc (runner, closure, "fiber", closure.Fiber, "done", result);
        if (closure.Fiber == 0 && closure.Bot.Id == 0)
        {
          runner.Finish (closure, result);
        }
        else
        {
          closure.Bot.FiberDone (runner, closure.Bot.Id, closure.Fiber, result);
        }
        //Remove closure from the pending queue.
        runner.Continue (closure, null);
      }
      else
      {
        runner.Continue (closure, next);
      }
    }

    //Construct the next closure, default case.
    public static RCClosure DoNext (RCValue val, 
                                    RCRunner runner, 
                                    RCClosure tail, 
                                    RCClosure previous, 
                                    RCValue result)
    {
      if (previous.Parent != null)
      {
        return previous.Parent.Code.Next (
          runner, tail == null ? previous : tail, previous.Parent, result);
      }
      else return null;
    }

    //Construct the next closure for a block.
    public static RCClosure DoNext (RCBlock block, 
                                    RCRunner runner, 
                                    RCClosure tail, 
                                    RCClosure previous, 
                                    RCValue result)
    {
      if (previous.Index < block.Count - 1)
      {
        return new RCClosure (
          previous.Bot, previous.Fiber, previous.Locks,
          previous.Parent, block, previous.Left,
          NextBlock (runner, block, previous, result), previous.Index + 1);
      }
      else if (previous.Parent != null)
      {
        if (block.Count == 0)
        {
          return previous.Parent.Code.Next (
            runner, tail, previous.Parent, result);
        }
        else if (block.Evaluator.Return && previous.Index == block.Count - 1)
        {
          return previous.Parent.Code.Next (
            runner, tail, previous.Parent, result);
        }
        else
        {
          return previous.Parent.Code.Next (
            runner, tail, previous.Parent, NextBlock (runner, block, previous, result));
        }
      }
      else return null;
    }

    protected static RCBlock NextBlock (RCRunner runner, 
                                        RCBlock block, 
                                        RCClosure previous, 
                                        RCValue val)
    {
      RCBlock code = block.GetName (previous.Index);
      RCBlock result = new RCBlock (
        previous.Result, code.Name, code.Evaluator, val);
      runner.Output (previous, new RCSymbolScalar (null, code.Name), val);
      return result;
    }

    //Construct the next closure for an operator.
    public static RCClosure DoNext (RCOperator op, 
                                    RCRunner runner, 
                                    RCClosure head, 
                                    RCClosure previous, 
                                    RCValue result)
    {
      if (op.Left == null)
      {
        if (previous.Index == 0)
        {
          RCValue userop;
          RCArray<RCBlock> useropContext;
          RCClosure next = new RCClosure (
            NextParentOf (op, previous, out userop, out useropContext),
            head.Bot,
            op,
            previous.Left,
            new RCBlock (null, "1", ":", result),
            previous.Index + 1,
            userop, useropContext);
          return next;
        }
        else if (previous.Index == 1 && previous.Parent != null)
        {
          return previous.Parent.Code.Next (
            runner, head == null ? previous : head, previous.Parent, result);
        }
        else return null;
      }
      else
      {
        if (previous.Index == 0)
        {
          return new RCClosure (
            previous.Parent,
            head.Bot,
            op,
            result,
            previous.Result,
            previous.Index + 1);
        }
        else if (previous.Index == 1)
        {
          RCValue userop;
          RCArray<RCBlock> useropContext;
          RCClosure next = new RCClosure (
            NextParentOf (op, previous, out userop, out useropContext),
            head.Bot,
            op,
            //reset "pocket" left to null.
            null, 
            //fold it into the current context for the final eval.
            new RCBlock (
              new RCBlock (null, "0", ":", previous.Left), "1", ":", result),
            previous.Index + 1);
          return next;
        }
        else if (previous.Index == 2 && previous.Parent != null)
        {
          return previous.Parent.Code.Next (
            runner, head == null ? previous : head, previous.Parent, result);
        }
        else if (previous.Parent != null && previous.Parent.Parent != null)
        {
          return previous.Parent.Parent.Code.Next (
            runner, head == null ? previous : head, previous.Parent.Parent, result);
        }
        else return null;
      }
    }

    protected static RCClosure NextParentOf (RCOperator op, 
                                             RCClosure previous, 
                                             out RCValue userop,
                                             out RCArray<RCBlock> useropContext)
    {
      //The only operator with IsHigherOrder set is switch.
      //Why is switch magical, why not each, take, fiber, etc...
      //Am I just missing tests for those? 
      userop = null;
      useropContext = null;
      RCClosure argument0, argument1;
      if (previous.Code.IsHigherOrder ())
      {
        return previous.Parent;
      }
      if (previous.Parent == null)
      {
        return previous.Parent;
      }
      if (!previous.Parent.Code.IsLastCall (previous.Parent, previous))
      {
        return previous.Parent;
      }
      RCClosure parent0 = OwnerOpOf (op, previous, out argument0);
      if (parent0 == null)
      {
        return previous.Parent;
      }
      if (!parent0.Code.IsLastCall (parent0, argument0))
      {
        return previous.Parent;
      }
      RCClosure parent1 = OwnerOpOf (op, parent0, out argument1);
      if (parent1 == null)
      {
        //parent1 is null when there is no switch in the last line.
        return previous.Parent;
      }
      if (!parent1.Code.IsLastCall (parent1, argument1))
      {
        return previous.Parent;
      }
      UserOperator name = op as UserOperator;
      if (name != null)
      {
        //Now we just did the this context work so you know what that means.
        //We have to pass in this.
        if (name.m_reference.Parts.Count > 1)
        {
          useropContext = new RCArray<RCBlock> ();
        }
        userop = Resolve (null, previous.Parent, name.m_reference.Parts, useropContext);
      }
      return parent1.Parent;
    }

    public static bool DoIsBeforeLastCall (RCClosure closure, RCOperator op)
    {
      if (closure.Index == 0)
      {
        return op.Left == null;
      }
      else
      {
        return closure.Index == 1;
      }
    }

    public static bool DoIsLastCall (RCClosure closure, 
                                     RCClosure arg, 
                                     RCBlock block)
    {
      //Costly call to GetName, will want to address this at some point.
      //return block.Evaluator.Return;
      return block.GetName (closure.Index).Evaluator.Return;
    }

    public static bool DoIsLastCall (RCClosure closure, 
                                     RCClosure arg, 
                                     RCOperator op)
    {
      if (closure.Index == 1)
      {
        return op.Left == null;
      }
      else
      {
        return closure.Index == 2;
      }
    }

    public static bool DoIsLastCall (RCClosure closure, 
                                     RCClosure arg, 
                                     UserOperator op)
    {
      //Commenting these lines out breaks the case where the last
      //op is not really the last op. But why?
      if (arg != null)
      {
        bool result = arg.Code.IsLastCall (arg, null);
        if (result)
        {
          result = DoIsLastCall (closure, arg, (RCOperator) op);
        }
        return result;
      }
      else
      {
        bool result = DoIsLastCall (closure, arg, (RCOperator) op);
        return result;
      }
    }

    protected static RCClosure OwnerOpOf (RCOperator op, 
                                          RCClosure closure, 
                                          out RCClosure child)
    {
      child = closure;
      RCClosure current = closure.Parent;
      while (current != null && !current.Code.ArgumentEval)
      {
        child = current;
        current = current.Parent;
      }
      return current;
    }

    public static RCValue DoFinish (RCRunner runner, 
                                    RCClosure closure, 
                                    RCValue result)
    {
      while (closure != null)
      {
        RCBlock obj = closure.Code as RCBlock;
        if (obj != null)
        {
          if (obj.Evaluator == RCEvaluator.Let && result != closure.Code)
          {
            result = NextBlock (runner, obj, closure, result);
          }
        }
        RCOperator op = closure.Code as RCOperator;
        if (op != null)
        {
          result = op.Finish (result);
        }
        if (closure.Parent != null &&
            (closure.Parent.Bot.Id != closure.Bot.Id ||
             closure.Parent.Fiber != closure.Fiber))
        {
          break;
        }
        closure = closure.Parent;
      }
      return result;
    }
  }
}