using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

namespace trash
{
    /* Kinda overengineered this one.
     * Could have used a lot of the constant states for granted 
     * but i decided to make some stuff dynamic in case i misread stuff.
     * 
     * 
     * 
     * 
     * also this found multiple solutions if you use caps in your charset
     * 
     * 
     */

    class Program
    {
        const string StartSection = "startcheck";
        const string FName = "ersi.asm";
        const int StackSize = 0x120;
        const int BaseInSize = 0x49;
        const int SEED = 0x6969420;
        //const string __CHARSET__ = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";//0123456789
        const string __CHARSET__ = "abcdefghijklmnopqrstuvwxyz";//0123456789
        private static Dictionary<string, ASMLine> ASM_Map;
        static void Main(string[] args)
        {
            string[] lines = File.ReadAllLines(FName);
            ASM_Map = new Dictionary<string, ASMLine>();

            ASMLine Current = null;
            foreach (string line in lines)
            {
                if (ASMLine.TryParse(line, Current, out ASMLine Next))
                {

                    if (Current == null || Current.Section != Next.Section)
                    {
                        if (!ASM_Map.ContainsKey(Next.Section))
                            ASM_Map[Next.Section] = Next;
                    }

                    Current = Next;
                }
            }

            char EDX = (char)0, EAX = (char)0;

            char[] Stack = new char[StackSize];

            Random r = new Random(SEED);
            //flag{{
            string IString = $"flag{{{new string(Enumerable.Repeat(__CHARSET__, BaseInSize - 6).Select(s => s[r.Next(s.Length)]).ToArray())}}}";


            IString.CopyTo(0, Stack, 0, BaseInSize);


            Solve(ref Stack, ref EDX, ref EAX);

            Console.WriteLine("Finished!");
            Console.ReadKey(false);
        }
        
        internal static void PrintStack(char[] Stack)
        {
            Console.WriteLine($"Current stack: {new string(Stack).Trim()}");
        }

        private enum SolveAction
        {
            Finished,
            JumpStart,
            Eval,
            Pop
        }

        private static void Solve(ref char[] Stack, ref char EDX, ref char EAX)
        {
            Stack<ASMLine> CallStack = new Stack<ASMLine>();
            CallStack.Push(ASM_Map[StartSection]);
            var CurrentAction = SolveAction.Finished;

            while (CallStack.Peek() == null || (CurrentAction = CallStack.Peek().Action) != SolveAction.Finished)
            {
                if (CallStack.Peek() == null)
                {
                    CallStack.Pop();
                    continue; //may cause an infinite loop, but if sections handle themselves correctly this is fine.
                }
                    
                switch (CurrentAction)
                {
                    case SolveAction.JumpStart:
                        while(CallStack.Count > 0) CallStack.Pop().Action = SolveAction.Eval;
                        CallStack.Push(ASM_Map[StartSection]);
                        break;

                    case SolveAction.Pop:
                        CallStack.Pop().Action = SolveAction.Eval;
                        break;

                    case SolveAction.Eval:
                        CallStack.Peek().Eval(ref Stack, ref EDX, ref EAX, ref CallStack);
                        break;
                }
            }

            Console.WriteLine(new string(Stack));
        }

        private abstract class ASMLine
        {
            public string Section;

            public SolveAction Action = SolveAction.Eval;

            public ASMLine Next;
            public ASMLine Previous;

            private static Regex SPR = new Regex(@"\s+", RegexOptions.Compiled);

            internal static bool TryParse(string ASMLine, ASMLine Current, out ASMLine Out)
            {
                Out = null;
                ASMLine = ASMLine.ToLower().Trim();
                string[] split = null;

                if (ASMLine.Length < 1)
                    return false; //empty line

                if (ASMLine[0] == ';')
                    return false; //comment line, end of section

                //section headers
                if ((split = ASMLine.Split(':')).Length > 1 && Regex.IsMatch(split[0], "^[a-z_0-9]+$"))
                {
                    return (Out = new SectionHeader(split[0])) != null;
                }

                split = SPR.Split(ASMLine);

                switch (split[0])
                {
                    case "nop":
                        return (Out = new NOP(Current)) != null;

                    case "movzx":
                        return (Out = new MOVZX(Current, split)) != null;

                    case "cmp":
                        return (Out = new CMP(Current, split)) != null;

                    case "jnb":
                    case "jbe":
                    case "jmp":
                        return (Out = new JUMP(Current, split)) != null;

                    case "retn":
                        return (Out = new RET(Current)) != null;

                    default:
                        throw new NotImplementedException($"Unable to parse instruction {split[0]}");
                }
            }

            public abstract void Eval(ref char[] Stack, ref char EDX, ref char EAX, ref Stack<ASMLine> CallStack);
        }

        private class SectionHeader : ASMLine
        {
            public SectionHeader(string sectionName)
            {
                Section = sectionName;
                Previous = null;
            }

            public override void Eval(ref char[] Stack, ref char EDX, ref char EAX, ref Stack<ASMLine> CallStack)
            {
                Action = SolveAction.Pop; //prevents infinite eval recursion on a section
                CallStack.Push(Next);
            }
        }

        private class RET : ASMLine
        {
            public RET(ASMLine _prev)
            {
                if (_prev == null)
                    return;

                Section = _prev.Section;
                _prev.Next = this;
                Previous = _prev;

                Action = SolveAction.Finished;
            }

            public override void Eval(ref char[] Stack, ref char EDX, ref char EAX, ref Stack<ASMLine> CallStack)
            {
                throw new InvalidOperationException("Cannot evaluate a return instruction");
            }
        }

        private class NOP : ASMLine
        { 
            public NOP(ASMLine _prev)
            {
                if (_prev == null)
                    return;

                Section = _prev.Section;
                _prev.Next = this;
                Previous = _prev;
            }

            public override void Eval(ref char[] Stack, ref char EDX, ref char EAX, ref Stack<ASMLine> CallStack)
            {
                Action = SolveAction.Pop; //allows section to be popped on a .previous() without needing to peek stack
                CallStack.Push(Next);
            }
        }
        private static KeyValuePair<int,int> Anchors = new KeyValuePair<int,int>(-0x11C, -0xD8);
        
        private class MOVZX : ASMLine
        {
            private string Register;
            private int BPOffset;

#if DEBUG
            private string DebugVarName;
#endif

            private char CacheValue;

            public MOVZX(ASMLine _prev, string[] split)
            {
                if (_prev == null)
                    return;

                Section = _prev.Section;
                _prev.Next = this;
                Previous = _prev;

                Register = split[1].Replace(",", "");
                BPOffset = Convert.ToInt32(split[2].Replace("[rbp+var_", "").Replace("]", ""), 16);
                BPOffset *= -1;
#if DEBUG
                DebugVarName = split[2];
#endif
            }

            internal bool IS_A() { return Register == "eax" || Register == "al" || Register == "rax"; }

            public override void Eval(ref char[] Stack, ref char EDX, ref char EAX, ref Stack<ASMLine> CallStack)
            {
                bool IsEax;
                Action = SolveAction.Pop;
                CacheValue = (IsEax = Register == "eax") ? EAX : EDX;

                if (IsEax)
                    EAX = Stack[Stack.Length + BPOffset];
                else
                    EDX = Stack[Stack.Length + BPOffset];

                CallStack.Push(Next);
            }

            public bool Anchored()
            {
                return BPOffset >= Anchors.Value || BPOffset <= Anchors.Key;
            }

            public void RefCycle(bool IsUp, ref char[] Stack, ref Stack<ASMLine> CallStack)
            {
                char val = Stack[Stack.Length + BPOffset];

                int index = __CHARSET__.IndexOf(val) + (IsUp ? 1 : -1);

                if (index == -1)
                    index = __CHARSET__.Length - 1;

                if (index == __CHARSET__.Length)
                    index = 0;

                Stack[Stack.Length + BPOffset] = __CHARSET__[index]; //why the fuck cant i cast a bool to an int cs?

                Action = SolveAction.JumpStart;
                CallStack.Push(this);
                PrintStack(Stack);
            }

            public bool CheckOffset(int Offset) { return Offset == BPOffset; }
        }

        private class CMP : ASMLine
        {
            private string R1;
            private string R2;

            public bool CF;
            public bool ZF;

            public CMP(ASMLine _prev, string[] split)
            {
                if (_prev == null)
                    return;

                Section = _prev.Section;
                _prev.Next = this;
                Previous = _prev;

                R1 = split[1].Replace(",", "");
                R2 = split[2];
            }

            public override void Eval(ref char[] Stack, ref char EDX, ref char EAX, ref Stack<ASMLine> CallStack)
            {
                Action = SolveAction.Pop;

                var destination = R1 == "al" ? EAX : EDX;
                var source = R2 == "al" ? EAX : EDX; //could optimize with a set and invert but fuck it

                CF = ZF = false;

                //https://www.aldeid.com/wiki/X86-assembly/Instructions/cmp

                if (destination == source)
                    ZF = true;

                if (destination < source)
                    CF = true;

                CallStack.Push(Next);
            }

            public void Cycle(byte _r, bool Up, ref char[] Stack, ref Stack<ASMLine> CallStack)
            {
                bool NeedsA = (_r == 2 ? R2 : R1) == "al";

                MOVZX mov;
                while((mov = CallStack.Peek() as MOVZX) == null || mov.IS_A() != NeedsA)
                {
                    CallStack.Pop().Action = SolveAction.Eval;
                    if (CallStack.Count < 1)
                    {
                        Action = SolveAction.JumpStart;
                        CallStack.Push(this);
                        return;
                    }
                }

                mov.RefCycle(Up, ref Stack, ref CallStack);
            }
        }

        private class JUMP : ASMLine
        {
            private string To;
            private enum JumpType
            {
                jnb,
                jbe,
                jmp
            }

            private JumpType Type;

            public JUMP(ASMLine _prev, string[] split)
            {
                if (_prev == null)
                    return;

                Section = _prev.Section;
                _prev.Next = this;
                Previous = _prev;


                Enum.TryParse(split[0], true, out Type);

                To = split[1] == "short" ? split[2] : split[1];
            }

            public override void Eval(ref char[] Stack, ref char EDX, ref char EAX, ref Stack<ASMLine> CallStack)
            {
                Action = SolveAction.Eval;
                
                if(To.Contains("die")) //oshit
                {
                    CallStack.Pop(); //this

                    JUMP PrevJump = null;

                    while ((PrevJump = CallStack.Peek() as JUMP) == null)
                    {
                        CallStack.Pop().Action = SolveAction.Eval;
                        if (CallStack.Count < 1)
                        {
                            Action = SolveAction.JumpStart;
                            CallStack.Push(this);
                            return;
                        }
                    }
     
                    PrevJump.TrySolve(ref Stack, ref CallStack);
                }
                else
                {
                    var _cmp = (Previous as CMP); //hope this never turns out bad thonkers

                    if (Type == JumpType.jmp || (!_cmp.CF) == (Type == JumpType.jnb))
                        CallStack.Push(ASM_Map[To]);
                    else
                        CallStack.Push(Next);
                }
            }

            private void TrySolve(ref char[] Stack, ref Stack<ASMLine> CallStack)
            {
                Action = SolveAction.Eval;
                if(Type == JumpType.jmp)
                    throw new InvalidOperationException("Cannot solve an unconditional jump");
                
                CMP LastCMP;

                while ((LastCMP = CallStack.Peek() as CMP) == null)
                {
                    CallStack.Pop().Action = SolveAction.Eval;
                    if (CallStack.Count < 1)
                    {
                        Action = SolveAction.JumpStart;
                        CallStack.Push(this);
                        return;
                    }
                }

                /* Conflict solver:
                 * If we peek back to the last two movs (cmp.previous and cmp.previous.previous)
                 * we can determine the comparisons. Choose the one to dec/inc based on the anchors.
                 * if *both* are anchored, something has gone horribly wrong...
                 * if neither are anchored, choose ax
                 */
                 //TODO determine how the anchors are moved...

                MOVZX axMov = LastCMP.Previous as MOVZX;
                MOVZX dxMov = axMov.Previous as MOVZX;

                if (axMov.Anchored() && dxMov.Anchored()) //Todo: better handling of this would be to ignore the conflict but disable the win condition.
                    throw new InvalidOperationException("Cannot solve two anchored indexes...");

                if(!axMov.Anchored())
                    LastCMP.Cycle(2, Type != JumpType.jbe, ref Stack, ref CallStack);
                
                if(!dxMov.Anchored())
                    LastCMP.Cycle(1, Type == JumpType.jbe, ref Stack, ref CallStack);
            }
        }
    }
}
